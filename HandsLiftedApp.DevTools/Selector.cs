using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace HandsLiftedApp.DevTools;

/// <summary>
/// Resolves a target from request parameters. <c>node=ID</c> wins; otherwise any combination of
/// <c>text</c>, <c>type</c>, <c>name</c>, <c>aid</c> (AutomationId) filters over the searched surfaces,
/// <c>contains=1</c> for substring text match, <c>index=N</c> to pick the Nth match.
/// Popups are searched before windows (they are on top); only visible nodes match unless <c>hidden=1</c>.
/// </summary>
public static class Selector
{
    public static bool HasCriteria(DevRequest r) =>
        r.Get("node") != null || r.Get("text") != null || r.Get("type") != null || r.Get("name") != null || r.Get("aid") != null;

    public static Visual Resolve(DevRequest r)
    {
        if (r.Get("node") is { } nodeArg)
        {
            if (!int.TryParse(nodeArg, out var id)) throw new DevToolsException($"node must be an integer id, got '{nodeArg}'");
            return NodeRegistry.Get(id)
                   ?? throw new DevToolsException($"node {id} no longer exists (the control was removed). Re-run /find or /tree.", 404);
        }

        var matches = FindAll(r).ToList();
        var index = r.GetInt("index", 0);
        if (matches.Count == 0)
            throw new DevToolsException("no visible node matched " + Describe(r), 404,
                "try /tree to see what is on screen, contains=1 for substring text, or hidden=1");
        if (index >= matches.Count)
            throw new DevToolsException($"index {index} out of range: only {matches.Count} matches for {Describe(r)}", 404);
        return matches[index];
    }

    public static IEnumerable<Visual> FindAll(DevRequest r)
    {
        var text = r.Get("text");
        var type = r.Get("type");
        var name = r.Get("name");
        var aid = r.Get("aid");
        var contains = r.Flag("contains");
        var hidden = r.Flag("hidden");

        IEnumerable<TopLevel> scope = r.Get("window") is { } w
            ? [Surfaces.Match(w) ?? throw new DevToolsException($"no window or popup matches '{w}'", 404, "see /windows")]
            : Surfaces.All();

        foreach (var root in scope)
        {
            foreach (var v in new[] { (Visual)root }.Concat(root.GetVisualDescendants()))
            {
                if (!hidden && !v.IsEffectivelyVisible) continue;
                if (type != null && !string.Equals(v.GetType().Name, type, StringComparison.OrdinalIgnoreCase)) continue;
                if (name != null && (v as StyledElement)?.Name != name) continue;
                if (aid != null && (v is not Control c || AutomationProperties.GetAutomationId(c) != aid)) continue;
                if (text != null)
                {
                    var t = NodeInfo.TextOf(v);
                    if (t == null) continue;
                    var ok = contains
                        ? t.Contains(text, StringComparison.OrdinalIgnoreCase)
                        : string.Equals(t.Trim(), text.Replace("_", "").Trim(), StringComparison.OrdinalIgnoreCase);
                    if (!ok) continue;
                }
                yield return v;
            }
        }
    }

    private static string Describe(DevRequest r) =>
        string.Join(", ", new[] { "text", "type", "name", "aid", "window" }
            .Where(k => r.Get(k) != null).Select(k => $"{k}='{r.Get(k)}'"));
}
