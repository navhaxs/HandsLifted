using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HandsLiftedApp.Core;

namespace HandsLiftedApp.DevTools;

/// <summary>
/// The devtools endpoints, independent of any transport. <see cref="Handle"/> is what the HTTP server calls and what
/// headless tests call directly. Every touch of the visual tree is marshalled onto the UI thread.
/// </summary>
public static class DevToolsApi
{
    private sealed record Route(bool Mutating, string Help, Func<DevRequest, Task<DevResponse>> Handler);

    private static readonly Dictionary<string, Route> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/"] = new(false, "this help", _ => Task.FromResult(Help())),
        ["/windows"] = new(false, "windows and open popups (flyouts, menus, dropdowns) with node ids", Windows),
        ["/tree"] = new(false, "visual tree. window=|node= depth=10 format=text|json all=1 (include layout containers) hidden=1", Tree),
        ["/find"] = new(false, "search nodes. text= type= name= aid= window= contains=1 hidden=1 limit=50 wait=<ms> (poll until >=1 match)", Find),
        ["/props"] = new(false, "Avalonia properties of a node (node= or selector). all=1 for unset ones too", Props),
        ["/a11y"] = new(false, "accessibility-API view of a node and its ancestors: peer, role, name, patterns (invoke/toggle/...)", A11y),
        ["/dc"] = new(false, "dump a node's DataContext. node=|selector, path=Some.Path[0], depth=1", DataContextDump),
        ["/vm"] = new(false, "dump Globals.Instance by path, e.g. path=MainViewModel.Playlist.UndoStack depth=2", ViewModelDump),
        ["/state"] = new(false, "curated app state: playlist, selected item, active slide, undo stack, windows", State),
        ["/logs"] = new(false, "recent logs. n=100 level=Warning contains= clear=1 (clear needs POST)", Logs),
        ["/screenshot"] = new(false, "PNG of a window/popup/node. window=|node=|selector scale=1 raw=1 (default: saves file, returns its path)", Screenshot),
        ["/click"] = new(true, "click/activate. node=|selector, mode=invoke (accessibility API, default)|mouse (real OS mouse), button=left|right double=1 settle=150", Click),
        ["/hover"] = new(true, "move the real mouse over a node (mode=mouse only)", Hover),
        ["/scroll"] = new(true, "real mouse wheel over a node. dy=-3 (negative=down)", Scroll),
        ["/type"] = new(true, "type text into a node (or the focused control). text= (or request body)", TypeText),
        ["/key"] = new(true, "press a key. key=Enter mods=ctrl+shift node= (default: focused control)", Key),
        ["/focus"] = new(true, "focus a node", Focus),
        ["/prop"] = new(true, "set an Avalonia property. node=|selector name=IsChecked value=true", SetProp),
    };

    public static async Task<DevResponse> Handle(DevRequest request)
    {
        if (!Routes.TryGetValue(request.Path.TrimEnd('/') is { Length: > 0 } p ? p : "/", out var route))
            return DevResponse.Error(404, "unknown endpoint " + request.Path, "GET / lists endpoints");
        if (route.Mutating && !request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            return DevResponse.Error(405, request.Path + " changes the app: use POST");

        try
        {
            return await route.Handler(request);
        }
        catch (DevToolsException ex)
        {
            return DevResponse.Error(ex.Status, ex.Message, ex.Hint);
        }
        catch (Exception ex)
        {
            return DevResponse.Error(500, ex.GetType().Name + ": " + ex.Message, ex.StackTrace?.Split('\n').Take(6));
        }
    }

    // ---- plumbing ---------------------------------------------------------------------------------------------

    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(8);

    public static async Task<T> OnUi<T>(Func<T> work)
    {
        var op = Dispatcher.UIThread.InvokeAsync(work);
        var task = op.GetTask();
        if (await Task.WhenAny(task, Task.Delay(UiTimeout)) != task)
            throw new DevToolsException($"UI thread did not respond within {UiTimeout.TotalSeconds:0}s (blocked or busy)", 504);
        return await task;
    }

    private static Task OnUi(Action work) => OnUi<object?>(() => { work(); return null; });

    private static async Task Settle(DevRequest r) => await Task.Delay(Math.Clamp(r.GetInt("settle", 150), 0, 5000));

    private static DevResponse Json(DevRequest r, object? value) => DevResponse.Json(value, r.Flag("pretty"));

    // ---- read endpoints ---------------------------------------------------------------------------------------

    private static DevResponse Help() => DevResponse.Text(
        "VisionScreens devtools (Debug builds only). GET reads; POST mutates and needs header 'X-DevTools: 1'.\n\n" +
        string.Join("\n", Routes.OrderBy(k => k.Key).Select(k => $"{(k.Value.Mutating ? "POST" : "GET ")} {k.Key,-12} {k.Value.Help}")) +
        "\n\nSelector params (click/type/key/focus/prop/props/dc/screenshot): node=ID | text= type= name= aid= window= index=N contains=1 hidden=1\n" +
        "Flyout/menu content is a separate popup surface: it shows up in /windows and is searched automatically.\n");

    private static Task<DevResponse> Windows(DevRequest r) => OnUi(() =>
    {
        var rows = Surfaces.All().Select(tl =>
        {
            var d = NodeInfo.Describe(tl);
            d["kind"] = Surfaces.KindOf(tl);
            if (tl is Window w)
            {
                d["title"] = w.Title;
                d["active"] = w.IsActive;
                d["state"] = w.WindowState.ToString();
                d["position"] = new[] { w.Position.X, w.Position.Y };
            }
            d["size"] = new[] { (int)tl.ClientSize.Width, (int)tl.ClientSize.Height };
            return d;
        }).ToList();
        return Json(r, rows);
    });

    private static Task<DevResponse> Tree(DevRequest r) => OnUi(() =>
    {
        Visual root = r.Get("node") != null || Selector.HasCriteria(r)
            ? Selector.Resolve(r)
            : Surfaces.Match(r.Get("window")) ?? throw new DevToolsException("no windows open yet", 404, "the app may still be starting; retry, or GET /state");

        var (roots, count, truncated) = TreeDump.Build(root, new TreeOptions
        {
            MaxDepth = r.GetInt("depth", 10),
            IncludeHidden = r.Flag("hidden"),
            Everything = r.Flag("all"),
            MaxNodes = r.GetInt("max", 1500),
        });

        if ((r.Get("format") ?? "text") == "json")
            return Json(r, new { count, truncated, tree = roots.Select(TreeDump.ToJson).ToList() });

        var text = TreeDump.ToText(roots);
        if (truncated) text += $"... truncated at {count} nodes (raise max=, narrow with node=/window=, or lower depth=)\n";
        return DevResponse.Text(text);
    });

    private static async Task<DevResponse> Find(DevRequest r)
    {
        var wait = Math.Clamp(r.GetInt("wait", 0), 0, 30000);
        var limit = r.GetInt("limit", 50);
        var deadline = DateTime.UtcNow.AddMilliseconds(wait);

        while (true)
        {
            var rows = await OnUi(() => Selector.FindAll(r).Take(limit).Select(NodeInfo.Describe).ToList());
            if (rows.Count > 0 || DateTime.UtcNow >= deadline)
                return Json(r, new { count = rows.Count, matches = rows });
            await Task.Delay(50);
        }
    }

    private static Task<DevResponse> Props(DevRequest r) => OnUi(() =>
    {
        var v = Selector.Resolve(r);
        return Json(r, new { node = NodeInfo.Describe(v), properties = PropDump.Of((AvaloniaObject)v, r.Flag("all")) });
    });

    private static Task<DevResponse> A11y(DevRequest r) => OnUi(() => Json(r, Input.A11yChain(Selector.Resolve(r))));

    private static Task<DevResponse> DataContextDump(DevRequest r) => OnUi(() =>
    {
        var v = Selector.Resolve(r);
        var dc = (v as StyledElement)?.DataContext ?? throw new DevToolsException($"{v.GetType().Name} has no DataContext", 404);
        var target = ObjectDump.Resolve(dc, r.Get("path"));
        return Json(r, ObjectDump.Dump(target, r.GetInt("depth", 1)));
    });

    private static Task<DevResponse> ViewModelDump(DevRequest r) => OnUi(() =>
    {
        var target = ObjectDump.Resolve(Globals.Instance, r.Get("path"));
        return Json(r, ObjectDump.Dump(target, r.GetInt("depth", 1)));
    });

    private static Task<DevResponse> State(DevRequest r) => OnUi(() => Json(r, AppState.Snapshot()));

    private static Task<DevResponse> Logs(DevRequest r)
    {
        if (r.Flag("clear"))
        {
            if (!r.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)) throw new DevToolsException("clear=1 changes state: use POST", 405);
            LogBuffer.Clear();
            return Task.FromResult(Json(r, new { cleared = true }));
        }

        var min = Enum.TryParse<Serilog.Events.LogEventLevel>(r.Get("level"), true, out var l) ? l : Serilog.Events.LogEventLevel.Verbose;
        var contains = r.Get("contains");
        var entries = LogBuffer.Snapshot()
            .Where(e => !Enum.TryParse<Serilog.Events.LogEventLevel>(e.Level, out var el) || el >= min)
            .Where(e => contains == null || e.Message.Contains(contains, StringComparison.OrdinalIgnoreCase) || e.Source.Contains(contains, StringComparison.OrdinalIgnoreCase))
            .TakeLast(r.GetInt("n", 100));

        var sb = new StringBuilder();
        foreach (var e in entries) sb.AppendLine($"[{e.Time:HH:mm:ss.fff} {e.Level[..Math.Min(3, e.Level.Length)].ToUpperInvariant()}] [{e.Source}] {e.Message}");
        return Task.FromResult(DevResponse.Text(sb.ToString()));
    }

    private static async Task<DevResponse> Screenshot(DevRequest r)
    {
        var png = await OnUi(() =>
        {
            Visual target = Selector.HasCriteria(r)
                ? Selector.Resolve(r)
                : Surfaces.Match(r.Get("window")) ?? throw new DevToolsException("no windows open yet", 404);
            return Capture.Png(target, r.GetDouble("scale", 1.0));
        });

        if (r.Flag("raw")) return DevResponse.Png(png);

        var path = Path.Combine(Capture.OutputDir, $"shot-{DateTime.Now:HHmmss-fff}.png");
        await File.WriteAllBytesAsync(path, png);
        return Json(r, new { path, bytes = png.Length });
    }

    // ---- mutating endpoints -----------------------------------------------------------------------------------

    private static async Task<DevResponse> Click(DevRequest r)
    {
        var mode = r.Get("mode") ?? "invoke";
        if (mode is not ("invoke" or "mouse")) throw new DevToolsException("mode must be invoke or mouse");

        var (target, how, point) = await OnUi<(Visual, string?, PixelPoint?)>(() =>
        {
            var v = Selector.Resolve(r);
            return mode == "invoke" ? (v, Input.Invoke(v), null) : (v, null, Input.PrepareOsMouse(v));
        });

        if (point is { } p)
        {
            await Input.OsMouse(p, r.Get("button") ?? "left", r.Flag("double"), moveOnly: false, wheelDelta: 0);
            how = "OS mouse " + (r.Get("button") ?? "left") + (r.Flag("double") ? " double-click" : " click") + $" at screen ({p.X},{p.Y})";
        }

        await Settle(r);
        return Json(r, new { ok = true, how, target = await OnUi(() => NodeInfo.Describe(target)), openPopups = await OnUi(() => Surfaces.OpenPopups().Count) });
    }

    private static async Task<DevResponse> Hover(DevRequest r)
    {
        var point = await OnUi(() => Input.PrepareOsMouse(Selector.Resolve(r)));
        await Input.OsMouse(point, "left", false, moveOnly: true, wheelDelta: 0);
        await Settle(r);
        return Json(r, new { ok = true });
    }

    private static async Task<DevResponse> Scroll(DevRequest r)
    {
        var point = await OnUi(() => Input.PrepareOsMouse(Selector.Resolve(r)));
        await Input.OsMouse(point, "left", false, moveOnly: false, wheelDelta: r.GetInt("dy", -3));
        await Settle(r);
        return Json(r, new { ok = true });
    }

    private static async Task<DevResponse> TypeText(DevRequest r)
    {
        var text = r.Get("text") ?? (r.Body.Length > 0 ? r.Body : throw new DevToolsException("text= (or a request body) is required"));
        await OnUi(() =>
        {
            Visual target = Selector.HasCriteria(r)
                ? Selector.Resolve(r)
                : (Surfaces.Match(r.Get("window"))?.FocusManager?.GetFocusedElement() as Visual) ?? throw new DevToolsException("nothing is focused: pass node= or a selector");
            Input.TypeText(target, text);
        });
        await Settle(r);
        return Json(r, new { ok = true, typed = text.Length });
    }

    private static async Task<DevResponse> Key(DevRequest r)
    {
        var key = Input.ParseKey(r.Get("key") ?? throw new DevToolsException("key= is required"));
        var mods = Input.ParseModifiers(r.Get("mods"));
        await OnUi(() =>
        {
            var explicitTarget = Selector.HasCriteria(r) ? Selector.Resolve(r) : null;
            Input.PressKey(Input.KeyTarget(r, explicitTarget), key, mods);
        });
        await Settle(r);
        return Json(r, new { ok = true, key = key.ToString(), mods = mods.ToString() });
    }

    private static async Task<DevResponse> Focus(DevRequest r)
    {
        var focused = await OnUi(() => Selector.Resolve(r) is Control c && c.Focus());
        await Settle(r);
        return Json(r, new { ok = focused });
    }

    private static async Task<DevResponse> SetProp(DevRequest r)
    {
        var name = r.Get("name") ?? throw new DevToolsException("name= is required");
        var value = r.Get("value") ?? throw new DevToolsException("value= is required");
        var result = await OnUi(() =>
        {
            var v = (AvaloniaObject)Selector.Resolve(r);
            var prop = AvaloniaPropertyRegistry.Instance.FindRegistered(v, name)
                       ?? throw new DevToolsException($"{v.GetType().Name} has no Avalonia property '{name}'", 404, "GET /props?node=... lists them");
            v.SetValue(prop, PropDump.Convert(value, prop.PropertyType));
            return new { ok = true, now = PropDump.Of(v, true).GetValueOrDefault(name) };
        });
        await Settle(r);
        return Json(r, result);
    }
}
