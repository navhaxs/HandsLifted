// HandsLiftedApp.Tests/Render/Skia/Builders/SongTitleSlideSpecBuilderTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Avalonia.Media;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;
using SkiaSharp;

namespace HandsLiftedApp.Tests.Render.Skia.Builders;

[TestClass]
public class SongTitleSlideSpecBuilderTests
{
    private static BaseSlideTheme MakeTheme() => new BaseSlideTheme
    {
        FontSize = 120,
        TextColour = Colors.White,
        BackgroundColour = Colors.Black,
    };

    [TestMethod]
    public void Build_TitleOnly_ReturnsOneElement()
    {
        var slide = new SongTitleSlideInstance(null) { Title = "Amazing Grace", Copyright = "" };
        slide.Theme = MakeTheme();

        var spec = SongTitleSlideSpecBuilder.Build(slide);

        Assert.AreEqual(1, spec.Elements.Count);
        Assert.AreEqual("Amazing Grace", ((TextLineElement)spec.Elements[0]).Text);
    }

    [TestMethod]
    public void Build_TitleAndCopyright_ReturnsTwoElements()
    {
        var slide = new SongTitleSlideInstance(null)
        {
            Title = "Amazing Grace",
            Copyright = "Public Domain"
        };
        slide.Theme = MakeTheme();

        var spec = SongTitleSlideSpecBuilder.Build(slide);

        Assert.AreEqual(2, spec.Elements.Count);
    }

    [TestMethod]
    public void Build_CopyrightIsSmaller_ThanTitle()
    {
        var slide = new SongTitleSlideInstance(null)
        {
            Title = "Amazing Grace",
            Copyright = "Public Domain"
        };
        slide.Theme = MakeTheme();

        var spec = SongTitleSlideSpecBuilder.Build(slide);

        var titleEl     = (TextLineElement)spec.Elements[0];
        var copyrightEl = (TextLineElement)spec.Elements[1];
        Assert.IsTrue(copyrightEl.FontSize < titleEl.FontSize);
    }

    [TestMethod]
    public void Build_WithVideoFrame_UsesSkiaBitmapBackground()
    {
        var slide = new SongTitleSlideInstance(null) { Title = "Holy Forever", Copyright = "" };
        slide.Theme = MakeTheme();
        using var videoFrame = new SkiaSharp.SKBitmap(4, 4);

        var spec = SongTitleSlideSpecBuilder.Build(slide, videoFrame);

        Assert.IsInstanceOfType(spec.Background, typeof(SkiaBitmapBackground));
        Assert.AreSame(videoFrame, ((SkiaBitmapBackground)spec.Background).Bitmap);
    }

    [TestMethod]
    public void Build_WithNullVideoFrame_UsesSolidBackground()
    {
        var slide = new SongTitleSlideInstance(null) { Title = "Test", Copyright = "" };
        slide.Theme = MakeTheme();

        var spec = SongTitleSlideSpecBuilder.Build(slide, null);

        Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
    }

    [TestMethod]
    public void Build_VideoFrameTakesPriorityOverThemeImage()
    {
        var slide = new SongTitleSlideInstance(null) { Title = "Test", Copyright = "" };
        slide.Theme = MakeTheme();
        slide.Theme.BackgroundGraphicFilePath = "some/image.png";
        using var videoFrame = new SkiaSharp.SKBitmap(4, 4);

        var spec = SongTitleSlideSpecBuilder.Build(slide, videoFrame);

        Assert.IsInstanceOfType(spec.Background, typeof(SkiaBitmapBackground));
    }
}
