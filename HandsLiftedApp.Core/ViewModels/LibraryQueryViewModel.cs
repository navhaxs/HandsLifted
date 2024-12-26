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
        private readonly Library _library;

        private string _query;
        public string Query
        {
            get => _query;
            set => this.RaiseAndSetIfChanged(ref _query, value);
        }
        
        public IEnumerable<LibraryItem> SearchResults => _searchResults.Value;

        private string _searchTerm;

        public string SearchTerm
        {
            get => _searchTerm;
            set => this.RaiseAndSetIfChanged(ref _searchTerm, value);
        }

        public bool IsMediaBin => true;

        public LibraryQueryViewModel()
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

    }
}