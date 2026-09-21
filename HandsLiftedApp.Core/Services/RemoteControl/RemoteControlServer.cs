using System;
using System.Text;
using Serilog;
using WatsonWebsocket;

namespace HandsLiftedApp.Core.Services.RemoteControl;

public class RemoteControlServer : IDisposable
{
    public const int Port = 8979;

    private readonly WatsonWsServer _server;

    public RemoteControlServer()
    {
        _server = new WatsonWsServer("127.0.0.1", Port);
        _server.MessageReceived += OnMessageReceived;
    }

    public void Start()
    {
        try
        {
            _server.Start();
            Log.Information("RemoteControlServer listening on ws://127.0.0.1:{Port}/", Port);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "RemoteControlServer failed to bind to port {Port} - remote control will be unavailable this session", Port);
        }
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        try
        {
            var json = Encoding.UTF8.GetString(e.Data);
            RemoteControlMessageHandler.Handle(json);
        }
        catch (Exception ex)
        {
            // This handler runs on WatsonWsServer's own background thread with nothing
            // above it to catch a fault - an uncaught exception here would take down that
            // thread (and, depending on the library's internals, possibly the server)
            // instead of just failing to process one bad message.
            Log.Error(ex, "RemoteControlServer: unhandled error processing incoming message");
        }
    }

    public void Dispose()
    {
        _server.MessageReceived -= OnMessageReceived;
        _server.Dispose();
    }
}
