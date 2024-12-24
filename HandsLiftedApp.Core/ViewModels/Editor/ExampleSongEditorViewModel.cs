using HandsLiftedApp.Core.Models;
using HandsLiftedApp.ViewModels.Editor;

namespace HandsLiftedApp.Core.ViewModels.Editor
{
    public class ExampleSongEditorViewModel
        : SongEditorViewModel
    {
        public ExampleSongEditorViewModel() : base(new ExampleSongItemInstance(), new PlaylistInstance())
        {
            // LyricEntryMode = true;
        }
    }
}