using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.UiTests.Support;

namespace HandsLiftedApp.UiTests;

[TestClass]
public class MessageWindowTests
{
    // The "libmpv missing" warning is several lines long: the dialog must grow to fit it, not clip it.
    [TestMethod]
    public async Task MessageWindow_Grows_To_Fit_A_Long_Message()
    {
        await HeadlessSession.Run(() =>
        {
            var window = new HandsLiftedApp.Views.App.MessageWindow
            {
                DataContext = new MessageWindowViewModel
                {
                    Title = "Video playback unavailable",
                    Content = "The video library (libmpv) could not be loaded from:\nC:\\Opt\\VisionScreensAppRolling\\\n\n" +
                              "Videos and motion backgrounds will not play. Copy libmpv-2.dll next to the app, or reinstall it."
                }
            };
            window.Show();
            UiTestHelpers.Settle();

            Assert.IsTrue(window.Bounds.Height > 140, $"dialog did not grow for long content (height {window.Bounds.Height})");
            window.Screenshot("message-window-long");
            window.Close();
        });
    }
}
