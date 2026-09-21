using System;
using System.Text;
using Avalonia.Threading;
using Serilog;
using WatsonWebsocket;

namespace HandsLiftedApp.Core.Services.RemoteControl;

public class RemoteControlServer : IDisposable
{
    public const int Port = 8979;

    private readonly WatsonWsServer _server;
    private bool _disposed;

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
        // WatsonWsServer raises this event on its own thread-pool thread, not the UI thread.
        // RemoteControlMessageHandler.Handle() drives the same navigation pipeline as
        // keyboard shortcuts and button clicks, which all run on the UI thread - so post the
        // actual handling onto the UI thread via the dispatcher rather than running it here.
        // Dispatcher.UIThread.Post is fire-and-forget: it does not block this socket thread
        // waiting for the UI thread to process the message.
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(e.Data);
                RemoteControlMessageHandler.Handle(json);
            }
            catch (Exception ex)
            {
                // Nothing above this catches a fault on the dispatcher either - an uncaught
                // exception here would escape onto the UI thread instead of just failing to
                // process one bad message.
                Log.Error(ex, "RemoteControlServer: unhandled error processing incoming message");
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _server.MessageReceived -= OnMessageReceived;
        _server.Dispose();
    }
}
