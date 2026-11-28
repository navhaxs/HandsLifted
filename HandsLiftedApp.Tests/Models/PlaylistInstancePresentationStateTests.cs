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
