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

    [TestMethod]
    public void Draw_UnchangedLine_StaysAtFullAlpha()
    {
        // A line present in both prev and current should always be alpha=1 (full opacity)
        var sharedElement = new TextLineElement(
            "Same line",
            new SKRect(0, 0, 400, 120),
            SKTypeface.Default, 80f, SKColors.White, null);

        var prev = new SlideRenderSpec(new SolidBackground(SKColors.Black),
            new[] { sharedElement });
        var curr = new SlideRenderSpec(new SolidBackground(SKColors.Black),
            new[] { sharedElement });

        // At mid-transition, unchanged line must remain visible
        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 400, 120, prev, 0.5f);

        // At least one pixel in the text area should be non-black (white text at full alpha)
        bool hasWhitePixel = false;
        for (int x = 0; x < bitmap.Width && !hasWhitePixel; x++)
            for (int y = 0; y < bitmap.Height && !hasWhitePixel; y++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red > 200 && px.Green > 200 && px.Blue > 200)
                    hasWhitePixel = true;
            }
        Assert.IsTrue(hasWhitePixel, "Unchanged line should render at full opacity at mid-transition");
    }

    [TestMethod]
    public void Draw_NewLine_InvisibleAtProgressZero()
    {
        // A line only in current (not in previous) should be invisible when progress=0
        var newElement = new TextLineElement(
            "Brand new line",
            new SKRect(0, 0, 400, 120),
            SKTypeface.Default, 80f, SKColors.White, null);

        var prev = new SlideRenderSpec(new TransparentBackground(), Array.Empty<RenderElement>());
        var curr = new SlideRenderSpec(new TransparentBackground(),
            new[] { newElement });

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 400, 120, prev, 0f);

        // All pixels should be transparent (new line at progress=0 is invisible)
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
                Assert.AreEqual(0, bitmap.GetPixel(x, y).Alpha,
                    $"Pixel at ({x},{y}) should be transparent; new line at progress=0 must be invisible");
    }

    [TestMethod]
    public void Draw_RemovedLine_InvisibleAtProgressOne()
    {
        // A line only in previous (not in current) should be invisible when progress=1
        var removedElement = new TextLineElement(
            "Old line",
            new SKRect(0, 0, 400, 120),
            SKTypeface.Default, 80f, SKColors.White, null);

        var prev = new SlideRenderSpec(new TransparentBackground(),
            new[] { removedElement });
        var curr = new SlideRenderSpec(new TransparentBackground(), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 400, 120, prev, 1f);

        // All pixels should be transparent (removed line at progress=1 is invisible)
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
                Assert.AreEqual(0, bitmap.GetPixel(x, y).Alpha,
                    $"Pixel at ({x},{y}) should be transparent; removed line at progress=1 must be invisible");
    }
}
