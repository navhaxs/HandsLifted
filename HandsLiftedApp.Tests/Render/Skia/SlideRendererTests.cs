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

    // ── Drawing small (live preview) ───────────────────────────────────────────

    private static SKBitmap Checkerboard(int width, int height)
    {
        var bmp = new SKBitmap(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bmp.SetPixel(x, y, (x + y) % 2 == 0 ? SKColors.Black : SKColors.White);
        return bmp;
    }

    // Draws a spec the way SlideCanvas does in a Viewbox: 1920x1080 layout through a canvas scale.
    private static SKBitmap DrawScaled(SlideRenderSpec spec, float scale)
    {
        var info = new SKImageInfo((int)(1920 * scale), (int)(1080 * scale), SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Black);
        surface.Canvas.Scale(scale);
        SlideRenderer.Draw(surface.Canvas, spec, null, 1f, 1920, 1080);
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    // A page of small black text on white: the kind of content that aliases when shrunk.
    private static SKBitmap TextLikePage()
    {
        var bmp = new SKBitmap(1920, 1080);
        bmp.Erase(SKColors.White);
        using var canvas = new SKCanvas(bmp);
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 34);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        var random = new Random(7);
        var words = new[] { "Donations", "BSB", "transfer", "account", "reference", "youth", "please", "include", "name" };
        for (var y = 40; y < 1060; y += 46)
            for (var x = 40f; x < 1800;)
            {
                var word = words[random.Next(words.Length)];
                canvas.DrawText(word, x, y, font, paint);
                x += font.MeasureText(word) + 14;
            }
        return bmp;
    }

    // Root-mean-square difference between `drawn` and the exact area average of the source pixels each output pixel covers.
    private static double ErrorVersusExactAreaAverage(SKBitmap drawn, SKBitmap source, float scale)
    {
        double sum = 0;
        long count = 0;
        for (var y = 2; y < drawn.Height - 2; y++)
            for (var x = 2; x < drawn.Width - 2; x++)
            {
                double x0 = x / scale, x1 = (x + 1) / scale, y0 = y / scale, y1 = (y + 1) / scale;
                double total = 0, weights = 0;
                for (var sy = (int)Math.Floor(y0); sy < Math.Ceiling(y1) && sy < source.Height; sy++)
                    for (var sx = (int)Math.Floor(x0); sx < Math.Ceiling(x1) && sx < source.Width; sx++)
                    {
                        var weight = (Math.Min(x1, sx + 1) - Math.Max(x0, sx)) * (Math.Min(y1, sy + 1) - Math.Max(y0, sy));
                        total += source.GetPixel(sx, sy).Red * weight;
                        weights += weight;
                    }
                var diff = drawn.GetPixel(x, y).Red - total / weights;
                sum += diff * diff;
                count++;
            }
        return Math.Sqrt(sum / count);
    }

    [TestMethod]
    public void Draw_BitmapBackgroundShownMuchSmaller_IsCloseToAnExactAreaAverage()
    {
        DownscaledBitmapCache.Clear();
        using var source = TextLikePage();
        var spec = new SlideRenderSpec(new SkiaBitmapBackground(source), Array.Empty<RenderElement>());

        using var drawn = DrawScaled(spec, 0.2f);

        // Measured on this page at 0.2: letting the canvas shrink it by itself gives an error of about 41,
        // the cached area-averaged copy about 18. The limit sits between the two.
        var error = ErrorVersusExactAreaAverage(drawn, source, 0.2f);
        Assert.IsTrue(error < 28, $"error vs exact area average was {error:F1}; the canvas's own shrink gives about 41");
    }

    [TestMethod]
    public void Draw_BitmapBackgroundAtFullSize_IsNotShrunkOrCached()
    {
        DownscaledBitmapCache.Clear();
        using var source = SolidBitmap(SKColors.Red);
        var spec = new SlideRenderSpec(new SkiaBitmapBackground(source), Array.Empty<RenderElement>());

        using var drawn = DrawScaled(spec, 1f);

        Assert.AreEqual(0, DownscaledBitmapCache.Count);
        Assert.AreEqual(255, drawn.GetPixel(100, 100).Red);
    }

    [TestMethod]
    public void Draw_SameBitmapShownSmallRepeatedly_ReusesOneCachedCopy()
    {
        DownscaledBitmapCache.Clear();
        using var source = Checkerboard(1920, 1080);
        var spec = new SlideRenderSpec(new SkiaBitmapBackground(source), Array.Empty<RenderElement>());

        for (var frame = 0; frame < 5; frame++)
        {
            using var drawn = DrawScaled(spec, 0.125f);
        }

        Assert.AreEqual(1, DownscaledBitmapCache.Count);
    }

    [TestMethod]
    public void DownscaledBitmapCache_KeepsAtMostEightCopies()
    {
        DownscaledBitmapCache.Clear();
        using var source = SolidBitmap(SKColors.Green);
        var big = new SKBitmap(64, 64);
        big.Erase(SKColors.Green);

        for (var size = 8; size < 8 + 12; size++)
            Assert.IsNotNull(DownscaledBitmapCache.GetOrCreate(big, size, size));

        Assert.AreEqual(8, DownscaledBitmapCache.Count);
        big.Dispose();
    }

    // ── Thumbnails ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void BitmapScaling_Downscale_AveragesFineDetailInsteadOfSkippingIt()
    {
        // 1px black/white checkerboard: a faithful 4:1 reduction is mid grey everywhere. A filter that only samples
        // a few source pixels lands on pure black or white (aliasing), which is what small text turns into.
        using var source = new SKBitmap(400, 400);
        for (var y = 0; y < 400; y++)
            for (var x = 0; x < 400; x++)
                source.SetPixel(x, y, (x + y) % 2 == 0 ? SKColors.Black : SKColors.White);

        using var scaled = BitmapScaling.Downscale(source, 100, 100);

        Assert.IsNotNull(scaled);
        foreach (var (x, y) in new[] { (10, 10), (50, 50), (51, 50), (90, 33) })
        {
            var red = scaled.GetPixel(x, y).Red;
            Assert.IsTrue(red is > 110 and < 145, $"({x},{y}) expected mid grey, got {red}");
        }
    }

    [TestMethod]
    public void RenderThumbnail_ReturnsRequestedWidthAt16By9()
    {
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Black), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderThumbnail(spec, 320);

        Assert.AreEqual(320, bitmap.Width);
        Assert.AreEqual(180, bitmap.Height);
    }

    [TestMethod]
    public void RenderThumbnail_SolidBackground_FillsWholeThumbnail()
    {
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Red), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderThumbnail(spec, 160);

        foreach (var (x, y) in new[] { (0, 0), (159, 0), (0, 89), (159, 89), (80, 45) })
        {
            var pixel = bitmap.GetPixel(x, y);
            Assert.AreEqual(255, pixel.Red, $"({x},{y})");
            Assert.AreEqual(0, pixel.Green, $"({x},{y})");
            Assert.AreEqual(0, pixel.Blue, $"({x},{y})");
        }
    }

    [TestMethod]
    public void RenderThumbnail_TransparentBackground_CompositesOverOpaqueBlack()
    {
        var spec = new SlideRenderSpec(new TransparentBackground(), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderThumbnail(spec, 160);
        var pixel = bitmap.GetPixel(10, 10);

        Assert.AreEqual(255, pixel.Alpha);
        Assert.AreEqual(0, pixel.Red);
        Assert.AreEqual(0, pixel.Green);
        Assert.AreEqual(0, pixel.Blue);
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
