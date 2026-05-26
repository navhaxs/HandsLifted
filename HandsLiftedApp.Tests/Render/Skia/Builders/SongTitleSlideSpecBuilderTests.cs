// HandsLiftedApp.Tests/Render/Skia/Builders/SongTitleSlideSpecBuilderTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Avalonia.Media;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

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
}
