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

    // ~43 chars: measured to exceed the 1760px width at FontSize=100 (forcing a
    // wrap without autofit) but comfortably fit at the 0.5-ratio floor of 50
    // (so autofit is guaranteed to land on a single, non-floor, shrunk size).
    private const string LongSingleLine = "Consider Christ the source of our salvation";

    [TestMethod]
    public void Build_AutofitEnabled_LongLineShrinksAndStaysOnOneLine()
    {
        var slide = new SongSlideInstance(null, null, "id-autofit-1") { Text = LongSingleLine };
        slide.Theme = MakeTheme(); // AutofitEnabled defaults to true

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.AreEqual(1, spec.Elements.Count, "autofit should keep the raw line on one display line");
        var element = (TextLineElement)spec.Elements[0];
        Assert.IsTrue(element.FontSize < 100, "font should have shrunk below the theme size");
        Assert.IsTrue(element.FontSize >= 50, "font should not shrink below the 0.5 ratio floor");
    }

    [TestMethod]
    public void Build_AutofitDisabled_LongLineWrapsAtFixedSize()
    {
        var slide = new SongSlideInstance(null, null, "id-autofit-2") { Text = LongSingleLine };
        var theme = MakeTheme();
        theme.AutofitEnabled = false;
        slide.Theme = theme;

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.IsTrue(spec.Elements.Count > 1, "without autofit the long line should word-wrap into multiple lines");
        foreach (var el in spec.Elements)
        {
            Assert.AreEqual(100f, ((TextLineElement)el).FontSize, "font size must stay fixed when autofit is disabled");
        }
    }

    [TestMethod]
    public void Build_AutofitEnabled_ShortTextKeepsThemeFontSize()
    {
        var slide = new SongSlideInstance(null, null, "id-autofit-3") { Text = "Line one\nLine two" };
        slide.Theme = MakeTheme();

        var spec = SongSlideSpecBuilder.Build(slide);

        foreach (var el in spec.Elements)
        {
            Assert.AreEqual(100f, ((TextLineElement)el).FontSize, "short text should not be shrunk");
        }
    }

    [TestMethod]
    public void Build_AutofitEnabled_UnfittableLineFallsBackToFloorSize()
    {
        var oneGiantWord = new string('a', 300); // no spaces: word-wrap cannot split it further
        var slide = new SongSlideInstance(null, null, "id-autofit-4") { Text = oneGiantWord };
        slide.Theme = MakeTheme(); // AutofitMinFontSizeRatio defaults to 0.5

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.AreEqual(1, spec.Elements.Count);
        var element = (TextLineElement)spec.Elements[0];
        Assert.AreEqual(50f, element.FontSize, "line can never fit even shrunk, so size must land exactly on the floor");
    }
}
