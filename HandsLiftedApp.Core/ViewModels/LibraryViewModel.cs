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
using Avalonia.Controls;
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
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
        
        private bool _isPreviewLoading = false;

        public bool IsPreviewLoading
        {
            get => _isPreviewLoading;
            set => this.RaiseAndSetIfChanged(ref _isPreviewLoading, value);
        }

        private Bitmap? _selectedItemImagePreview;

        public Bitmap? SelectedItemImagePreview
        {
            get => _selectedItemImagePreview;
            set => this.RaiseAndSetIfChanged(ref _selectedItemImagePreview, value);
        }

        private LibraryConfig _libraryConfig = new LibraryConfig();

        public LibraryConfig LibraryConfig
        {
            get => _libraryConfig;
            set => this.RaiseAndSetIfChanged(ref _libraryConfig, value);
        }

        private CancellationTokenSource _cancellationTokenSource;

        public void ReloadLibraries()
        {
            var lastSavedConfig = LoadConfig();
            if (lastSavedConfig == null || lastSavedConfig.LibraryItems.Count == 0)
            {
                // config is for loading...
                LibraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
                    { Label = "Songs", Directory = @"D:\data\VisionScreens\Song Library" });
                LibraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
                {
                    Label = "Church News",
                    Directory = @"H:\.shortcut-targets-by-id\1VCRKC34SblCDK3hzr9hkb7b5wMjUQF5R\Service Docs\Church News"
                });
                LibraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
                {
                    Label = "Sermon Media",
                    Directory =
                        @"H:\.shortcut-targets-by-id\1VCRKC34SblCDK3hzr9hkb7b5wMjUQF5R\Service Docs\Sermon Media"
                });
                LibraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
                {
                    Label = "Kids Talks",
                    Directory = @"H:\.shortcut-targets-by-id\1VCRKC34SblCDK3hzr9hkb7b5wMjUQF5R\Service Docs\Kids Talks"
                });
                WriteConfig();
            }
            else
            {
                LibraryConfig = lastSavedConfig;
            }

            // runtime...
            Libraries = new ObservableCollection<Library>();

            foreach (var library in LibraryConfig.LibraryItems)
            {
                Libraries.Add(new Library(library));
            }
        }

        public LibraryViewModel()
        {
            ReloadLibraries();

            Debug.Print(LibraryConfig.ToString());

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

            _selectedItemPreview = this.WhenAnyValue(x => x.SelectedLibraryItem, (_SelectedItem) =>
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
            this.WhenAnyValue(x => x.SelectedLibraryItem)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe((_SelectedItem) =>
                {
                    // TODO implement this - early cancellation of BitmapLoader.LoadBitmap()
                    _cancellationTokenSource.Cancel();

                    if (_SelectedItem != null)
                    {
                        try
                        {
                            string extension = new FileInfo(_SelectedItem.FullFilePath).Extension.ToLower();
                            if (Constants.SUPPORTED_IMAGE.Contains(extension.TrimStart('.')))
                            {
                                IsPreviewLoading = true;
                                SelectedItemImagePreview = BitmapLoader.LoadBitmap(_SelectedItem.FullFilePath, 800);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Attempt to read file for library preview failed", ex);
                        }
                        finally
                        {
                            IsPreviewLoading = false;
                        }
                    }

                    SelectedItemImagePreview = null;
                });

            OnAddSelectedToPlaylistCommand = ReactiveCommand.Create(() =>
            {
                if (SelectedLibraryItem != null)
                {
                    List<string> items = new List<string>() { SelectedLibraryItem.FullFilePath };
                    MessageBus.Current.SendMessage(new AddItemByFilePathMessage(items));
                }
            });

            CreateNewSongCommand = ReactiveCommand.Create(() =>
            {
                // TODO remove dependency on Globals.MainViewModel.Playlist 
                MessageBus.Current.SendMessage(new MainWindowModalMessage(new SongEditorWindow(), false,
                    new SongEditorViewModel(new SongItemInstance(null), Globals.Instance.MainViewModel.Playlist)));
            });
        }

        private void WriteConfig()
        {
            try
            {
                if (Design.IsDesignMode) return;
                
                using (var streamWriter = new StreamWriter(Constants.LIBRARY_CONFIG_FILEPATH))
                {
                    var serializer = new SerializerBuilder()
                        // .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
                    serializer.Serialize(streamWriter, LibraryConfig);
                }
            }
            catch (Exception e)
            {
                Log.Error("Failed to write library config", e);
                throw;
            }
        }

        private LibraryConfig? LoadConfig()
        {
            try
            {
                if (Design.IsDesignMode) return null;
                
                using (var streamReader = new StreamReader(Constants.LIBRARY_CONFIG_FILEPATH))
                {
                    var deserializer = new DeserializerBuilder()
                        // .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
                    return deserializer.Deserialize<LibraryConfig>(streamReader);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load library config", ex);
                return null;
            }
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
        private readonly ObservableAsPropertyHelper<IEnumerable<LibraryItem>> _searchResults;
        public IEnumerable<LibraryItem> SearchResults => _searchResults.Value;

        // Here, we want to create a property to represent when the application 
        // is performing a search (i.e. when to show the "spinner" control that 
        // lets the user know that the app is busy). We also declare this property
        // to be the result of an Observable (i.e. its value is derived from 
        // some other property)
        private readonly ObservableAsPropertyHelper<bool> _isAvailable;
        public bool IsAvailable => _isAvailable.Value;

        private LibraryItem _selectedLibraryItem;

        public LibraryItem SelectedLibraryItem
        {
            get => _selectedLibraryItem;
            set => this.RaiseAndSetIfChanged(ref _selectedLibraryItem, value);
        }

        private async Task<IEnumerable<LibraryItem>> SearchLibrary(
            string term, CancellationToken token)
        {
            return new List<LibraryItem>();
            // if (term == null || term.Length == 0)
            //     return Items;
            //
            // // TODO: filter by file *content* as well (full-text search)
            // return Items.Where(item => item.Title.ToLower().Contains(term));
        }

        private ObservableCollection<Library> _libraries;
        public ObservableCollection<Library> Libraries { get => _libraries ; set => this.RaiseAndSetIfChanged(ref _libraries, value); }

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