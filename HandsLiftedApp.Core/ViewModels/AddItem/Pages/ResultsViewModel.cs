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
        private readonly ObservableAsPropertyHelper<IEnumerable<LibraryItem>> _searchResults;
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
            _searchResults = this
                .WhenAnyValue(x => x.SearchTerm)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Select(term => term?.Trim().ToLower())
                .DistinctUntilChanged()
                // .Where(term => !string.IsNullOrWhiteSpace(term))
                .SelectMany(SearchLibrary)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SearchResults);

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
                var song = new SongItemInstance(Globals.Instance.MainViewModel.Playlist);
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
                    new SongEditorViewModel(song, Globals.Instance.MainViewModel.Playlist) { ItemInsertIndex = addItemViewModel.ItemInsertIndex }));

                window.Close();
            });
        }

        public IEnumerable<LibraryItem> SearchResults => _searchResults.Value;

        private string _searchTerm;

        public string SearchTerm
        {
            get => _searchTerm;
            set => this.RaiseAndSetIfChanged(ref _searchTerm, value);
        }

        private async Task<IEnumerable<LibraryItem>> SearchLibrary(
            string? term, CancellationToken token)
        {
            var items = _library?.Items?.OrderBy(a => a?.Title);
            if (string.IsNullOrEmpty(term))
                return items;

            // TODO: filter by file *content* as well (full-text search)
            return items.Where(item => item.Title.ToLower().Contains(term));
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