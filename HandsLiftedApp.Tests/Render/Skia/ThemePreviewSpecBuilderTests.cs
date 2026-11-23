using System.Threading.Tasks;
using Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Tests.TestSupport;
using SkiaSharp;

namespace HandsLiftedApp.Tests.Render.Skia;

[TestClass]
public class ThemePreviewSpecBuilderTests
{
    private static BaseSlideTheme MakeTheme(SlideThemeType type) => new BaseSlideTheme
    {
        Type = type,
        FontSize = 60,
        BackgroundColour = Colors.Red,
        TextColour = Colors.White,
    };

    [TestMethod]
    public Task Build_SongTheme_HasTextElementsAndThemeColourBackground() => DispatcherTestThread.Run(() =>
    {
        var spec = ThemePreviewSpecBuilder.Build(MakeTheme(SlideThemeType.SongTheme));

        Assert.IsTrue(spec.Elements.Count > 0);
        Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
        Assert.AreEqual(new SKColor(255, 0, 0, 255), ((SolidBackground)spec.Background).Color);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Build_GeneralTheme_UsesSongLyrics() => DispatcherTestThread.Run(() =>
    {
        var spec = ThemePreviewSpecBuilder.Build(MakeTheme(SlideThemeType.General));

        Assert.IsTrue(spec.Elements.Count > 0);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Build_ScriptureTheme_HasTextElements() => DispatcherTestThread.Run(() =>
    {
        var spec = ThemePreviewSpecBuilder.Build(MakeTheme(SlideThemeType.ScriptureTheme));

        Assert.IsTrue(spec.Elements.Count > 0);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Build_ThemeWithMotionVideo_BackgroundIsNotTransparent() => DispatcherTestThread.Run(() =>
    {
        var theme = MakeTheme(SlideThemeType.SongTheme);
        theme.DefaultMotionBackgroundVideoPath = "x.mp4";
        theme.BackgroundOpacity = 100;

        var spec = ThemePreviewSpecBuilder.Build(theme);

        Assert.IsNotInstanceOfType(spec.Background, typeof(TransparentBackground));
        return Task.CompletedTask;
    });

    [TestMethod]
    public void Build_NullTheme_ReturnsBlackSolidBackgroundNoElements()
    {
        var spec = ThemePreviewSpecBuilder.Build(null);

        Assert.AreEqual(0, spec.Elements.Count);
        Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
        Assert.AreEqual(SKColors.Black, ((SolidBackground)spec.Background).Color);
    }
}
