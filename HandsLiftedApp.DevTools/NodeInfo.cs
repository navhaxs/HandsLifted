using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace HandsLiftedApp.DevTools;

/// <summary>Describes visuals for clients: identity, visible text, bounds, state. Also the "interesting node" filter.</summary>
public static class NodeInfo
{
    /// <summary>Visible text/label of a visual, with menu access-key underscores removed.</summary>
    public static string? TextOf(Visual v)
    {
        string? s = v switch
        {
            TextBlock t => t.Text,
            TextBox tb => tb.Text,
            Window w => w.Title,
            MenuItem m => m.Header as string,
            HeaderedContentControl h when h.Header is string hs => hs,
            ContentControl c when c.Content is string cs => cs,
            _ => null,
        };
        return s?.Replace("_", "");
    }

    /// <summary>Current value for stateful controls (checked, slider value, selection).</summary>
    public static object? ValueOf(Visual v) => v switch
    {
        ToggleButton tb => tb.IsChecked,
        Slider s => s.Value,
        ProgressBar p => p.Value,
        ComboBox cb => cb.SelectedIndex,
        SelectingItemsControl sic => sic.SelectedIndex,
        _ => null,
    };

    public static Rect? BoundsInTopLevel(Visual v)
    {
        var tl = TopLevel.GetTopLevel(v);
        if (tl == null) return null;
        var p = v.TranslatePoint(default, tl);
        return p == null ? null : new Rect(p.Value, v.Bounds.Size);
    }

    public static Dictionary<string, object?> Describe(Visual v)
    {
        var d = new Dictionary<string, object?>
        {
            ["id"] = NodeRegistry.IdFor(v),
            ["type"] = v.GetType().Name,
        };
        if (v is StyledElement se && !string.IsNullOrEmpty(se.Name)) d["name"] = se.Name;
        if (v is Control c)
        {
            var aid = AutomationProperties.GetAutomationId(c);
            if (!string.IsNullOrEmpty(aid)) d["automationId"] = aid;
            if (c.DataContext != null) d["dataContext"] = c.DataContext.GetType().Name;
            if (!c.IsEnabled || !c.IsEffectivelyEnabled) d["enabled"] = false;
            if (c.IsFocused) d["focused"] = true;
        }
        if (!v.IsEffectivelyVisible) d["visible"] = false;
        if (TextOf(v) is { Length: > 0 } text) d["text"] = text.Length > 200 ? text[..200] + "…" : text;
        if (ValueOf(v) is { } val) d["value"] = val;
        if (BoundsInTopLevel(v) is { } b) d["bounds"] = new[] { (int)b.X, (int)b.Y, (int)b.Width, (int)b.Height };
        return d;
    }

    /// <summary>One human-readable line, e.g. <c>[12] Button "Add Item" name=AddBtn (134,707 118x44) dc=MainViewModel</c>.</summary>
    public static string Line(Visual v, string? inheritedDataContext = null)
    {
        var d = Describe(v);
        // DataContext is inherited by nearly everything: only print it where it changes.
        if (d.TryGetValue("dataContext", out var dcv) && (string?)dcv == inheritedDataContext) d.Remove("dataContext");
        var sb = new StringBuilder();
        sb.Append('[').Append(d["id"]).Append("] ").Append(d["type"]);
        if (d.TryGetValue("text", out var t)) sb.Append(" \"").Append(((string)t!).ReplaceLineEndings(" ")).Append('"');
        if (d.TryGetValue("name", out var n)) sb.Append(" name=").Append(n);
        if (d.TryGetValue("automationId", out var a)) sb.Append(" aid=").Append(a);
        if (d.TryGetValue("value", out var val)) sb.Append(" value=").Append(val);
        if (d.TryGetValue("bounds", out var b) && b is int[] r) sb.Append($" ({r[0]},{r[1]} {r[2]}x{r[3]})");
        if (d.ContainsKey("enabled")) sb.Append(" DISABLED");
        if (d.ContainsKey("visible")) sb.Append(" HIDDEN");
        if (d.ContainsKey("focused")) sb.Append(" FOCUSED");
        if (d.TryGetValue("dataContext", out var dc)) sb.Append(" dc=").Append(dc);
        return sb.ToString();
    }

    /// <summary>
    /// Whether a node is worth showing in the compact tree: named, interactive, text-bearing or a landmark
    /// (window / user control). Pure layout containers (Grid, Border, ContentPresenter...) are elided.
    /// </summary>
    public static bool IsInteresting(Visual v)
    {
        // Template-part names (PART_ContentPresenter...) are internals, not something a person named.
        if (v is StyledElement { Name: { Length: > 0 } n } && !n.StartsWith("PART_", StringComparison.Ordinal)) return true;
        if (v is Control c && !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(c))) return true;
        return v switch
        {
            TopLevel or UserControl => true,
            TextBlock t => !string.IsNullOrEmpty(t.Text),
            Button or ToggleButton or TextBox or ComboBox or Slider or MenuItem or TabItem or ListBoxItem
                or TreeViewItem or Expander or ProgressBar or ScrollBar or SelectingItemsControl => true,
            Avalonia.Controls.Image => true,
            // custom-drawn / app-specific controls (SlideCanvas etc.) are landmarks too
            Control when v.GetType().Namespace?.StartsWith("HandsLiftedApp") == true => true,
            _ => false,
        };
    }
}
