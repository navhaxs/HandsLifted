using Avalonia.Controls;
using Avalonia.Media;
using HandsLiftedApp.Core.Controls;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.UiTests.Support;

namespace HandsLiftedApp.UiTests;

[TestClass]
public class ThemePreviewImageTests
{
    private static BaseSlideTheme MakeTheme(string name = "Preview test") => new() { Name = name, FontSize = 80 };

    private static Window Host(ThemePreviewImage control)
    {
        control.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        var window = new Window { Width = 320, Height = 240, Content = control };
        window.Show();
        UiTestHelpers.Settle();
        return window;
    }

    /// <summary>Pumps the UI thread's dispatcher (timers, scheduled work) until <paramref name="condition"/> holds or two seconds pass.</summary>
    private static async Task<bool> WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            UiTestHelpers.Settle();
            if (condition()) return true;
            await Task.Delay(20);
        }
        UiTestHelpers.Settle();
        return condition();
    }

    /// <summary>Waits until no render happens for 600 ms (longer than the control's debounce), so a test sees the final state.</summary>
    private static async Task WaitUntilQuiet()
    {
        var last = ThemeThumbnailCache.RenderCount;
        var quietSince = DateTime.UtcNow;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && DateTime.UtcNow - quietSince < TimeSpan.FromMilliseconds(600))
        {
            UiTestHelpers.Settle();
            await Task.Delay(20);
            if (ThemeThumbnailCache.RenderCount != last)
            {
                last = ThemeThumbnailCache.RenderCount;
                quietSince = DateTime.UtcNow;
            }
        }
    }

    [TestMethod]
    public async Task Attached_WithTheme_ShowsBitmap()
    {
        await HeadlessSession.Run(() =>
        {
            var theme = MakeTheme();
            var control = new ThemePreviewImage { SlideTheme = theme };
            Host(control);

            Assert.IsNotNull(control.Source);
            Assert.AreSame(ThemeThumbnailCache.GetOrRender(theme), control.Source);
            Assert.AreEqual(320, control.Bounds.Width, 0.5);
            Assert.AreEqual(180, control.Bounds.Height, 0.5, "16:9 of the 320 px available width");
        });
    }

    [TestMethod]
    public async Task NeverAttached_DoesNotRender()
    {
        await HeadlessSession.Run(() =>
        {
            var before = ThemeThumbnailCache.RenderCount;

            var control = new ThemePreviewImage { SlideTheme = MakeTheme() };
            UiTestHelpers.Settle();

            Assert.IsNull(control.Source);
            Assert.AreEqual(before, ThemeThumbnailCache.RenderCount);
        });
    }

    [TestMethod]
    public async Task ThemeEdited_RefreshesSource()
    {
        await HeadlessSession.RunAsync(async () =>
        {
            var theme = MakeTheme();
            var control = new ThemePreviewImage { SlideTheme = theme };
            Host(control);
            var first = control.Source;
            Assert.IsNotNull(first);

            theme.BackgroundAvaloniaColour = Colors.Red;

            Assert.IsTrue(await WaitFor(() => !ReferenceEquals(control.Source, first)), "Source was not refreshed after the edit");
            Assert.IsNotNull(control.Source);
        });
    }

    [TestMethod]
    public async Task RapidEdits_AreDebounced()
    {
        await HeadlessSession.RunAsync(async () =>
        {
            var theme = MakeTheme();
            var control = new ThemePreviewImage { SlideTheme = theme };
            Host(control);
            var first = control.Source;
            var before = ThemeThumbnailCache.RenderCount;

            // Settle between edits so each change's notification is delivered, like a slider drag: without the
            // debounce every one of them would re-render.
            for (var i = 0; i < 50; i++)
            {
                theme.BackgroundAvaloniaColour = Color.FromRgb((byte)i, 0, 0);
                UiTestHelpers.Settle();
            }

            Assert.IsTrue(await WaitFor(() => !ReferenceEquals(control.Source, first)), "Source was not refreshed after the edits");
            await WaitUntilQuiet();

            var renders = ThemeThumbnailCache.RenderCount - before;
            Assert.IsTrue(renders >= 1 && renders <= 3, $"expected 1-3 renders for 50 edits, got {renders}");
        });
    }

    [TestMethod]
    public async Task ThemeSwapped_ShowsNewTheme_AndStopsListeningToOld()
    {
        await HeadlessSession.RunAsync(async () =>
        {
            var themeA = MakeTheme("A");
            var themeB = MakeTheme("B");
            var control = new ThemePreviewImage { SlideTheme = themeA };
            Host(control);
            Assert.AreSame(ThemeThumbnailCache.GetOrRender(themeA), control.Source);

            control.SlideTheme = themeB;
            UiTestHelpers.Settle();

            Assert.AreSame(ThemeThumbnailCache.GetOrRender(themeB), control.Source);
            var showing = control.Source;
            var renders = ThemeThumbnailCache.RenderCount;

            themeA.BackgroundAvaloniaColour = Colors.Green;
            await WaitUntilQuiet();

            Assert.AreSame(showing, control.Source, "editing the old theme changed the control");
            Assert.AreEqual(renders, ThemeThumbnailCache.RenderCount, "editing the old theme caused a render");

            // The new theme is still live.
            themeB.BackgroundAvaloniaColour = Colors.Blue;
            Assert.IsTrue(await WaitFor(() => !ReferenceEquals(control.Source, showing)), "the new theme no longer refreshes the control");
        });
    }

    [TestMethod]
    public async Task Detached_StopsListening_AndReattachShowsCurrentTheme()
    {
        await HeadlessSession.RunAsync(async () =>
        {
            var theme = MakeTheme();
            var control = new ThemePreviewImage { SlideTheme = theme };
            var window = Host(control);
            var shown = control.Source;

            window.Content = null;
            UiTestHelpers.Settle();
            var renders = ThemeThumbnailCache.RenderCount;

            theme.BackgroundAvaloniaColour = Colors.Orange;
            await WaitUntilQuiet();
            Assert.AreEqual(renders, ThemeThumbnailCache.RenderCount, "a detached control rendered");
            Assert.AreSame(shown, control.Source);

            window.Content = control;
            UiTestHelpers.Settle();
            Assert.AreNotSame(shown, control.Source, "reattaching did not pick up the edited theme");
            Assert.AreSame(ThemeThumbnailCache.GetOrRender(theme), control.Source);
        });
    }

    [TestMethod]
    public async Task NullTheme_MeasuresZeroAndDoesNotThrow()
    {
        await HeadlessSession.Run(() =>
        {
            var control = new ThemePreviewImage { SlideTheme = null };
            Host(control);

            Assert.IsNull(control.Source);
            Assert.AreEqual(0, control.DesiredSize.Width);
            Assert.AreEqual(0, control.DesiredSize.Height);
        });
    }

    [TestMethod]
    public async Task MotionVideoTheme_RendersWithBadge()
    {
        await HeadlessSession.Run(() =>
        {
            var theme = MakeTheme();
            theme.DefaultMotionBackgroundVideoPath = @"Z:\loop.mp4";
            var control = new ThemePreviewImage { SlideTheme = theme };
            var window = Host(control);

            Assert.IsNotNull(control.Source);
            window.Screenshot("ThemePreviewImage_MotionBadge");
        });
    }
}
