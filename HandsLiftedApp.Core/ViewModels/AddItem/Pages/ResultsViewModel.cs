using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.ViewModels.AddItem.Pages
{
    public class ResultsViewModel : AddItemPageViewModel
    {
        private readonly ObservableAsPropertyHelper<IEnumerable<LibraryItem>?> _searchResults;
        private readonly ObservableAsPropertyHelper<bool> _isSearchResultsEmpty;
        private readonly Library _library;

        public ReactiveCommand<Window, Unit> OnCreateNewSongCommand { get; }

        // TODO this could be a list
        public Library Library
        {
            get => _library;
        }

        public ResultsViewModel(AddItemViewModel addItemViewModel, Library _library) : base(addItemViewModel)
        {
            this._library = _library;
            // constructor
            // Same pipeline as the library pane: Library.Search does the matching (fuzzy title/lyrics for songs).
            _searchResults = LibrarySearch
                .Observe(this.WhenAnyValue(x => x.SearchTerm),
                    _library == null ? Array.Empty<Library>() : new[] { _library })
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.SearchResults);

            _isSearchResultsEmpty = this
                .WhenAnyValue(x => x.SearchResults)
                .Select(results => results == null || !results.Any())
                .ToProperty(this, x => x.IsSearchResultsEmpty);

            _selectedItemPreview = this.WhenAnyValue(x => x.SelectedLibraryItem, (_SelectedItem) =>
                    {
                        if (Design.IsDesignMode)
                        {
                            return "Design Mode";
                        }
                        
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
            
            OnCreateNewSongCommand = ReactiveCommand.Create<Window>(window =>
            {
                var song = SongItemInstance.NewDraft(Globals.Instance.MainViewModel.Playlist);
                // itemToInsert = song;
                // SongEditorViewModel vm = new SongEditorViewModel(song, Playlist);
                // SongEditorWindow seq = new SongEditorWindow() { DataContext = vm };
                // seq.Show();
                // TODO
                // TODO
                // TODO
                // TODO
                // TODO
                // TODO
                // TODO  add ITEM into PLAYLIST
                // MessageBus.Current.SendMessage(new AddItemByFilePathMessage(items, vm.AddItemViewModel.ItemInsertIndex));

                MessageBus.Current.SendMessage(new MainWindowModalMessage(new SongEditorWindow(), false,
                    new SongEditorViewModel(song, Globals.Instance.MainViewModel.Playlist)
                    {
                        ItemInsertIndex = addItemViewModel.ItemInsertIndex,
                        SongLibrary = _library as SongLibrary
                    }));

                window.Close();
            });
        }

        public IEnumerable<LibraryItem> SearchResults => _searchResults.Value;

        public bool IsSearchResultsEmpty => _isSearchResultsEmpty.Value;

        private string _searchTerm;

        public string SearchTerm
        {
            get => _searchTerm;
            set => this.RaiseAndSetIfChanged(ref _searchTerm, value);
        }

        private LibraryItem _selectedLibraryItem;

        public LibraryItem SelectedLibraryItem
        {
            get => _selectedLibraryItem;
            set => this.RaiseAndSetIfChanged(ref _selectedLibraryItem, value);
        }

        private ObservableAsPropertyHelper<string> _selectedItemPreview;

        public string SelectedItemPreview
        {
            get => _selectedItemPreview.Value;
        }
    }
}