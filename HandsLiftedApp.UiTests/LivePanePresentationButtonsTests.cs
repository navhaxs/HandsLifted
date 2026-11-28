using Avalonia.Controls;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.UiTests.Support;
using ReactiveUI;

namespace HandsLiftedApp.UiTests;

/// <summary>
/// The Logo / Blank buttons in the live pane are radio buttons bound two-way to Playlist.IsLogo / IsBlank. A state change
/// that starts elsewhere (the remote-control WebSocket, F1/F12) arrives as an ActionMessage and must update them.
/// </summary>
[TestClass]
public class LivePanePresentationButtonsTests
{
    private static void Send(ActionMessage.NavigateSlideAction action) =>
        MessageBus.Current.SendMessage(new ActionMessage { Action = action });

    [TestMethod]
    public async Task Logo_And_Blank_Buttons_Follow_Actions_Sent_Over_The_Message_Bus()
    {
        await HeadlessSession.Run(() =>
        {
            var window = UiTestHelpers.OpenMainWindow();
            var playlist = ((MainViewModel)window.DataContext!).Playlist;
            var logo = window.FindByText<RadioButton>("Logo")!;
            var blank = window.FindByText<RadioButton>("Blank")!;
            Assert.IsNotNull(logo, "Logo button not found");
            Assert.IsNotNull(blank, "Blank button not found");
            Assert.IsFalse(logo.IsChecked == true);
            Assert.IsFalse(blank.IsChecked == true);

            Send(ActionMessage.NavigateSlideAction.GotoBlank);
            UiTestHelpers.Settle();
            Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Blank, playlist.PresentationState);
            Assert.IsTrue(blank.IsChecked == true, "Blank button not checked after GotoBlank");
            Assert.IsFalse(logo.IsChecked == true, "Logo button still checked after GotoBlank");

            Send(ActionMessage.NavigateSlideAction.GotoLogo);
            UiTestHelpers.Settle();
            Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Logo, playlist.PresentationState,
                "un-checking the Blank radio must not knock the playlist out of Logo");
            Assert.IsTrue(logo.IsChecked == true, "Logo button not checked after GotoLogo");
            Assert.IsFalse(blank.IsChecked == true, "Blank button still checked after GotoLogo");
            window.Screenshot("live-pane-logo-checked");

            Send(ActionMessage.NavigateSlideAction.GotoSlides);
            UiTestHelpers.Settle();
            Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Slides, playlist.PresentationState);
            Assert.IsFalse(logo.IsChecked == true, "Logo button still checked after GotoSlides");
            Assert.IsFalse(blank.IsChecked == true, "Blank button still checked after GotoSlides");

            window.Close();
        });
    }

    [TestMethod]
    public async Task Clicking_The_Buttons_Still_Switches_State()
    {
        await HeadlessSession.Run(() =>
        {
            var window = UiTestHelpers.OpenMainWindow();
            var playlist = ((MainViewModel)window.DataContext!).Playlist;
            var logo = window.FindByText<RadioButton>("Logo")!;
            var blank = window.FindByText<RadioButton>("Blank")!;

            window.Click(logo);
            Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Logo, playlist.PresentationState);

            window.Click(blank);
            Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Blank, playlist.PresentationState,
                "checking Blank after Logo must end in Blank, not Slides");
            Assert.IsTrue(blank.IsChecked == true);
            Assert.IsFalse(logo.IsChecked == true);

            window.Close();
        });
    }
}
