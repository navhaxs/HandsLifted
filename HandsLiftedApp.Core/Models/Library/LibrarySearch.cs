using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace HandsLiftedApp.Core.Models.Library
{
    /// <summary>
    /// The one library-search pipeline, shared by the library pane (<c>LibraryQueryViewModel</c>) and the
    /// Add Item dialog (<c>ResultsViewModel</c>). The matching itself lives on <see cref="Library.Search"/>
    /// (a <see cref="SongLibrary"/> overrides it with fuzzy title/copyright/lyrics scoring).
    /// </summary>
    public static class LibrarySearch
    {
        private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(100);

        /// <summary>Runs <paramref name="term"/> against every library and concatenates the results.</summary>
        public static IEnumerable<LibraryItem> Run(IEnumerable<Library> libraries, string? term)
        {
            var results = new List<LibraryItem>();
            foreach (var library in libraries)
                results.AddRange(library.Search(term));
            return results;
        }

        /// <summary>
        /// Turns a stream of search-box text into a stream of results. Typing is debounced, a newer term
        /// cancels a stale in-flight search, and the search runs again whenever a song library's index
        /// becomes ready or is rebuilt (until then a song library can only match on title). Emits on a
        /// background thread: observe on the main thread before touching UI state.
        /// </summary>
        public static IObservable<IEnumerable<LibraryItem>> Observe(
            IObservable<string?> searchTerm, IReadOnlyCollection<Library> libraries)
        {
            var terms = searchTerm
                .Throttle(Debounce)
                .Select(t => t?.Trim().ToLower())
                .DistinctUntilChanged();

            var indexStates = libraries.OfType<SongLibrary>()
                .Select(l => l.WhenAnyValue(x => x.IsIndexReady))
                .ToList();
            var indexChanges = indexStates.Count == 0
                ? Observable.Return(0)
                : indexStates.CombineLatest().Select(flags => flags.Count(ready => ready));

            return terms
                .CombineLatest(indexChanges, (term, _) => term)
                .Select(term => Observable.FromAsync(ct => Task.Run(() => Run(libraries, term), ct)))
                .Switch();
        }
    }
}
