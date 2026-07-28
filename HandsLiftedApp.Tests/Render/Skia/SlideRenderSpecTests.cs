// HandsLiftedApp.Tests/Render/Skia/SlideRenderSpecTests.cs
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;
using HandsLiftedApp.Core.Render.Skia;

namespace HandsLiftedApp.Tests.Render.Skia;

[TestClass]
public class SlideRenderSpecTests
{
    [TestMethod]
    public void TransparentBackground_IsDistinctFromSolid()
    {
        BackgroundSpec transparent = new TransparentBackground();
        BackgroundSpec solid = new SolidBackground(SKColors.Black);
        Assert.AreNotEqual(transparent, solid);
    }

    [TestMethod]
    public void TextLineElement_IdentityIsText()
    {
        var a = new TextLineElement("Hello", SKRect.Empty, SKTypeface.Default, 100f, SKColors.White, null);
        var b = new TextLineElement("Hello", new SKRect(10, 20, 300, 120), SKTypeface.Default, 80f, SKColors.Red, null);
        Assert.AreEqual(a.Text, b.Text);
    }

    [TestMethod]
    public void SlideRenderSpec_StoresElementsAndBackground()
    {
        var elements = new List<RenderElement>
        {
            new TextLineElement("Line one", SKRect.Empty, SKTypeface.Default, 100f, SKColors.White, null)
        };
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Black), elements);

        Assert.AreEqual(1, spec.Elements.Count);
        Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
    }

    [TestMethod]
    public void MultiRunTextLineElement_IdentityIsConcatenatedRunText()
    {
        var runsA = new[] { new TextRun("13", 60f, -20f), new TextRun("Be sober-minded", 100f, 0f) };
        var runsB = new[] { new TextRun("13", 60f, -20f), new TextRun("Be sober-minded", 100f, 0f) };
        var a = new MultiRunTextLineElement(runsA, SKRect.Empty, SKTypeface.Default, SKColors.White, null);
        var b = new MultiRunTextLineElement(runsB, new SKRect(10, 20, 300, 120), SKTypeface.Default, SKColors.Red, null);

        Assert.AreEqual(
            string.Concat(a.Runs.Select(r => r.Text)),
            string.Concat(b.Runs.Select(r => r.Text)));
    }

    [TestMethod]
    public void SlideRenderSpec_StoresMultiRunTextLineElement()
    {
        var runs = new[] { new TextRun("Line one", 100f, 0f) };
        var elements = new List<RenderElement>
        {
            new MultiRunTextLineElement(runs, SKRect.Empty, SKTypeface.Default, SKColors.White, null)
        };
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Black), elements);

        Assert.AreEqual(1, spec.Elements.Count);
        Assert.IsInstanceOfType(spec.Elements[0], typeof(MultiRunTextLineElement));
    }
}
