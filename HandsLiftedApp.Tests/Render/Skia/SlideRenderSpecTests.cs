// HandsLiftedApp.Tests/Render/Skia/SlideRenderSpecTests.cs
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
}
