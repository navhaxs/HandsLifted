using System.Threading.Tasks;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReactiveUI;

namespace HandsLiftedApp.Tests.Models;

[TestClass]
public class PlaylistInstancePresentationStateTests
{
    [TestInitialize]
    public void Setup()
    {
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    private static void Send(ActionMessage.NavigateSlideAction action) =>
        MessageBus.Current.SendMessage(new ActionMessage { Action = action });

    [TestMethod]
    public Task GotoBlankLogoSlides_MoveThroughPresentationStates() => DispatcherTestThread.Run(() =>
    {
        var playlist = new PlaylistInstance();
        Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Slides, playlist.PresentationState);

        Send(ActionMessage.NavigateSlideAction.GotoBlank);
        Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Blank, playlist.PresentationState);

        Send(ActionMessage.NavigateSlideAction.GotoLogo);
        Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Logo, playlist.PresentationState);

        Send(ActionMessage.NavigateSlideAction.GotoSlides);
        Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Slides, playlist.PresentationState);

        return Task.CompletedTask;
    });

    // The Logo / Blank RadioButtons in LivePane bind IsChecked to IsLogo / IsBlank, so a state change that comes from
    // the message bus (WebSocket, F1/F12) must raise those properties, not just PresentationState.
    [TestMethod]
    public Task GotoBlankAndLogo_RaiseIsBlankAndIsLogoChanged() => DispatcherTestThread.Run(() =>
    {
        var playlist = new PlaylistInstance();
        var raised = new System.Collections.Generic.List<string>();
        playlist.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        Send(ActionMessage.NavigateSlideAction.GotoBlank);
        CollectionAssert.Contains(raised, nameof(PlaylistInstance.IsBlank));
        Assert.IsTrue(playlist.IsBlank);
        Assert.IsFalse(playlist.IsLogo);

        raised.Clear();
        Send(ActionMessage.NavigateSlideAction.GotoLogo);
        CollectionAssert.Contains(raised, nameof(PlaylistInstance.IsLogo));
        CollectionAssert.Contains(raised, nameof(PlaylistInstance.IsBlank)); // Blank radio must un-check
        Assert.IsTrue(playlist.IsLogo);
        Assert.IsFalse(playlist.IsBlank);

        raised.Clear();
        Send(ActionMessage.NavigateSlideAction.GotoSlides);
        CollectionAssert.Contains(raised, nameof(PlaylistInstance.IsLogo));
        Assert.IsFalse(playlist.IsLogo);
        Assert.IsFalse(playlist.IsBlank);

        return Task.CompletedTask;
    });

    // A radio button that is un-checked because its sibling was checked writes false back to its binding. That must
    // not knock the playlist out of the state the sibling just set.
    [TestMethod]
    public Task SettingIsLogoFalse_WhileBlank_KeepsBlank() => DispatcherTestThread.Run(() =>
    {
        var playlist = new PlaylistInstance();
        Send(ActionMessage.NavigateSlideAction.GotoBlank);

        playlist.IsLogo = false;

        Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Blank, playlist.PresentationState);

        playlist.IsBlank = false;
        Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Slides, playlist.PresentationState);

        playlist.IsLogo = true;
        playlist.IsBlank = false; // Blank radio un-checked after Logo was checked
        Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Logo, playlist.PresentationState);

        return Task.CompletedTask;
    });

    [TestMethod]
    public Task GotoSlides_ClearsQuickShow() => DispatcherTestThread.Run(() =>
    {
        var playlist = new PlaylistInstance { PresentationState = PlaylistInstance.PresentationStateEnum.QuickShow };

        Send(ActionMessage.NavigateSlideAction.GotoSlides);

        Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Slides, playlist.PresentationState);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task GotoSlides_WhenAlreadyShowingSlides_IsANoOp() => DispatcherTestThread.Run(() =>
    {
        var playlist = new PlaylistInstance();

        Send(ActionMessage.NavigateSlideAction.GotoSlides);

        Assert.AreEqual(PlaylistInstance.PresentationStateEnum.Slides, playlist.PresentationState);
        return Task.CompletedTask;
    });
}
