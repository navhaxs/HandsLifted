using HandsLiftedApp.ViewModels.Editor;
using HandsLiftedApp.Views.Editor;

namespace HandsLiftedApp
{
    internal static class TestModeApp
    {
        public static void RunSongEditorWindow()
        {
            var song = new ExampleSongViewModel();
            SongEditorViewModel vm = new SongEditorViewModel() { song = song };
            SongEditorWindow seq = new SongEditorWindow() { DataContext = vm };
            seq.Show();
        }
    }
}
