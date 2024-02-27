using HandsLiftedApp.Core.Models;
using HandsLiftedApp.ViewModels.Editor;

namespace HandsLiftedApp.Core.ViewModels.Editor
{
    public class ExampleSongEditorViewModel()
        : SongEditorViewModel(new ExampleSongItemInstance(), new PlaylistInstance());
}