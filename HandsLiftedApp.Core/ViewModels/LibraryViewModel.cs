using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Models.PlaylistActions;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.Utils;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.ViewModels
{
    public class LibraryViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, Unit> OnAddSelectedToPlaylistCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateNewSongCommand { get; }
        private ObservableAsPropertyHelper<string> _selectedItemPreview;

        public string SelectedItemPreview
        {
            get => _selectedItemPreview.Value;
        }

        private Bitmap? _selectedItemImagePreview;

        public Bitmap? SelectedItemImagePreview
        {
            get => _selectedItemImagePreview;
            set => this.RaiseAndSetIfChanged(ref _selectedItemImagePreview, value);
        }

        private CancellationTokenSource _cancellationTokenSource;

        public LibraryViewModel()
        {
            // config is for loading...
            LibraryConfig libraryConfig = new LibraryConfig();
            libraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
                { Label = "Songs", Directory = @"D:\data\VisionScreens\Song Library" });
            libraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
            {
                Label = "Church News",
                Directory = @"H:\.shortcut-targets-by-id\1VCRKC34SblCDK3hzr9hkb7b5wMjUQF5R\Service Docs\Church News"
            });
            libraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
            {
                Label = "Sermon Media",
                Directory = @"H:\.shortcut-targets-by-id\1VCRKC34SblCDK3hzr9hkb7b5wMjUQF5R\Service Docs\Sermon Media"
            });
            libraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
            {
                Label = "Kids Talks",
                Directory = @"H:\.shortcut-targets-by-id\1VCRKC34SblCDK3hzr9hkb7b5wMjUQF5R\Service Docs\Kids Talks"
            });

            // runtime...
            Libraries = new ObservableCollection<Library>();

            foreach (var library in libraryConfig.LibraryItems)
            {
                Libraries.Add(new Library(library));
            }

            Debug.Print(libraryConfig.ToString());

            // constructor
            _searchResults = this
                .WhenAnyValue(x => x.SearchTerm)
                //.Throttle(TimeSpan.FromMilliseconds(100))
                .Select(term => term?.Trim().ToLower())
                .DistinctUntilChanged()
                //.Where(term => !string.IsNullOrWhiteSpace(term))
                .SelectMany(SearchLibrary)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SearchResults);

            // We subscribe to the "ThrownExceptions" property of our OAPH, where ReactiveUI 
            // marshals any exceptions that are thrown in SearchNuGetPackages method. 
            // See the "Error Handling" section for more information about this.
            _searchResults.ThrownExceptions.Subscribe(error =>
            {
                /* Handle errors here */
            });

            // A helper method we can use for Visibility or Spinners to show if results are available.
            // We get the latest value of the SearchResults and make sure it's not null.
            _isAvailable = this
                .WhenAnyValue(x => x.SearchResults)
                .Select(searchResults => searchResults != null)
                .ToProperty(this, x => x.IsAvailable);

            _cancellationTokenSource = new CancellationTokenSource();

            _selectedItemPreview = this.WhenAnyValue(x => x.SelectedItem, (_SelectedItem) =>
                    {
                        if (_SelectedItem != null)
                        {
                            try
                            {
                                string extension = new FileInfo(_SelectedItem.FullFilePath).Extension.ToLower();
                                if (!Constants.SUPPORTED_IMAGE.Contains(extension.TrimStart('.')))
                                {
                                    long length = new FileInfo(_SelectedItem.FullFilePath).Length;
                                    if (length < 200_000) // 0.2MB
                                    {
                                        return File.ReadAllText(_SelectedItem.FullFilePath);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Attempt to read file for library preview failed", ex);
                            }
                        }

                        return null;
                    })
                    .ToProperty(this, c => c.SelectedItemPreview)
                ;
            this.WhenAnyValue(x => x.SelectedItem)
                // .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe((_SelectedItem) =>
                {
                    _cancellationTokenSource.Cancel();

                    // Task.Run(() =>
                    // {
                        if (_SelectedItem != null)
                        {
                            try
                            {
                                string extension = new FileInfo(_SelectedItem.FullFilePath).Extension.ToLower();
                                if (Constants.SUPPORTED_IMAGE.Contains(extension.TrimStart('.')))
                                {
                                    // if (File.Exists(x) || AssetLoader.Exists(new Uri(x)))
                                    // {
                                    SelectedItemImagePreview = BitmapLoader.LoadBitmap(_SelectedItem.FullFilePath, 800);
                                    // } 
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Attempt to read file for library preview failed", ex);
                            }
                        }

                        SelectedItemImagePreview = null;
                    // }, _cancellationTokenSource.Token);
                });

            OnAddSelectedToPlaylistCommand = ReactiveCommand.Create(() =>
            {
                if (SelectedItem != null)
                {
                    List<string> items = new List<string>() { SelectedItem.FullFilePath };
                    MessageBus.Current.SendMessage(new AddItemToPlaylistMessage(items));
                }
            });

            CreateNewSongCommand = ReactiveCommand.Create(() =>
            {
                // TODO remove dependency on Globals.MainViewModel.Playlist 
                MessageBus.Current.SendMessage(new MainWindowModalMessage(new SongEditorWindow(), false,
                    new SongEditorViewModel(new SongItemInstance(null), Globals.Instance.MainViewModel.Playlist)));
            });
        }

        // In ReactiveUI, this is the syntax to declare a read-write property
        // that will notify Observers, as well as WPF, that a property has 
        // changed. If we declared this as a normal property, we couldn't tell 
        // when it has changed!
        private string _searchTerm;

        public string SearchTerm
        {
            get => _searchTerm;
            set => this.RaiseAndSetIfChanged(ref _searchTerm, value);
        }

        // Here's the interesting part: In ReactiveUI, we can take IObservables
        // and "pipe" them to a Property - whenever the Observable yields a new
        // value, we will notify ReactiveObject that the property has changed.
        // 
        // To do this, we have a class called ObservableAsPropertyHelper - this
        // class subscribes to an Observable and stores a copy of the latest value.
        // It also runs an action whenever the property changes, usually calling
        // ReactiveObject's RaisePropertyChanged.
        private readonly ObservableAsPropertyHelper<IEnumerable<Item>> _searchResults;
        public IEnumerable<Item> SearchResults => _searchResults.Value;

        // Here, we want to create a property to represent when the application 
        // is performing a search (i.e. when to show the "spinner" control that 
        // lets the user know that the app is busy). We also declare this property
        // to be the result of an Observable (i.e. its value is derived from 
        // some other property)
        private readonly ObservableAsPropertyHelper<bool> _isAvailable;
        public bool IsAvailable => _isAvailable.Value;

        private Item _selectedItem;

        public Item SelectedItem
        {
            get => _selectedItem;
            set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
        }

        private async Task<IEnumerable<Item>> SearchLibrary(
            string term, CancellationToken token)
        {
            return new List<Item>();
            // if (term == null || term.Length == 0)
            //     return Items;
            //
            // // TODO: filter by file *content* as well (full-text search)
            // return Items.Where(item => item.Title.ToLower().Contains(term));
        }


        public ObservableCollection<Library> Libraries { get; set; }

        private Library _selectedLibrary;

        public Library SelectedLibrary
        {
            get => _selectedLibrary;
            set => this.RaiseAndSetIfChanged(ref _selectedLibrary, value);
        }


        // selected collection

        // selected item previews
    }
}