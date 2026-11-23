using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace HandsLiftedApp.DevTools;

/// <summary>
/// Input injection. Two strategies:
/// <list type="bullet">
/// <item><b>invoke</b> (default): the in-process accessibility API (automation peers: Invoke/Toggle/SelectionItem/
/// ExpandCollapse). No window focus needed, doesn't move the real mouse, works on popups.</item>
/// <item><b>mouse</b>: real OS mouse events at the control's screen position (Win32). Faithful hit-testing, but moves the
/// real cursor and needs the target visible and uncovered.</item>
/// </list>
/// </summary>
internal static class Input
{
    // ---- accessibility-API click -------------------------------------------------------------------------------

    /// <summary>Walks up from <paramref name="start"/> to the first control exposing an actionable automation pattern.</summary>
    public static (Control Control, string Pattern)? FindActionable(Visual start)
    {
        for (Visual? v = start; v != null; v = v.GetVisualParent())
        {
            if (v is not Control c) continue;
            var peer = ControlAutomationPeer.CreatePeerForElement(c);
            if (peer == null) continue;
            if (peer.GetProvider<IInvokeProvider>() != null) return (c, "invoke");
            if (peer.GetProvider<IToggleProvider>() != null) return (c, "toggle");
            if (peer.GetProvider<ISelectionItemProvider>() != null) return (c, "select");
            if (peer.GetProvider<IExpandCollapseProvider>() != null) return (c, "expand");
        }
        return null;
    }

    /// <summary>What the accessibility API reports for a node and each ancestor: peer type, role, name, available patterns.</summary>
    public static List<Dictionary<string, object?>> A11yChain(Visual start)
    {
        var rows = new List<Dictionary<string, object?>>();
        for (Visual? v = start; v != null; v = v.GetVisualParent())
        {
            if (v is not Control c) continue;
            var row = new Dictionary<string, object?> { ["node"] = NodeRegistry.IdFor(c), ["type"] = c.GetType().Name };
            try
            {
                var peer = ControlAutomationPeer.CreatePeerForElement(c);
                row["peer"] = peer?.GetType().Name;
                if (peer != null)
                {
                    row["role"] = peer.GetAutomationControlType().ToString();
                    row["name"] = peer.GetName();
                    row["patterns"] = new[]
                    {
                        peer.GetProvider<IInvokeProvider>() != null ? "invoke" : null,
                        peer.GetProvider<IToggleProvider>() != null ? "toggle" : null,
                        peer.GetProvider<ISelectionItemProvider>() != null ? "selectionItem" : null,
                        peer.GetProvider<IExpandCollapseProvider>() != null ? "expandCollapse" : null,
                        peer.GetProvider<IValueProvider>() != null ? "value" : null,
                        peer.GetProvider<IRangeValueProvider>() != null ? "rangeValue" : null,
                    }.Where(p => p != null).ToArray();
                }
            }
            catch (Exception ex) { row["error"] = ex.Message; }
            rows.Add(row);
        }
        return rows;
    }

    public static string Invoke(Visual target)
    {
        var actionable = FindActionable(target);
        if (actionable == null) return ClickFallback(target);
        var (control, pattern) = actionable.Value;

        var peer = ControlAutomationPeer.CreatePeerForElement(control)!;
        switch (pattern)
        {
            case "invoke": peer.GetProvider<IInvokeProvider>()!.Invoke(); break;
            case "toggle": peer.GetProvider<IToggleProvider>()!.Toggle(); break;
            case "select": peer.GetProvider<ISelectionItemProvider>()!.Select(); break;
            case "expand":
                var e = peer.GetProvider<IExpandCollapseProvider>()!;
                if (e.ExpandCollapseState == ExpandCollapseState.Collapsed) e.Expand(); else e.Collapse();
                break;
        }
        return $"{pattern} on {control.GetType().Name}";
    }

