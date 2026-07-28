using System.IO;
using System.Linq;
using HandsLiftedApp.Comparer;
using HandsLiftedApp.Core.Models.Library.Config;
using Serilog;

namespace HandsLiftedApp.Core.Models.Library
{
    // No fuzzy-search index (unlike SongLibrary) — scripture items don't have
    // free-text lyric content to search; the base Library.Search's title
    // substring match is sufficient for a first pass.
    public class ScriptureLibrary : Library
    {
        public ScriptureLibrary(LibraryConfig.LibraryDefinition config) : base(config, ConstructorMode.SkipRefresh)
        {
            isMediaBin = false;
            Refresh();
        }

        protected override void Refresh()
        {
            Items.Clear();

            if (!Directory.Exists(Config.Directory))
            {
                Log.Error("ScriptureLibrary [{Label}] fail - directory [{Directory}] does not exist", Config.Label, Config.Directory);
                return;
            }

            var files = new DirectoryInfo(Config.Directory)
                .GetFiles("*.xml", SearchOption.TopDirectoryOnly)
                .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(f => f.FullName, new NaturalSortStringComparer(System.StringComparison.Ordinal));

            foreach (var f in files)
            {
                Items.Add(new LibraryItem { FullFilePath = f.FullName });
            }

            Log.Information("Refreshed ScriptureLibrary [{Label}] — {Count} items", Config.Label, Items.Count);
        }
    }
}
