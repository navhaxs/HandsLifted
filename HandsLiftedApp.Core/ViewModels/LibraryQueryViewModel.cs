using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Models.Library;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels
{
    public class LibraryQueryViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<IEnumerable<LibraryItem>> _searchResults;
        private LibraryItem _selectedLibraryItem;
        private readonly List<Library> _libraries;
        
        private readonly ObservableAsPropertyHelper<bool> _isMediaBin;
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
        private readonly ObservableAsPropertyHelper<LibraryItemPreviewViewModel> _selectedItemPreview;

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

        public LibraryQueryViewModel(List<Library> _libraries)
        {
            _searchResults = this
                .WhenAnyValue(x => x.SearchTerm)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Select(term => term?.Trim().ToLower())
                .DistinctUntilChanged()
                // .Where(term => !string.IsNullOrWhiteSpace(term))
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
            
            this._libraries = _libraries;
        }
        
        private async Task<IEnumerable<LibraryItem>> SearchLibrary(
            string? term, CancellationToken token)
        {
            List<LibraryItem>? items = new();
            foreach (var library in _libraries)
            {
                items.AddRange(library.Items?.OrderBy(a => a?.Title));
            }
            
            if (string.IsNullOrEmpty(term))
                return items;
       
            // TODO: filter by file *content* as well (full-text search)
            return items.Where(item => item.Title.ToLower().Contains(term));
        }

    }
}