using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace HandsLiftedApp.DevTools;

/// <summary>
/// Everything that can show UI: top-level windows plus open popups (flyouts, context menus, combo dropdowns).
/// Popup content lives in its own PopupRoot top-level, so it is NOT reachable from a window's visual tree.
/// </summary>
public static class Surfaces
{
    /// <summary>Tests (headless, no application lifetime) can supply their own windows here.</summary>
    public static Func<IEnumerable<Window>>? WindowProvider { get; set; }

    public static IReadOnlyList<Window> Windows()
    {
        if (WindowProvider != null) return WindowProvider().ToList();
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
            ? d.Windows.ToList()
            : [];
    }

    /// <summary>An open popup surface and how to dismiss it.</summary>
    private sealed record PopupHandle(TopLevel Root, Action Close);

    /// <summary>Open popup roots over any window, most recently discovered last.</summary>
    public static IReadOnlyList<TopLevel> OpenPopups() => OpenPopupHandles().Select(h => h.Root).ToList();

    /// <summary>
    /// Closes the open popup (flyout, context menu, dropdown) that hosts <paramref name="v"/>, like a real click on a
    /// leaf menu item would. Returns false if <paramref name="v"/> is not inside a popup.
    /// </summary>
    public static bool DismissPopupContaining(Visual v)
    {
        var root = TopLevel.GetTopLevel(v);
        if (root == null || root is Window) return false;
        var handle = OpenPopupHandles().FirstOrDefault(h => h.Root == root);
        handle?.Close();
        return handle != null;
    }

    private static List<PopupHandle> OpenPopupHandles()
    {
        var found = new List<PopupHandle>();
        var windows = Windows();

        void Add(Visual? content, Action close)
        {
            if (content == null) return;
            var tl = TopLevel.GetTopLevel(content);
            if (tl != null && tl is not Window && !found.Any(h => h.Root == tl)) found.Add(new PopupHandle(tl, close));
        }

        foreach (var w in windows)
        {
            foreach (var v in w.GetVisualDescendants())
            {
                if (v is Popup { IsOpen: true } popup) Add(popup.Child, () => popup.IsOpen = false);
                if (v is not Control c) continue;

                foreach (var flyout in FlyoutsOf(c))
                    if (flyout.IsOpen) Add(FlyoutVisual(flyout), flyout.Hide);
                if (c.ContextMenu is { IsOpen: true } cm) Add(cm, cm.Close);
            }
        }
        return found;
    }

    /// <summary>Windows (active first) preceded by open popups, which are visually on top.</summary>
    public static IReadOnlyList<TopLevel> All()
    {
        var windows = Windows().OrderByDescending(w => w.IsActive).ToList<TopLevel>();
        return [.. OpenPopups(), .. windows];
    }

    public static string KindOf(TopLevel tl) => tl is Window ? "window" : "popup";

    private static IEnumerable<FlyoutBase> FlyoutsOf(Control c)
    {
        if (c.ContextFlyout != null) yield return c.ContextFlyout;
        if (FlyoutBase.GetAttachedFlyout(c) is { } attached) yield return attached;
        if (c is Button { Flyout: { } bf }) yield return bf;
        if (c is SplitButton { Flyout: { } sf }) yield return sf;
    }

    private static readonly PropertyInfo? FlyoutPopupProperty =
        typeof(FlyoutBase).GetProperty("Popup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static Visual? FlyoutVisual(FlyoutBase flyout)
    {
        // The flyout's own Popup (internal API, hence reflection) hosts the presenter inside the PopupRoot.
        if (FlyoutPopupProperty?.GetValue(flyout) is Popup { Child: { } child }) return child;
        return flyout switch
        {
            MenuFlyout mf => mf.Items.OfType<Visual>().FirstOrDefault(),
            Flyout f => f.Content as Visual,
            _ => null,
        };
    }

    /// <summary>The top-level a "window" selector refers to: node id, window type name, or title substring.</summary>
    public static TopLevel? Match(string? selector)
    {
        var all = All();
        if (string.IsNullOrWhiteSpace(selector)) return Windows().FirstOrDefault(w => w.GetType().Name == "MainWindow") ?? all.FirstOrDefault();
        if (int.TryParse(selector, out var id) && NodeRegistry.Get(id) is TopLevel byId) return byId;
        return all.FirstOrDefault(t => string.Equals(t.GetType().Name, selector, StringComparison.OrdinalIgnoreCase))
               ?? all.FirstOrDefault(t => t is Window w && (w.Title?.Contains(selector, StringComparison.OrdinalIgnoreCase) ?? false));
    }
}
