using Avalonia.Media;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.UiTests.Support;

namespace HandsLiftedApp.UiTests;

[TestClass]
public class ThemeThumbnailCacheTests
{
    private static BaseSlideTheme MakeTheme() => new() { Name = "Cache test", FontSize = 80 };

    [TestMethod]
    public async Task GetOrRender_SameTheme_RendersOnce()
    {
        await HeadlessSession.Run(() =>
        {
            var theme = MakeTheme();
            var before = ThemeThumbnailCache.RenderCount;

            var first = ThemeThumbnailCache.GetOrRender(theme);
            var second = ThemeThumbnailCache.GetOrRender(theme);

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
            Assert.AreEqual(before + 1, ThemeThumbnailCache.RenderCount);
            Assert.AreEqual(ThemeThumbnailCache.PixelWidth, first.PixelSize.Width);
        });
    }

    [TestMethod]
    public async Task GetOrRender_AfterThemeEdit_RendersNewBitmap()
    {
        await HeadlessSession.Run(() =>
        {
            var theme = MakeTheme();
            var first = ThemeThumbnailCache.GetOrRender(theme);

            theme.BackgroundAvaloniaColour = Colors.Red;
            var second = ThemeThumbnailCache.GetOrRender(theme);

            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
        });
    }

    [TestMethod]
    public async Task ThemeEdit_RaisesInvalidated_OnTheUiThread()
    {
        await HeadlessSession.Run(() =>
        {
            var theme = MakeTheme();
            ThemeThumbnailCache.GetOrRender(theme);

            var seen = new List<BaseSlideTheme>();
            using var sub = ThemeThumbnailCache.Invalidated.Subscribe(seen.Add);

            theme.BackgroundAvaloniaColour = Colors.Blue;
            UiTestHelpers.Settle();

            CollectionAssert.Contains(seen, theme);
        });
    }

    [TestMethod]
    public void IsRelevantChange_IgnoresCalculatedProperties()
    {
        Assert.IsFalse(ThemeThumbnailCache.IsRelevantChange("CalculatedFontWeight"));
        Assert.IsFalse(ThemeThumbnailCache.IsRelevantChange("CalculatedLineHeight"));
        Assert.IsTrue(ThemeThumbnailCache.IsRelevantChange("BackgroundAvaloniaColour"));
        Assert.IsTrue(ThemeThumbnailCache.IsRelevantChange("FontSize"));
        Assert.IsTrue(ThemeThumbnailCache.IsRelevantChange(""));
    }

    [TestMethod]
    public async Task GetOrRender_NullTheme_ReturnsNull()
    {
        await HeadlessSession.Run(() => Assert.IsNull(ThemeThumbnailCache.GetOrRender(null)));
    }

    [TestMethod]
    public async Task GetOrRender_MissingBackgroundImage_StillRenders()
    {
        await HeadlessSession.Run(() =>
        {
            var theme = MakeTheme();
            theme.BackgroundGraphicFilePath = @"Z:\nope.png";

            var bitmap = ThemeThumbnailCache.GetOrRender(theme);

            Assert.IsNotNull(bitmap);
        });
    }
}
