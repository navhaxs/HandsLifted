using Avalonia.Controls;
using Avalonia.Headless;
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

    // ---- badge pixels -------------------------------------------------------------------------------------------

    /// <summary>A rendered window frame as raw pixels (R, G, B in that order, 4 bytes per pixel).</summary>
    private sealed record Frame(int Width, int Height, byte[] Rgba)
    {
        public (byte R, byte G, byte B) At(int x, int y)
        {
            var i = (y * Width + x) * 4;
            return (Rgba[i], Rgba[i + 1], Rgba[i + 2]);
        }

        /// <summary>Gold star pixels (#FFC107, allowing for anti-aliasing against the dark disc), as (x, y).</summary>
        public List<(int X, int Y)> GoldPixels()
        {
            var found = new List<(int, int)>();
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                {
                    var (r, g, b) = At(x, y);
                    if (r > 200 && g is > 150 and < 225 && b < 80) found.Add((x, y));
                }
            return found;
        }

        public bool SameRegion(Frame other, int x0, int y0, int x1, int y1)
        {
            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                    if (At(x, y) != other.At(x, y)) return false;
            return true;
        }
    }

    // Host() gives the control 320 x 180 at the window's top-left; the badges (22 px discs, 5.5 px margin) sit in these corners.
    private const int RightX0 = 280, RightX1 = 320, TopY0 = 0, TopY1 = 40, BottomY0 = 140, BottomY1 = 180;

    private static Frame Render(BaseSlideTheme theme, bool isDefault)
    {
        var control = new ThemePreviewImage { SlideTheme = theme, IsDefault = isDefault };
        var window = Host(control);
        var bitmap = window.CaptureRenderedFrame() ?? throw new AssertFailedException("headless renderer produced no frame");
        try
        {
            using var fb = bitmap.Lock();
            var bytes = new byte[fb.RowBytes * fb.Size.Height];
            System.Runtime.InteropServices.Marshal.Copy(fb.Address, bytes, 0, bytes.Length);
            var bgra = fb.Format == Avalonia.Platform.PixelFormat.Bgra8888;
            var rgba = new byte[fb.Size.Width * fb.Size.Height * 4];
            for (var y = 0; y < fb.Size.Height; y++)
                for (var x = 0; x < fb.Size.Width; x++)
                {
                    var s = y * fb.RowBytes + x * 4;
                    var d = (y * fb.Size.Width + x) * 4;
                    rgba[d] = bytes[s + (bgra ? 2 : 0)];
                    rgba[d + 1] = bytes[s + 1];
                    rgba[d + 2] = bytes[s + (bgra ? 0 : 2)];
                    rgba[d + 3] = bytes[s + 3];
                }
            return new Frame(fb.Size.Width, fb.Size.Height, rgba);
        }
        finally
        {
            window.Close();
            bitmap.Dispose();
        }
    }

    private static BaseSlideTheme MakeBrightTheme(bool motion)
    {
        // A white background: the badge must be readable on a bright thumbnail too.
        var theme = MakeTheme();
        theme.BackgroundAvaloniaColour = Colors.White;
        if (motion) theme.DefaultMotionBackgroundVideoPath = @"Z:\loop.mp4";
        return theme;
    }

    [TestMethod]
    public async Task IsDefault_DrawsStarBadgeInTopRightCorner()
    {
        await HeadlessSession.Run(() =>
        {
            var theme = MakeBrightTheme(motion: false);
            var plain = Render(theme, isDefault: false);
            var starred = Render(theme, isDefault: true);

            Assert.AreEqual(0, plain.GoldPixels().Count, "no gold without IsDefault");

            var gold = starred.GoldPixels();
            Assert.IsTrue(gold.Count >= 15, $"expected a gold star, found {gold.Count} gold pixels");
            Assert.IsTrue(gold.All(p => p.X >= RightX0 && p.X < RightX1 && p.Y >= TopY0 && p.Y < TopY1),
                "the star must sit in the top-right corner");

            Assert.IsFalse(plain.SameRegion(starred, RightX0, TopY0, RightX1, TopY1), "top-right corner is unchanged by IsDefault");
            Assert.IsTrue(plain.SameRegion(starred, 0, TopY1, plain.Width, plain.Height),
                "IsDefault must only change the top-right badge area, including leaving the bottom-right corner alone");
            Assert.IsTrue(plain.SameRegion(starred, 0, 0, RightX0, TopY1), "IsDefault changed pixels left of the badge");
            Assert.IsTrue(plain.SameRegion(starred, RightX0, BottomY0, RightX1, BottomY1), "bottom-right corner changed");
        });
    }

    [TestMethod]
    public async Task IsDefault_OnMotionTheme_DrawsBothBadgesWithoutOverlap()
    {
        await HeadlessSession.Run(() =>
        {
            var motion = MakeBrightTheme(motion: true);
            var plainTheme = MakeBrightTheme(motion: false);

            var motionOnly = Render(motion, isDefault: false); // play badge only
            var both = Render(motion, isDefault: true);        // play + star
            var starOnly = Render(plainTheme, isDefault: true); // star only

            // Adding IsDefault to a motion theme changes the top-right corner and leaves the play badge alone.
            Assert.IsFalse(motionOnly.SameRegion(both, RightX0, TopY0, RightX1, TopY1), "star missing on a motion theme");
            Assert.IsTrue(motionOnly.SameRegion(both, RightX0, BottomY0, RightX1, BottomY1), "the play badge changed when IsDefault was set");
            Assert.IsTrue(both.GoldPixels().All(p => p.Y < TopY1), "star pixels leaked outside the top-right corner");
            Assert.IsTrue(both.GoldPixels().Count >= 15, "no star on the motion theme");

            // The play badge is still there: it is the only difference from the star-only render, and it is in the bottom-right.
            Assert.IsFalse(starOnly.SameRegion(both, RightX0, BottomY0, RightX1, BottomY1), "play badge missing when IsDefault is set");
            Assert.IsTrue(starOnly.SameRegion(both, RightX0, TopY0, RightX1, TopY1), "the star differs between motion and plain themes");
            Assert.IsTrue(starOnly.SameRegion(both, 0, 0, RightX0, starOnly.Height), "motion video changed pixels left of the badges");

            // Neither badge reaches the other: the right-hand strip between the two corners is untouched by IsDefault.
            Assert.IsTrue(motionOnly.SameRegion(both, RightX0, TopY1, RightX1, BottomY0), "the star badge reaches below the top-right corner");

            var host = new ThemePreviewImage { SlideTheme = motion, IsDefault = true };
            var window = Host(host);
            window.Screenshot("ThemePreviewImage_MotionBadge_Default");
            window.Close();
        });
    }
}
