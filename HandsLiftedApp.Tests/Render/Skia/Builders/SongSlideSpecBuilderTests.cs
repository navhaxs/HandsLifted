// HandsLiftedApp.Tests/Render/Skia/Builders/SongSlideSpecBuilderTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Avalonia.Media;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Tests.Render.Skia.Builders;

[TestClass]
public class SongSlideSpecBuilderTests
{
    private static BaseSlideTheme MakeTheme() => new BaseSlideTheme
    {
        FontSize = 100,
        TextColour = Colors.White,
        BackgroundColour = Colors.Black,
    };

    [TestMethod]
    public void Build_TwoLineText_ReturnsTwoTextElements()
    {
        var slide = new SongSlideInstance(null, null, "id1") { Text = "Line one\nLine two" };
        slide.Theme = MakeTheme();

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.AreEqual(2, spec.Elements.Count);
        Assert.IsInstanceOfType(spec.Elements[0], typeof(TextLineElement));
        Assert.IsInstanceOfType(spec.Elements[1], typeof(TextLineElement));
    }

    [TestMethod]
    public void Build_TextElementsCarryCorrectText()
    {
        var slide = new SongSlideInstance(null, null, "id2") { Text = "Amazing grace\nHow sweet the sound" };
        slide.Theme = MakeTheme();

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.AreEqual("Amazing grace", ((TextLineElement)spec.Elements[0]).Text);
        Assert.AreEqual("How sweet the sound", ((TextLineElement)spec.Elements[1]).Text);
    }

    [TestMethod]
    public void Build_NoMotionBackground_ReturnsNonTransparentBackground()
    {
        // SongSlideInstance.HasMotionBackground is driven by its parent SongItemInstance.
        // For now, test the false branch (no motion background):
        var slide = new SongSlideInstance(null, null, "id3") { Text = "Test" };
        slide.Theme = MakeTheme();

        var spec = SongSlideSpecBuilder.Build(slide);

        // No motion background → solid or image background, not transparent
        Assert.IsNotInstanceOfType(spec.Background, typeof(TransparentBackground));
    }

    [TestMethod]
    public void Build_EmptyText_ReturnsEmptyElements()
    {
        var slide = new SongSlideInstance(null, null, "id4") { Text = "" };
        slide.Theme = MakeTheme();

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }

    [TestMethod]
    public void Build_WhitespaceOnlyText_ReturnsEmptyElements()
    {
        var slide = new SongSlideInstance(null, null, "id5") { Text = "   " };
        slide.Theme = MakeTheme();

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }
}
