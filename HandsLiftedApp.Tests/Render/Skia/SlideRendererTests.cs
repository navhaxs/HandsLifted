// HandsLiftedApp.Tests/Render/Skia/SlideRendererTests.cs
using System.IO;
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

    // ── Background colour + opacity ────────────────────────────────────────────

    private static SKBitmap SolidBitmap(SKColor color)
    {
        var bmp = new SKBitmap(2, 2);
        bmp.Erase(color);
        return bmp;
    }

    [TestMethod]
    public void SkiaBitmapBackground_HalfOpacityOverBlue_BlendsEvenly()
    {
        using var red = SolidBitmap(SKColors.Red);
        var spec = new SlideRenderSpec(new SkiaBitmapBackground(red, SKColors.Blue, 0.5f), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 100, 100);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(128, pixel.Red, 2);
        Assert.AreEqual(128, pixel.Blue, 2);
        Assert.AreEqual(255, pixel.Alpha);
    }

    [TestMethod]
    public void SkiaBitmapBackground_FullOpacity_ShowsOnlyBitmap()
    {
        using var red = SolidBitmap(SKColors.Red);
        var spec = new SlideRenderSpec(new SkiaBitmapBackground(red, SKColors.Blue, 1f), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 100, 100);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(255, pixel.Red);
        Assert.AreEqual(0, pixel.Blue);
    }

    [TestMethod]
    public void SkiaBitmapBackground_ZeroOpacity_ShowsOnlyBackColor()
    {
        using var red = SolidBitmap(SKColors.Red);
        var spec = new SlideRenderSpec(new SkiaBitmapBackground(red, SKColors.Blue, 0f), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 100, 100);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(0, pixel.Red);
        Assert.AreEqual(255, pixel.Blue);
    }

    [TestMethod]
    public void SkiaBitmapBackground_NoBackColor_DrawsBitmapUnchanged()
    {
        using var red = SolidBitmap(SKColors.Red);
        var spec = new SlideRenderSpec(new SkiaBitmapBackground(red), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 100, 100);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(255, pixel.Red);
        Assert.AreEqual(0, pixel.Blue);
    }

    [TestMethod]
    public void ImageBackground_HalfOpacityOverBlue_BlendsEvenly()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            using (var red = SolidBitmap(SKColors.Red))
            using (var image = SKImage.FromBitmap(red))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                File.WriteAllBytes(path, data.ToArray());

            var spec = new SlideRenderSpec(new ImageBackground(path, SKColors.Blue, 0.5f), Array.Empty<RenderElement>());

            using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 100, 100);
            var pixel = bitmap.GetPixel(50, 50);

            Assert.AreEqual(128, pixel.Red, 2);
            Assert.AreEqual(128, pixel.Blue, 2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ImageBackground_MissingFile_WithBackColor_DrawsBackColorAndDoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png"); // never created
        var spec = new SlideRenderSpec(new ImageBackground(path, SKColors.Red, 0.5f), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 100, 100);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(255, pixel.Red);
        Assert.AreEqual(0, pixel.Blue);
    }

    // A colour+image background is ONE composite: during a transition it must fade as a group, so the result
    // is a straight cross-fade between the two composited backgrounds. Fading the colour layer and the image
    // layer separately lets the colour layer darken/tint the picture mid-fade.

    [TestMethod]
    public void Transition_FromTransparent_FadesBackgroundAsOneGroup()
    {
        // Composite = red at 50% over blue = (128, 0, 128), opaque. Faded in at 0.5 -> same colour, alpha ~128.
        using var red = SolidBitmap(SKColors.Red);
        var prev = new SlideRenderSpec(new TransparentBackground(), Array.Empty<RenderElement>());
        var curr = new SlideRenderSpec(new SkiaBitmapBackground(red, SKColors.Blue, 0.5f), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 100, 100, prev, 0.5f);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(128, pixel.Red, 3);
        Assert.AreEqual(128, pixel.Blue, 3);
        Assert.AreEqual(128, pixel.Alpha, 3);
    }

    [TestMethod]
    public void Transition_FullImageToFullImage_IsLinearCrossfade_NoDarkDip()
    {
        // Black back colour under both. Straight cross-fade at 0.5 = half green + half red = (128, 128, 0).
        using var green = SolidBitmap(SKColors.Lime);
        using var red = SolidBitmap(SKColors.Red);
        var prev = new SlideRenderSpec(new SkiaBitmapBackground(green, SKColors.Black, 1f), Array.Empty<RenderElement>());
        var curr = new SlideRenderSpec(new SkiaBitmapBackground(red, SKColors.Black, 1f), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 100, 100, prev, 0.5f);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(128, pixel.Red, 3);
        Assert.AreEqual(128, pixel.Green, 3);
        Assert.AreEqual(255, pixel.Alpha);
    }

    [TestMethod]
    public void Transition_DimmedImageToFullImage_IsLinearCrossfadeOfComposites()
    {
        // prev composite: lime at 50% over blue = (0, 128, 128). curr composite: red = (255, 0, 0).
        // Straight cross-fade at 0.5 = (128, 64, 64).
        using var green = SolidBitmap(SKColors.Lime);
        using var red = SolidBitmap(SKColors.Red);
        var prev = new SlideRenderSpec(new SkiaBitmapBackground(green, SKColors.Blue, 0.5f), Array.Empty<RenderElement>());
        var curr = new SlideRenderSpec(new SkiaBitmapBackground(red, SKColors.Blue, 1f), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 100, 100, prev, 0.5f);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(128, pixel.Red, 3);
        Assert.AreEqual(64, pixel.Green, 3);
        Assert.AreEqual(64, pixel.Blue, 3);
    }

    [TestMethod]
    public void Transition_FullImageToDimmedImage_IsLinearCrossfadeOfComposites()
    {
        // prev composite: red = (255, 0, 0). curr composite: lime at 50% over blue = (0, 128, 128).
        // Straight cross-fade at 0.5 = (128, 64, 64).
        using var green = SolidBitmap(SKColors.Lime);
        using var red = SolidBitmap(SKColors.Red);
        var prev = new SlideRenderSpec(new SkiaBitmapBackground(red, SKColors.Blue, 1f), Array.Empty<RenderElement>());
        var curr = new SlideRenderSpec(new SkiaBitmapBackground(green, SKColors.Blue, 0.5f), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 100, 100, prev, 0.5f);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(128, pixel.Red, 3);
        Assert.AreEqual(64, pixel.Green, 3);
        Assert.AreEqual(64, pixel.Blue, 3);
    }

    [TestMethod]
    public void Transition_DimmedImageToTransparent_FadesOutAsOneGroup()
    {
        // Composite = red at 50% over blue = (128, 0, 128); fading out at 0.5 keeps that colour at alpha ~128.
        using var red = SolidBitmap(SKColors.Red);
        var prev = new SlideRenderSpec(new SkiaBitmapBackground(red, SKColors.Blue, 0.5f), Array.Empty<RenderElement>());
        var curr = new SlideRenderSpec(new TransparentBackground(), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 100, 100, prev, 0.5f);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(128, pixel.Red, 3);
        Assert.AreEqual(128, pixel.Blue, 3);
        Assert.AreEqual(128, pixel.Alpha, 3);
    }

    // A motion theme below 100% yields a translucent SolidBackground drawn over the video layer. The outgoing
    // background must fade OUT beneath it; if it stayed at full strength it would pop away at progress 1.

    [TestMethod]
    public void Transition_OpaqueToTranslucentSolid_FadesPreviousOut()
    {
        // prev red at 0.5 -> premul R .498; curr blue alpha 128 at 0.5 -> .251. Over: R .373, B .251, A .624.
        var prev = new SlideRenderSpec(new SolidBackground(SKColors.Red), Array.Empty<RenderElement>());
        var curr = new SlideRenderSpec(new SolidBackground(SKColors.Blue.WithAlpha(128)), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 100, 100, prev, 0.5f);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(152, pixel.Red, 4);
        Assert.AreEqual(103, pixel.Blue, 4);
        Assert.AreEqual(159, pixel.Alpha, 4);
    }

    [TestMethod]
    public void Transition_OpaqueToTranslucentSolid_NearEnd_LeavesNoPreviousBackground()
    {
        var prev = new SlideRenderSpec(new SolidBackground(SKColors.Red), Array.Empty<RenderElement>());
        var curr = new SlideRenderSpec(new SolidBackground(SKColors.Blue.WithAlpha(128)), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 100, 100, prev, 0.99f);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.IsTrue(pixel.Red < 10, $"previous background must be (almost) gone just before the end, red was {pixel.Red}");
    }

    [TestMethod]
    public void Transition_TranslucentToTranslucent_DoesNotStackTints()
    {
        // Both tints at 0.5 progress: prev alpha 76, curr alpha 76 -> total alpha ~0.507 (129). Stacking the
        // previous at full strength would give ~0.72 (183).
        var prev = new SlideRenderSpec(new SolidBackground(SKColors.Blue.WithAlpha(153)), Array.Empty<RenderElement>());
        var curr = new SlideRenderSpec(new SolidBackground(SKColors.Red.WithAlpha(153)), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 100, 100, prev, 0.5f);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.AreEqual(129, pixel.Alpha, 4);
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
    public void RenderToSKBitmap_MultiRunTextLineElement_DrawsBothRuns()
    {
        // A superscript marker run and a regular-size text run on the same line —
        // both should paint white pixels somewhere in the bounds.
        var runs = new[]
        {
            new TextRun("13", 48f, -20f),
            new TextRun("Be sober-minded", 80f, 0f)
        };
        var element = new MultiRunTextLineElement(runs, new SKRect(0, 0, 600, 120), SKTypeface.Default, SKColors.White, null);
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Black), new[] { element });

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 600, 120);

        bool hasWhitePixel = false;
        for (int x = 0; x < bitmap.Width && !hasWhitePixel; x++)
            for (int y = 0; y < bitmap.Height && !hasWhitePixel; y++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red > 200 && px.Green > 200 && px.Blue > 200)
                    hasWhitePixel = true;
            }
        Assert.IsTrue(hasWhitePixel, "multi-run element should paint visible white text");
    }

    [TestMethod]
    public void Draw_UnchangedMultiRunLine_StaysAtFullAlpha()
    {
        var sharedElement = new MultiRunTextLineElement(
            new[] { new TextRun("Same line", 80f, 0f) },
            new SKRect(0, 0, 400, 120), SKTypeface.Default, SKColors.White, null);

        var prev = new SlideRenderSpec(new SolidBackground(SKColors.Black), new[] { sharedElement });
        var curr = new SlideRenderSpec(new SolidBackground(SKColors.Black), new[] { sharedElement });

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 400, 120, prev, 0.5f);

        bool hasWhitePixel = false;
        for (int x = 0; x < bitmap.Width && !hasWhitePixel; x++)
            for (int y = 0; y < bitmap.Height && !hasWhitePixel; y++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red > 200 && px.Green > 200 && px.Blue > 200)
                    hasWhitePixel = true;
            }
        Assert.IsTrue(hasWhitePixel, "unchanged multi-run line should render at full opacity at mid-transition");
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
