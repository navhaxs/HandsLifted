using System.Net;
using System.Text;
using System.Text.Json;
using Serilog;

namespace HandsLiftedApp.DevTools;

/// <summary>
/// Opt-in loopback HTTP front-end for <see cref="DevToolsApi"/>. Off unless HANDSLIFTED_DEVTOOLS_PORT is set.
/// Safety: 127.0.0.1 only; requests carrying an Origin header (i.e. from a web page) are refused; mutating
/// endpoints need POST plus a custom <c>X-DevTools</c> header, which a browser cannot send cross-origin without a
/// preflight we never approve. The project is only referenced by Debug builds of the Desktop app.
/// </summary>
public static class DevToolsHost
{
    public const string PortEnvVar = "HANDSLIFTED_DEVTOOLS_PORT";
    private static HttpListener? _listener;

    /// <summary>Call once after Avalonia is set up (e.g. AppBuilder.AfterSetup). No-op unless the env var is set.</summary>
    public static void StartIfEnabled()
    {
        var raw = Environment.GetEnvironmentVariable(PortEnvVar);
        if (string.IsNullOrWhiteSpace(raw)) return;
        if (!int.TryParse(raw, out var port) || port is < 1024 or > 65535)
        {
            Log.Warning("DevTools: {Var}='{Value}' is not a usable port (1024-65535); not starting", PortEnvVar, raw);
            return;
        }
        Start(port);
    }

    public static void Start(int port)
    {
        if (_listener != null) return;
        try
        {
            LogBuffer.Install();

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();

            var info = JsonSerializer.Serialize(new { port, pid = Environment.ProcessId, started = DateTime.Now });
            File.WriteAllText(Path.Combine(Capture.OutputDir, "devtools.json"), info);

            Log.Information("DevTools listening on http://127.0.0.1:{Port}/ (Debug only; GET / for help)", port);
            _ = Task.Run(AcceptLoop);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DevTools failed to start on port {Port}; continuing without it", port);
            _listener = null;
        }
    }

    public static void Stop()
    {
        try { _listener?.Stop(); } catch { /* shutting down */ }
        _listener = null;
    }

    private static async Task AcceptLoop()
    {
        while (_listener is { IsListening: true } listener)
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync(); }
            catch { return; }
            _ = Task.Run(() => Serve(ctx));
        }
    }

    private static async Task Serve(HttpListenerContext ctx)
    {
        DevResponse response;
        try
        {
            response = await Dispatch(ctx.Request);
        }
        catch (Exception ex)
        {
            response = DevResponse.Error(500, ex.Message);
        }

        try
        {
            ctx.Response.StatusCode = response.Status;
            ctx.Response.ContentType = response.ContentType;
            ctx.Response.ContentLength64 = response.Body.Length;
            await ctx.Response.OutputStream.WriteAsync(response.Body);
        }
        catch { /* client went away */ }
        finally
        {
            try { ctx.Response.Close(); } catch { /* ignore */ }
        }
    }

    private static async Task<DevResponse> Dispatch(HttpListenerRequest req)
    {
        // A browser page (any origin) must never be able to drive the app.
        if (req.Headers["Origin"] != null)
            return DevResponse.Error(403, "cross-origin requests are refused");

        var isPost = req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase);
        if (isPost && req.Headers["X-DevTools"] != "1")
            return DevResponse.Error(403, "POST requires header 'X-DevTools: 1'");

        string body = "";
        if (req.HasEntityBody)
        {
            using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
            body = await reader.ReadToEndAsync();
        }

        return await DevToolsApi.Handle(new DevRequest
        {
            Method = req.HttpMethod,
            Path = req.Url?.AbsolutePath ?? "/",
            Query = System.Web.HttpUtility.ParseQueryString(req.Url?.Query ?? ""),
            Body = body,
        });
    }
}
