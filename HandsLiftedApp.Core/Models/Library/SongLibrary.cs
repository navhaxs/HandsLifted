using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Models.Library
{
    public class SongLibrary : Library
    {
        private readonly ISongLibrarySource _source;
        private volatile Dictionary<string, SongIndexEntry>? _index;
        private readonly Dictionary<string, LibraryItem> _itemsByPath = new();

        private bool _isIndexReady;
        public bool IsIndexReady
        {
            get => _isIndexReady;
            private set => this.RaiseAndSetIfChanged(ref _isIndexReady, value);
        }

        public SongLibrary(LibraryConfig.LibraryDefinition config, ISongLibrarySource source)
            : base(config, ConstructorMode.SkipRefresh)
        {
            _source = source;
            isMediaBin = false;
            Refresh();
        }

        protected override void Refresh()
        {
            Items.Clear();
            _itemsByPath.Clear();
            IsIndexReady = false;
            _index = null;

            var paths = new List<string>();
            foreach (var entry in _source.GetEntries())
            {
                var item = new LibraryItem { FullFilePath = entry.Id };
                Items.Add(item);
                _itemsByPath[entry.Id] = item;
                paths.Add(entry.Id);
            }
            Log.Information("Refreshed SongLibrary [{Label}] — {Count} items", Config.Label, paths.Count);
            _ = BuildIndexAsync(paths);
        }

        private async Task BuildIndexAsync(List<string> paths)
        {
            await Task.Run(() =>
            {
                var idx = new Dictionary<string, SongIndexEntry>(paths.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var path in paths)
                {
                    try
                    {
                        var song = CreateItem.GenerateItem(path) as SongItem;
                        if (song == null) continue;
                        idx[path] = new SongIndexEntry(
                            FilePath:  path,
                            Title:     song.Title ?? Path.GetFileName(path),
                            Copyright: song.Copyright ?? "",
                            LyricText: string.Join(" ", song.Stanzas.Select(s => s.Lyrics ?? ""))
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "SongLibrary: failed to index {Path}", path);
                    }
                }
                _index = idx;
            });
            await Dispatcher.UIThread.InvokeAsync(() => IsIndexReady = true);
            Log.Information("SongLibrary [{Label}] index ready — {Count} entries", Config.Label, _index?.Count ?? 0);
        }

        public override IEnumerable<LibraryItem> Search(string? term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Items.OrderBy(i => i.Title);

            var tokens = term.Trim().ToLowerInvariant()
                             .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var idx = _index;

            if (idx == null)
            {
                // Index not ready yet — fall back to title substring
                return Items.Where(i => i.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(i => i.Title);
            }

            return idx.Values
                .Select(e => (e, score: SongFuzzyMatcher.Score(tokens, e)))
                .Where(t => t.score > 0)
                .OrderByDescending(t => t.score)
                .Select(t => _itemsByPath.TryGetValue(t.e.FilePath, out var item) ? item : null)
                .Where(item => item != null)!;
        }
    }
}
