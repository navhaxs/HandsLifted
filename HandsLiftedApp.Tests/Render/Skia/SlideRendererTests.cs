// HandsLiftedApp.Tests/Render/Skia/SlideRendererTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;
using HandsLiftedApp.Core.Render.Skia;

namespace HandsLiftedApp.Tests.Render.Skia;

[TestClass]
public class SlideRendererTests
{
    [TestMethod]
    public void RenderToSKBitmap_ReturnsBitmapWithRequestedDimensions()
    {
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Black), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 320, 180);

        Assert.AreEqual(320, bitmap.Width);
        Assert.AreEqual(180, bitmap.Height);
    }

    [TestMethod]
    public void RenderToSKBitmap_SolidBackground_FillsCanvas()
    {
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Red), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 100, 100);
        var pixel = bitmap.GetPixel(0, 0);

        Assert.AreEqual(255, pixel.Red);
        Assert.AreEqual(0, pixel.Green);
        Assert.AreEqual(0, pixel.Blue);
    }

    [TestMethod]
    public void RenderToSKBitmap_TransparentBackground_PixelIsTransparent()
    {
        var spec = new SlideRenderSpec(new TransparentBackground(), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 100, 100);
        var pixel = bitmap.GetPixel(0, 0);

        Assert.AreEqual(0, pixel.Alpha);
    }

    [TestMethod]
    public void RenderToSKBitmap_WithPreviousSpec_DoesNotThrow()
    {
        var prev = new SlideRenderSpec(new SolidBackground(SKColors.Blue), Array.Empty<RenderElement>());
        var curr = new SlideRenderSpec(new SolidBackground(SKColors.Green), Array.Empty<RenderElement>());

        // Should not throw at any transition progress
        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 100, 100, prev, 0.5f);

        Assert.IsNotNull(bitmap);
    }
}
