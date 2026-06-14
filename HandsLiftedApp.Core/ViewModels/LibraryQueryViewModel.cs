using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels
{
    public class LibraryQueryViewModel : ReactiveObject
    {
        private ObservableAsPropertyHelper<IEnumerable<LibraryItem>> _searchResults;
        private LibraryItem _selectedLibraryItem;
        private readonly List<Library> _libraries;
        private readonly Subject<string?> _reSearchTrigger = new();

        private ObservableAsPropertyHelper<bool> _isMediaBin;
        public bool IsMediaBin
        {
            get => _isMediaBin.Value;
        }

        private string _query;
        public string Query
        {
            get => _query;
            set => this.RaiseAndSetIfChanged(ref _query, value);
        }
        
        public LibraryItem SelectedLibraryItem
        {
            get => _selectedLibraryItem;
            set => this.RaiseAndSetIfChanged(ref _selectedLibraryItem, value);
        }

        private SongItem? _selectedSongItem;
        public SongItem? SelectedSongItem
        {
            get => _selectedSongItem;
            set => this.RaiseAndSetIfChanged(ref _selectedSongItem, value);
        }

        private ObservableAsPropertyHelper<LibraryItemPreviewViewModel> _selectedItemPreview;

        public LibraryItemPreviewViewModel SelectedLibraryItemPreview
        {
            get => _selectedItemPreview.Value;
        }

        public IEnumerable<LibraryItem> SearchResults => _searchResults.Value;

        private string _searchTerm;

        public string SearchTerm
        {
            get => _searchTerm;
            set => this.RaiseAndSetIfChanged(ref _searchTerm, value);
        }

        private bool IsLyricFile(string fullFilePath)
        {
            string str = fullFilePath.ToLower();
            return str.EndsWith(".txt") || str.EndsWith(".xml");
        }

        public LibraryQueryViewModel()
        {
            var sampleItems = new List<LibraryItem>
            {
                new LibraryItem() { FullFilePath = @"C:\Songs\Amazing Grace.txt" },
                new LibraryItem() { FullFilePath = @"C:\Songs\How Great Thou Art.txt" },
                new LibraryItem() { FullFilePath = @"C:\Songs\10000 Reasons.txt" },
                new LibraryItem() { FullFilePath = @"C:\Songs\Great Is Thy Faithfulness.txt" },
                new LibraryItem() { FullFilePath = @"C:\Songs\In Christ Alone.txt" },
            };
            _searchResults = Observable.Return<IEnumerable<LibraryItem>>(sampleItems)
                .ToProperty(this, x => x.SearchResults);
            _isMediaBin = Observable.Return(false)
                .ToProperty(this, x => x.IsMediaBin);
            _selectedItemPreview = Observable.Return<LibraryItemPreviewViewModel>(null)
                .ToProperty(this, x => x.SelectedLibraryItemPreview);
            _libraries = new List<Library>();
            SelectedSongItem = new SongItem
            {
                Title = "Amazing Grace",
                Stanzas = new TrulyObservableCollection<SongStanza>
                {
                    new SongStanza(Guid.NewGuid(), "Verse 1", "Amazing grace, how sweet the sound\r\nThat saved a wretch like me\r\nI once was lost, but now am found\r\nWas blind, but now I see"),
                    new SongStanza(Guid.NewGuid(), "Chorus", "My chains are gone, I've been set free\r\nMy God, my Savior has ransomed me"),
                    new SongStanza(Guid.NewGuid(), "Verse 2", "'Twas grace that taught my heart to fear\r\nAnd grace my fears relieved"),
                }
            };
        }

        public LibraryQueryViewModel(List<Library> _libraries)
        {
            this._libraries = _libraries;

            _searchResults = Observable.Merge(
                    this.WhenAnyValue(x => x.SearchTerm)
                        .Throttle(TimeSpan.FromMilliseconds(100))
                        .Select(t => t?.Trim().ToLower())
                        .DistinctUntilChanged(),
                    _reSearchTrigger
                )
                .SelectMany(SearchLibrary)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SearchResults);

            _isMediaBin = this
                .WhenAnyValue(x => x.SearchResults)
                .Select(x => x?.All(result => !IsLyricFile(result.FullFilePath)) ?? false)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.IsMediaBin);

            _selectedItemPreview = this
                .WhenAnyValue(x => x.SelectedLibraryItem)
                .Select(x => new LibraryItemPreviewViewModel(x))
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedLibraryItemPreview);

            this.WhenAnyValue(x => x.SelectedLibraryItem)
                .Where(item => item != null)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(item => CreateItem.GenerateItem(item.FullFilePath) as SongItem)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(song => SelectedSongItem = song);

            foreach (var lib in this._libraries.OfType<SongLibrary>())
            {
                lib.WhenAnyValue(x => x.IsIndexReady)
                   .Where(ready => ready)
                   .Subscribe(_ => _reSearchTrigger.OnNext(SearchTerm?.Trim().ToLower()));
            }
        }

        private async Task<IEnumerable<LibraryItem>> SearchLibrary(
            string? term, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                var results = new List<LibraryItem>();
                foreach (var library in _libraries)
                    results.AddRange(library.Search(term));
                return (IEnumerable<LibraryItem>)results;
            }, token);
        }

    }
}