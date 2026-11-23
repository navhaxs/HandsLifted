using System.Reflection;
using Avalonia.Media;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data.Data.Models.Types;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.UiTests.Support;
using ReactiveUI;

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

    /// <remarks>Reflects on ReactiveUI's <c>ReactiveObject.PropertyChangedHandler</c> field: expect to adjust it on a ReactiveUI upgrade.</remarks>
    private static int PropertyChangedHandlerCount(object target)
    {
        var field = typeof(ReactiveObject).GetField("PropertyChangedHandler", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?? throw new AssertFailedException("ReactiveObject no longer has a PropertyChangedHandler field to count handlers on");
        return (field.GetValue(target) as MulticastDelegate)?.GetInvocationList().Length ?? 0;
    }

    // Each refresh used to leave a throwaway slide instance subscribed to the theme: the leak grew with every edit and
    // every later edit then queued a full-HD render per leaked slide.
    [TestMethod]
    [DataRow(SlideThemeType.SongTheme)]
    [DataRow(SlideThemeType.ScriptureTheme)]
    public async Task RefreshingThumbnails_DoesNotAccumulateThemeSubscriptions(SlideThemeType type)
    {
        await HeadlessSession.Run(() =>
        {
            var theme = new BaseSlideTheme { Name = "Leak test", Type = type, FontSize = 80 };
            ThemeThumbnailCache.GetOrRender(theme);
            UiTestHelpers.Settle();
            var baseline = PropertyChangedHandlerCount(theme);

            for (var i = 0; i < 4; i++)
            {
                theme.BackgroundAvaloniaColour = Color.FromRgb((byte)(10 + i), 0, 0);
                UiTestHelpers.Settle();
                var renders = ThemeThumbnailCache.RenderCount;
                Assert.IsNotNull(ThemeThumbnailCache.GetOrRender(theme));
                Assert.AreEqual(renders + 1, ThemeThumbnailCache.RenderCount, "the edit did not cause a fresh render");
            }

            Assert.AreEqual(baseline, PropertyChangedHandlerCount(theme), "thumbnail refreshes left subscriptions on the theme");
        });
    }

    // Changing a font weight raises a Calculated* notification asynchronously after the real property. That derived
    // notification must not invalidate the thumbnail a second time.
    [TestMethod]
    public async Task EditThatRaisesCalculatedProperty_InvalidatesExactlyOnce()
    {
        await HeadlessSession.Run(() =>
        {
            var theme = MakeTheme();
            ThemeThumbnailCache.GetOrRender(theme);
            UiTestHelpers.Settle();

            var calculated = new List<string>();
            using var changes = theme.Changed.Subscribe(e => { if (e.PropertyName?.StartsWith("Calculated") == true) calculated.Add(e.PropertyName); });
            var invalidations = 0;
            using var sub = ThemeThumbnailCache.Invalidated.Subscribe(_ => invalidations++);

            theme.FontWeight = (XmlFontWeight)FontWeight.Bold; // the derived CalculatedTextFontBold flips after it
            UiTestHelpers.Settle();

            Assert.IsTrue(calculated.Count > 0, "the edit raised no Calculated* notification, so this test proves nothing");
            Assert.AreEqual(1, invalidations, "a Calculated* notification caused an extra invalidation");
        });
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
