using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.Views;

namespace HandsLiftedApp.UiTests.Support;

public static class UiTestHelpers
{
    /// <summary>Opens a real <see cref="MainWindow"/> over a fresh, empty <see cref="MainViewModel"/>. Call on the UI thread.</summary>
    public static MainWindow OpenMainWindow(double width = 1280, double height = 800)
    {
        // Skip Globals.OnStartup (libmpv, Syncfusion licence, remote-control server): set only what MainWindow needs.
        var globals = Globals.Instance;
        globals.AppPreferences = new AppPreferencesViewModel { OnTheEdge = true };
        globals.MainViewModel = new MainViewModel();

        var window = new MainWindow { DataContext = globals.MainViewModel, Width = width, Height = height };
        window.Show();
        Settle();
        return window;
    }

    /// <summary>Drains pending dispatcher work (layout, bindings, render) so the next assertion sees a stable UI.</summary>
    public static void Settle() => Dispatcher.UIThread.RunJobs();

    /// <summary>First visual descendant of type <typeparamref name="T"/> matching <paramref name="predicate"/>, or null.</summary>
    public static T? Find<T>(this Visual root, Func<T, bool>? predicate = null) where T : Visual =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(v => predicate?.Invoke(v) ?? true);

    /// <summary>The control whose visible text is exactly <paramref name="text"/> (TextBlock), or its nearest Button ancestor if <typeparamref name="T"/> is Button.</summary>
    public static T? FindByText<T>(this Visual root, string text) where T : Control
    {
        foreach (var tb in root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == text && t.IsEffectivelyVisible))
        {
            if (tb is T direct) return direct;
            var ancestor = tb.FindAncestorOfType<T>();
            if (ancestor != null) return ancestor;
        }
        return null;
    }

    /// <summary>
    /// Finds a control inside any currently open popup/flyout of <paramref name="window"/>. Popup content is hosted
    /// in a separate PopupRoot, so it is NOT reachable by walking the window's own visual tree.
    /// </summary>
    /// <summary>
    /// Finds a menu item (searching sub-menus too) in the flyout attached to <paramref name="button"/> by its visible
    /// text. Flyout content is hosted in its own PopupRoot top-level, which is not reachable from the window's visual
    /// tree, so we go through the flyout's own item collection.
    /// </summary>
    public static MenuItem? FindFlyoutMenuItem(this Button button, string text)
    {
        static IEnumerable<MenuItem> Walk(IEnumerable<object?> items)
        {
            foreach (var m in items.OfType<MenuItem>())
            {
                yield return m;
                foreach (var child in Walk(m.Items.Cast<object?>())) yield return child;
            }
        }

        if (button.Flyout is not MenuFlyout flyout) return null;
        return Walk(flyout.Items.Cast<object?>()).FirstOrDefault(m =>
            // Menu headers carry access-key underscores ("_Section"): compare on the visible text.
            (m.Header as string)?.Replace("_", "") == text ||
            m.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text));
    }

    /// <summary>Real pointer click on a control hosted in a popup: the click goes through that popup's own PopupRoot.</summary>
    public static void ClickInPopup(this Control target)
    {
        var root = TopLevel.GetTopLevel(target) ?? throw new InvalidOperationException("control is not attached to a top-level");
        var p = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), root)
                ?? throw new InvalidOperationException("control is not attached to its popup root");
        root.MouseMove(p);
        root.MouseDown(p, MouseButton.Left);
        root.MouseUp(p, MouseButton.Left);
        Settle();
    }

    /// <summary>Centre of <paramref name="control"/> in <paramref name="window"/> coordinates.</summary>
    public static Point CentreIn(this Control control, Window window)
    {
        var p = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)
                ?? throw new InvalidOperationException($"{control.GetType().Name} is not attached to the window");
        return p;
    }

    /// <summary>Real pointer click through Avalonia's input pipeline (hit-testing included), then lets the UI settle.</summary>
    public static void Click(this Window window, Control target)
    {
        var p = target.CentreIn(window);
        window.MouseMove(p);
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Settle();
    }

    public static void Screenshot(this Window window, string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "screenshots", name + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        window.CaptureRenderedFrame()?.Save(path);
        Console.WriteLine($"Screenshot: {path}");
    }
}
