using HandsLiftedApp.Core.Models.Library.Config;
using Serilog;

namespace HandsLiftedApp.Core.Models.Library
{
    public class SongLibrary : Library
    {
        private readonly ISongLibrarySource _source;

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
            int count = 0;
            foreach (var entry in _source.GetEntries())
            {
                Items.Add(new LibraryItem { FullFilePath = entry.Id });
                count++;
            }
            Log.Information($"Refreshed SongLibrary [{Config.Label}] — {count} items");
        }
    }
}
