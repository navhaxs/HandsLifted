using Microsoft.VisualStudio.TestTools.UnitTesting;
using Avalonia.Media;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Tests.Render.Skia.Builders;

[TestClass]
public class ScriptureSlideSpecBuilderTests
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
        var slide = new ScriptureSlideInstance(null, "id1") { Text = "Line one\nLine two" };
        slide.Theme = MakeTheme();

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual(2, spec.Elements.Count);
        Assert.IsInstanceOfType(spec.Elements[0], typeof(TextLineElement));
        Assert.IsInstanceOfType(spec.Elements[1], typeof(TextLineElement));
    }

    [TestMethod]
    public void Build_TextElementsCarryCorrectText()
    {
        var slide = new ScriptureSlideInstance(null, "id2") { Text = "For God so loved the world\nthat He gave His one and only Son" };
        slide.Theme = MakeTheme();

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual("For God so loved the world", ((TextLineElement)spec.Elements[0]).Text);
        Assert.AreEqual("that He gave His one and only Son", ((TextLineElement)spec.Elements[1]).Text);
    }

    [TestMethod]
    public void Build_WithTheme_ReturnsSolidBackground()
    {
        var slide = new ScriptureSlideInstance(null, "id3") { Text = "Test" };
        slide.Theme = MakeTheme();

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
    }

    [TestMethod]
    public void Build_NoTheme_ReturnsEmptyElements()
    {
        var slide = new ScriptureSlideInstance(null, "id-no-theme") { Text = "Test" };
        slide.Theme = null;

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }

    [TestMethod]
    public void Build_EmptyText_ReturnsEmptyElements()
    {
        var slide = new ScriptureSlideInstance(null, "id4") { Text = "" };
        slide.Theme = MakeTheme();

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }

    [TestMethod]
    public void Build_WhitespaceOnlyText_ReturnsEmptyElements()
    {
        var slide = new ScriptureSlideInstance(null, "id5") { Text = "   " };
        slide.Theme = MakeTheme();

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }

    // ~55 chars: measured to exceed the 1760px width at FontSize=100 (forcing a
    // wrap without autofit) but comfortably fit at the 0.5-ratio floor of 50.
    private const string LongVerseLine = "For God so loved the world that He gave His only Son";

    [TestMethod]
    public void Build_AutofitEnabled_LongLineShrinksAndStaysOnOneLine()
    {
        var slide = new ScriptureSlideInstance(null, "id-autofit-1") { Text = LongVerseLine };
        slide.Theme = MakeTheme(); // AutofitEnabled defaults to true

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual(1, spec.Elements.Count, "autofit should keep the raw line on one display line");
        var element = (TextLineElement)spec.Elements[0];
        Assert.IsTrue(element.FontSize < 100, "font should have shrunk below the theme size");
        Assert.IsTrue(element.FontSize >= 50, "font should not shrink below the 0.5 ratio floor");
    }

    [TestMethod]
    public void Build_AutofitDisabled_LongLineWrapsAtFixedSize()
    {
        var slide = new ScriptureSlideInstance(null, "id-autofit-2") { Text = LongVerseLine };
        var theme = MakeTheme();
        theme.AutofitEnabled = false;
        slide.Theme = theme;

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.IsTrue(spec.Elements.Count > 1, "without autofit the long line should word-wrap into multiple lines");
        foreach (var el in spec.Elements)
        {
            Assert.AreEqual(100f, ((TextLineElement)el).FontSize, "font size must stay fixed when autofit is disabled");
        }
    }
}
