using System;
using HandsLiftedApp.Core.Models.AppState;
using Newtonsoft.Json;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Services.RemoteControl;

internal static class RemoteControlMessageHandler
{
    private class IncomingMessage
    {
        public string? Action { get; set; }
    }

    public static void Handle(string json)
    {
        var action = ParseAction(json);
        if (action == null)
        {
            return;
        }

        MessageBus.Current.SendMessage(new ActionMessage { Action = action.Value });
    }

    private static ActionMessage.NavigateSlideAction? ParseAction(string json)
    {
        IncomingMessage? message;
        try
        {
            message = JsonConvert.DeserializeObject<IncomingMessage>(json);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "RemoteControlServer: failed to parse message {Json}", json);
            return null;
        }

        return message?.Action switch
        {
            "NextSlide" => ActionMessage.NavigateSlideAction.NextSlide,
            "PreviousSlide" => ActionMessage.NavigateSlideAction.PreviousSlide,
            null => LogAndReturnNull(json, "message had no \"action\" field or was not a JSON object"),
            _ => LogAndReturnNull(json, $"unrecognized action \"{message.Action}\"")
        };
    }

    private static ActionMessage.NavigateSlideAction? LogAndReturnNull(string json, string reason)
    {
        Log.Warning("RemoteControlServer: ignoring message {Json} - {Reason}", json, reason);
        return null;
    }
}
