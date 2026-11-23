using Avalonia.Threading;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.UiTests.Support;

namespace HandsLiftedApp.UiTests;

[TestClass]
public class PlaylistReplacementTests
{
    // Regression: "New playlist" assigns a brand-new PlaylistInstance to MainViewModel.Playlist.
    // LivePane subscribed to the *old* instance's ActiveSlide only, so after File > New the live
    // preview (and projector / stage display) never followed slide changes again.
    [TestMethod]
    public async Task LivePane_Follows_ActiveSlide_Of_A_Replaced_Playlist()
    {
        await HeadlessSession.Run(() =>
        {
            var window = UiTestHelpers.OpenMainWindow();
            var vm = (MainViewModel)window.DataContext!;
            var canvas = window.Find<SlideCanvas>(c => c.Name == "LivePreviewCanvas");
            Assert.IsNotNull(canvas, "LivePreviewCanvas not found");

            // what "new playlist" does (MainWindow.HandleNewPlaylist)
            vm.Playlist = new PlaylistInstance();
            UiTestHelpers.Settle();

            // Logo state yields a LogoSlide whose spec is non-null (default logo); a blank slide yields null.
            vm.Playlist.PresentationState = PlaylistInstance.PresentationStateEnum.Logo;

            for (var i = 0; i < 50 && canvas.Spec == null; i++)
            {
                UiTestHelpers.Settle();
                Thread.Sleep(20);
            }

            Assert.IsNotNull(canvas.Spec, "live preview did not follow the replaced playlist's ActiveSlide");
            window.Close();
        });
    }
}