    /// <summary>
    /// For controls whose automation peer offers no actionable pattern: do what the control does on a real click -
    /// raise its Click event and, if unhandled, execute its Command (mirrors Button/MenuItem.OnClick).
    /// </summary>
    private static string ClickFallback(Visual target)
    {
        for (Visual? v = target; v != null; v = v.GetVisualParent())
        {
            RoutedEvent? clickEvent = v switch { MenuItem => MenuItem.ClickEvent, Button => Button.ClickEvent, _ => null };
            if (clickEvent == null || v is not Interactive i) continue;

            var c = (Control)v;
            if (!c.IsEffectivelyEnabled) throw new DevToolsException($"{v.GetType().Name} is disabled", 422);

            var args = new RoutedEventArgs(clickEvent) { Source = v };
            i.RaiseEvent(args);
            var ranCommand = false;
            if (!args.Handled && v is Avalonia.Input.ICommandSource { Command: { } cmd } src && cmd.CanExecute(src.CommandParameter))
            {
                cmd.Execute(src.CommandParameter);
                ranCommand = true;
            }
            // A real click on a leaf menu item dismisses the menu; do the same.
            var dismissed = v is MenuItem { HasSubMenu: false } && Surfaces.DismissPopupContaining(v);
            return $"Click event{(ranCommand ? " + Command" : "")}{(dismissed ? " + menu dismissed" : "")} on {v.GetType().Name} (no accessibility pattern; fallback)";
        }
        throw new DevToolsException($"{target.GetType().Name} (or its ancestors) is not clickable: no accessibility pattern and not a Button/MenuItem",
            422, "use mode=mouse to click it with a real pointer, or target a Button/MenuItem/ListBoxItem; GET /a11y?node=... shows what is exposed");
    }

    // ---- text / keys (in-process routed events) ----------------------------------------------------------------

    public static void TypeText(Visual target, string text)
    {
        if (target is Control c && !c.IsFocused) c.Focus();
        if (target is not Interactive i) throw new DevToolsException("target cannot receive text input");
        i.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text, Source = target });
    }

    public static Interactive KeyTarget(DevRequest r, Visual? explicitTarget)
    {
        if (explicitTarget is Interactive i) return i;
        var tl = Surfaces.Match(r.Get("window")) ?? throw new DevToolsException("no window to send keys to");
        return (tl.FocusManager?.GetFocusedElement() as Interactive) ?? tl;
    }

    public static void PressKey(Interactive target, Key key, KeyModifiers mods)
    {
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = mods, Source = target });
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key, KeyModifiers = mods, Source = target });
    }

    public static Key ParseKey(string s)
    {
        var alias = s.ToLowerInvariant() switch
        {
            "esc" => "Escape", "del" => "Delete", "return" => "Enter", "pgup" => "PageUp", "pgdn" => "PageDown",
            "ctrl" => "LeftCtrl", "space" => "Space", _ => s,
        };
        if (alias.Length == 1 && char.IsDigit(alias[0])) alias = "D" + alias;
        if (Enum.TryParse<Key>(alias, ignoreCase: true, out var k)) return k;
        throw new DevToolsException($"unknown key '{s}'", 400, "use Avalonia Key names, e.g. Enter, Escape, Tab, Left, F5, Z, D1");
    }

    public static KeyModifiers ParseModifiers(string? s)
    {
        var m = KeyModifiers.None;
        foreach (var part in (s ?? "").Split(['+', ',', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            m |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => KeyModifiers.Control,
                "shift" => KeyModifiers.Shift,
                "alt" => KeyModifiers.Alt,
                "meta" or "win" or "cmd" => KeyModifiers.Meta,
                _ => throw new DevToolsException($"unknown modifier '{part}'", 400, "ctrl, shift, alt, meta"),
            };
        }
        return m;
    }

    // ---- real OS mouse (Windows) -------------------------------------------------------------------------------

    private const uint LeftDown = 0x2, LeftUp = 0x4, RightDown = 0x8, RightUp = 0x10, Wheel = 0x800;

    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extra);

    /// <summary>UI thread only: activates the target's window and returns the target's centre in screen pixels.</summary>
    public static PixelPoint PrepareOsMouse(Visual v)
    {
        if (!OperatingSystem.IsWindows()) throw new DevToolsException("mode=mouse is only implemented on Windows", 501);
        if (TopLevel.GetTopLevel(v) is Window w) w.Activate();
        return v.PointToScreen(new Point(v.Bounds.Width / 2, v.Bounds.Height / 2));
    }

    /// <summary>Any thread: drives the real OS cursor/buttons. Call <see cref="PrepareOsMouse"/> on the UI thread first.</summary>
    public static async Task OsMouse(PixelPoint p, string button, bool doubleClick, bool moveOnly, int wheelDelta)
    {
        await Task.Delay(120); // let window activation settle
        if (!SetCursorPos(p.X, p.Y)) throw new Win32Exception(Marshal.GetLastWin32Error());
        await Task.Delay(30);
        if (moveOnly) return;

        if (wheelDelta != 0) { mouse_event(Wheel, 0, 0, wheelDelta * 120, UIntPtr.Zero); return; }

        var (down, up) = button == "right" ? (RightDown, RightUp) : (LeftDown, LeftUp);
        for (var i = 0; i < (doubleClick ? 2 : 1); i++)
        {
            mouse_event(down, 0, 0, 0, UIntPtr.Zero);
            await Task.Delay(30);
            mouse_event(up, 0, 0, 0, UIntPtr.Zero);
            await Task.Delay(60);
        }
    }
}
