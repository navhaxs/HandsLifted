// HandsLiftedApp.Tests/Render/Skia/Builders/ThemeBackgroundSpecBuilderTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using SkiaSharp;
using AvaloniaColor = Avalonia.Media.Color;
using AvaloniaColors = Avalonia.Media.Colors;

namespace HandsLiftedApp.Tests.Render.Skia.Builders;

[TestClass]
public class ThemeBackgroundSpecBuilderTests
{
    private static readonly SKColor Blue = new(0, 0, 255, 255);

    private static BaseSlideTheme MakeTheme(int opacity = 100, string? image = null) => new()
    {
        BackgroundColour = AvaloniaColors.Blue,
        BackgroundOpacity = opacity,
        BackgroundGraphicFilePath = image,
    };

    [TestMethod]
    public void NullTheme_NoMotion_IsBlackSolid()
    {
        var bg = ThemeBackgroundSpecBuilder.Build(null, hasMotionBackground: false);

        Assert.AreEqual(new SolidBackground(SKColors.Black), bg);
    }

    [TestMethod]
    public void Plain_NoMedia_IsSolidThemeColour_OpacityIgnored()
    {
        var bg = ThemeBackgroundSpecBuilder.Build(MakeTheme(opacity: 40), hasMotionBackground: false);

        Assert.AreEqual(new SolidBackground(Blue), bg);
    }

    [TestMethod]
    public void Image_CarriesColourAndOpacity()
    {
        var bg = ThemeBackgroundSpecBuilder.Build(MakeTheme(opacity: 40, image: "a.png"), hasMotionBackground: false);

        Assert.AreEqual(new ImageBackground("a.png", Blue, 0.4f), bg);
    }

    [TestMethod]
    public void Motion_Opacity100_IsTransparent()
    {
        var bg = ThemeBackgroundSpecBuilder.Build(MakeTheme(opacity: 100), hasMotionBackground: true);

        Assert.IsInstanceOfType(bg, typeof(TransparentBackground));
    }

    [TestMethod]
    public void Motion_Opacity40_IsSolidColourWithAlpha153()
    {
        var bg = ThemeBackgroundSpecBuilder.Build(MakeTheme(opacity: 40), hasMotionBackground: true);

        Assert.AreEqual(new SolidBackground(new SKColor(0, 0, 255, 153)), bg);
    }

    [TestMethod]
    public void Motion_Opacity0_IsFullyOpaqueColour()
    {
        var bg = ThemeBackgroundSpecBuilder.Build(MakeTheme(opacity: 0), hasMotionBackground: true);

        Assert.AreEqual(new SolidBackground(Blue), bg);
    }

    [TestMethod]
    public void Motion_ColourWithOwnAlpha_IsScaledNotReplaced()
    {
        var theme = MakeTheme(opacity: 50);
        theme.BackgroundColour = AvaloniaColor.FromArgb(200, 0, 0, 255);

        var bg = ThemeBackgroundSpecBuilder.Build(theme, hasMotionBackground: true);

        Assert.AreEqual(new SolidBackground(new SKColor(0, 0, 255, 100)), bg);
    }

    [TestMethod]
    public void VideoFrame_WinsOverMotion_CarriesColourAndOpacity()
    {
        using var frame = new SKBitmap(4, 4);

        var bg = ThemeBackgroundSpecBuilder.Build(MakeTheme(opacity: 40), hasMotionBackground: true, frame);

        var bitmapBg = (SkiaBitmapBackground)bg;
        Assert.AreSame(frame, bitmapBg.Bitmap);
        Assert.AreEqual(Blue, bitmapBg.BackColor);
        Assert.AreEqual(0.4f, bitmapBg.Opacity);
    }
}
