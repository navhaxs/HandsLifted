using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Avalonia.Media;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Tests.Render.Skia.Builders;

[TestClass]
public class ScriptureParagraphSpecBuilderTests
{
    private static BaseSlideTheme MakeTheme() => new BaseSlideTheme
    {
        FontSize = 60,
        TextColour = Colors.White,
        BackgroundColour = Colors.Black,
    };

    private static ScriptureParagraphLine HeaderLine(string text) =>
        new ScriptureParagraphLine(new[] { new ScriptureParagraphRun(text, IsSuperscript: false) }, IsHeader: true);

    private static ScriptureParagraphLine BodyLine(params ScriptureParagraphRun[] runs) =>
        new ScriptureParagraphLine(runs, IsHeader: false);

    [TestMethod]
    public void Build_WithTheme_ReturnsSolidBackground()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = MakeTheme() };
        slide.Lines = new[] { HeaderLine("Genesis 1:1") };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
    }

    [TestMethod]
    public void Build_NoTheme_ReturnsEmptyElements()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = null };
        slide.Lines = new[] { HeaderLine("Genesis 1:1") };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }

    [TestMethod]
    public void Build_NoLines_ReturnsEmptyElements()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = MakeTheme() };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }

    [TestMethod]
    public void Build_OneLineWithMarkerAndText_ReturnsOneMultiRunElementWithTwoRuns()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = MakeTheme() };
        slide.Lines = new[]
        {
            HeaderLine("Genesis 1:1"),
            BodyLine(
                new ScriptureParagraphRun("1", IsSuperscript: true),
                new ScriptureParagraphRun("In the beginning God created the heaven and the earth.", IsSuperscript: false))
        };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        Assert.AreEqual(2, spec.Elements.Count, "one element for the header line, one for the body line");
        var bodyElement = (MultiRunTextLineElement)spec.Elements[1];
        Assert.AreEqual(2, bodyElement.Runs.Count);
        Assert.AreEqual("1", bodyElement.Runs[0].Text);
        Assert.IsTrue(bodyElement.Runs[0].FontSize < bodyElement.Runs[1].FontSize, "superscript run must be smaller than body run");
        Assert.IsTrue(bodyElement.Runs[0].BaselineOffsetY < 0f, "superscript run must be raised (negative offset)");
    }

    [TestMethod]
    public void Build_HeaderLine_UsesLargerFontThanBodyLine()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = MakeTheme() };
        slide.Lines = new[]
        {
            HeaderLine("Genesis 1:1"),
            BodyLine(new ScriptureParagraphRun("Body text", IsSuperscript: false))
        };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        var headerElement = (MultiRunTextLineElement)spec.Elements[0];
        var bodyElement = (MultiRunTextLineElement)spec.Elements[1];
        Assert.IsTrue(headerElement.Runs[0].FontSize > bodyElement.Runs[0].FontSize);
    }

    // The design spec calls for the header to render bold while body text uses the theme's
    // plain weight. SKTypeface doesn't expose an easy way to assert "is bold" back out, so the
    // closest structurally-checkable proxy is: the header element's typeface must be a distinct
    // instance from the body element's typeface, proving they came from separate GetBoldTypeface
    // / GetTypeface construction calls rather than sharing one (which would mean both got the
    // same weight).
    [TestMethod]
    public void Build_HeaderLine_UsesDistinctTypefaceInstanceFromBodyLine()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = MakeTheme() };
        slide.Lines = new[]
        {
            HeaderLine("Genesis 1:1"),
            BodyLine(new ScriptureParagraphRun("Body text", IsSuperscript: false))
        };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        var headerElement = (MultiRunTextLineElement)spec.Elements[0];
        var bodyElement = (MultiRunTextLineElement)spec.Elements[1];
        Assert.AreNotSame(headerElement.Typeface, bodyElement.Typeface,
            "header should get a separately-constructed (bold) typeface, not the body's typeface instance");
    }

    // Exercises the full Build -> SlideRenderer.RenderToSKBitmap path, which the structural
    // assertions above never touch: they only inspect the returned SlideRenderSpec, never render
    // it. This is the path where a stale/disposed SKTypeface stored on a RenderElement (the bug
    // this builder was fixed for -- see git history) would actually surface, since
    // SlideRenderer.DrawMultiRunTextElement constructs a new SKFont from element.Typeface at draw
    // time. Confirmed real coverage: this test was written and run against the pre-fix builder
    // (single `using`-scoped measurement typeface shared across every element) before being run
    // against the fix.
    [TestMethod]
    public void Build_ThenRenderToSKBitmap_DoesNotThrowAndProducesBitmap()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = MakeTheme() };
        slide.Lines = new[]
        {
            HeaderLine("Genesis 1:1"),
            BodyLine(
                new ScriptureParagraphRun("1", IsSuperscript: true),
                new ScriptureParagraphRun("In the beginning God created the heaven and the earth.", IsSuperscript: false))
        };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec);

        Assert.IsNotNull(bitmap);
        Assert.AreEqual(1920, bitmap.Width);
        Assert.AreEqual(1080, bitmap.Height);
    }
}
