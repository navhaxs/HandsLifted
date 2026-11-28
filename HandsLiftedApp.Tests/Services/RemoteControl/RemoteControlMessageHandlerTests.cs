using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.Services.RemoteControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReactiveUI;

namespace HandsLiftedApp.Tests.Services.RemoteControl;

[TestClass]
public class RemoteControlMessageHandlerTests
{
    [TestMethod]
    public void Handle_NextSlideAction_PublishesActionMessage()
    {
        ActionMessage? received = null;
        using var subscription = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(msg => received = msg);

        RemoteControlMessageHandler.Handle("{\"action\":\"NextSlide\"}");

        Assert.IsNotNull(received);
        Assert.AreEqual(ActionMessage.NavigateSlideAction.NextSlide, received!.Action);
    }

    [TestMethod]
    public void Handle_PreviousSlideAction_PublishesActionMessage()
    {
        ActionMessage? received = null;
        using var subscription = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(msg => received = msg);

        RemoteControlMessageHandler.Handle("{\"action\":\"PreviousSlide\"}");

        Assert.IsNotNull(received);
        Assert.AreEqual(ActionMessage.NavigateSlideAction.PreviousSlide, received!.Action);
    }

    [TestMethod]
    [DataRow("NextSlide")]
    [DataRow("PreviousSlide")]
    public void Handle_NavigationAction_AlsoPublishesFocusSelectedItemAfterActionMessage(string action)
    {
        var order = new List<string>();
        using var actionSub = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(_ => order.Add("action"));
        using var focusSub = MessageBus.Current.Listen<FocusSelectedItem>()
            .Subscribe(_ => order.Add("focus"));

        RemoteControlMessageHandler.Handle("{\"action\":\"" + action + "\"}");

        // focus must come after navigation so the scroll targets the new selection
        CollectionAssert.AreEqual(new[] { "action", "focus" }, order);
    }

    [TestMethod]
    public void Handle_UnknownAction_DoesNotPublishFocusSelectedItem()
    {
        FocusSelectedItem? received = null;
        using var subscription = MessageBus.Current.Listen<FocusSelectedItem>()
            .Subscribe(msg => received = msg);

        RemoteControlMessageHandler.Handle("{\"action\":\"Bogus\"}");

        Assert.IsNull(received);
    }

    [TestMethod]
    [DataRow("GotoLogo")]
    [DataRow("GotoBlank")]
    [DataRow("GotoSlides")]
    public void Handle_PresentationStateAction_PublishesActionMessageWithoutFocus(string action)
    {
        // wire names are the enum member names (ActionMessage is internal, so the parameter can't be the enum)
        var expected = Enum.Parse<ActionMessage.NavigateSlideAction>(action);
        ActionMessage? received = null;
        var focusCount = 0;
        using var actionSub = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(msg => received = msg);
        using var focusSub = MessageBus.Current.Listen<FocusSelectedItem>()
            .Subscribe(_ => focusCount++);

        RemoteControlMessageHandler.Handle("{\"action\":\"" + action + "\"}");

        Assert.IsNotNull(received);
        Assert.AreEqual(expected, received!.Action);
        Assert.AreEqual(0, focusCount);
    }

    [TestMethod]
    public void Handle_UnknownAction_DoesNotPublish()
    {
        ActionMessage? received = null;
        using var subscription = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(msg => received = msg);

        RemoteControlMessageHandler.Handle("{\"action\":\"Bogus\"}");

        Assert.IsNull(received);
    }

    [TestMethod]
    public void Handle_MalformedJson_DoesNotThrowAndDoesNotPublish()
    {
        ActionMessage? received = null;
        using var subscription = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(msg => received = msg);

        RemoteControlMessageHandler.Handle("not json");

        Assert.IsNull(received);
    }

    [TestMethod]
    public void Handle_EmptyString_DoesNotThrowAndDoesNotPublish()
    {
        ActionMessage? received = null;
        using var subscription = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(msg => received = msg);

        RemoteControlMessageHandler.Handle("");

        Assert.IsNull(received);
    }
}
