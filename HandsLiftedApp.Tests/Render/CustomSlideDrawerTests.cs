using Avalonia.Media;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.Models.SlideElement;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;
using CustomSlideModel = HandsLiftedApp.Data.Data.Models.Slides.CustomSlide;

namespace HandsLiftedApp.Tests.Render;

[TestClass]
public class CustomSlideDrawerTests
{
    private static readonly Color Bg = Color.Parse("#112233");
    private static readonly Color Fill = Color.Parse("#ff0000");

    private static CustomSlideModel NewSlide() => new() { BackgroundAvaloniaColour = Bg };

    private static TextElement Box(string text, int x = 100, int y = 100, int w = 600, int h = 300, Color? fill = null) => new()
    {
        X = x, Y = y, Width = w, Height = h, Text = text, FontSize = 60,
        BackgroundAvaloniaColour = fill ?? Fill,
        ForegroundAvaloniaColour = Color.Parse("#ffffff"),
    };

    private static SKBitmap Render(CustomSlideModel slide, TextElement? hide = null)
    {
        var bmp = new SKBitmap(1920, 1080);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Black);
        CustomSlideDrawer.Draw(canvas, slide, hide);
        return bmp;
    }

    private static SKColor Sk(Color c) => new(c.R, c.G, c.B, c.A);

    private static int CountNonFill(SKBitmap bmp, int x, int y, int w, int h, SKColor fill)
    {
        int n = 0;
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                if (bmp.GetPixel(xx, yy) != fill) n++;
        return n;
    }

    [TestMethod]
    public void EmptySlide_DrawsOnlyBackground()
    {
        using var bmp = Render(NewSlide());
        Assert.AreEqual(Sk(Bg), bmp.GetPixel(10, 10));
        Assert.AreEqual(Sk(Bg), bmp.GetPixel(1900, 1070));
    }

    [TestMethod]
    public void TextBoxFill_IsDrawnAtElementRect()
    {
        var slide = NewSlide();
        slide.SlideElements.Add(Box("", 100, 100, 600, 300));
        using var bmp = Render(slide);
        Assert.AreEqual(Sk(Fill), bmp.GetPixel(101, 101));
        Assert.AreEqual(Sk(Fill), bmp.GetPixel(698, 398));
        Assert.AreEqual(Sk(Bg), bmp.GetPixel(50, 50));
        Assert.AreEqual(Sk(Bg), bmp.GetPixel(701, 401));
    }

    [TestMethod]
    public void WhitespaceText_DrawsFillButNoGlyphs()
    {
        var slide = NewSlide();
        slide.SlideElements.Add(Box("   "));
        using var bmp = Render(slide);
        Assert.AreEqual(0, CountNonFill(bmp, 100, 100, 600, 300, Sk(Fill)));
    }

    [TestMethod]
    public void HideTextOf_SkipsGlyphsButKeepsFill()
    {
        var slide = NewSlide();
        var el = Box("Hello world");
        slide.SlideElements.Add(el);
        using var hidden = Render(slide, el);
        using var shown = Render(slide);
        Assert.AreEqual(0, CountNonFill(hidden, 100, 100, 600, 300, Sk(Fill)));
        Assert.IsTrue(CountNonFill(shown, 100, 100, 600, 300, Sk(Fill)) > 0);
    }

    [TestMethod]
    public void Underline_AddsPixelsBelowBaseline()
    {
        var slide = NewSlide();
        var el = Box("Hello");
        slide.SlideElements.Add(el);
        float baseline;
        using (var font = CustomSlideTextLayout.CreateFont(el))
        {
            baseline = CustomSlideTextLayout.Layout(el, font)[0].Baseline;
            font.Typeface?.Dispose();
        }
        using var plain = Render(slide);
        el.Underline = true;
        using var underlined = Render(slide);

        int y0 = (int)baseline + 1, y1 = (int)baseline + (int)(el.FontSize * 0.25f);
        int plainCount = CountNonFill(plain, 100, y0, 600, y1 - y0, Sk(Fill));
        int underCount = CountNonFill(underlined, 100, y0, 600, y1 - y0, Sk(Fill));
        Assert.IsTrue(underCount > plainCount + 50, $"plain={plainCount} underlined={underCount}");
    }

    [TestMethod]
    public void Italic_ChangesRenderedPixels()
    {
        var slide = NewSlide();
        var el = Box("Hello");
        slide.SlideElements.Add(el);
        using var upright = Render(slide);
        el.Italic = true;
        using var italic = Render(slide);
        bool differs = false;
        for (int y = 100; y < 400 && !differs; y++)
            for (int x = 100; x < 700; x++)
                if (upright.GetPixel(x, y) != italic.GetPixel(x, y)) { differs = true; break; }
        Assert.IsTrue(differs);
    }

    [TestMethod]
    public void ZOrder_FrontElementWinsOverlap()
    {
        var blue = Color.Parse("#0000ff");
        var slide = NewSlide();
        slide.SlideElements.Add(Box("", 100, 100, 300, 300, blue)); // index 0 = front
        slide.SlideElements.Add(Box("", 200, 200, 300, 300, Fill));
        using var bmp = Render(slide);
        Assert.AreEqual(Sk(blue), bmp.GetPixel(300, 300));
        Assert.AreEqual(Sk(Fill), bmp.GetPixel(450, 450));
    }

    [TestMethod]
    public void MissingImageFile_IsSkippedWithoutThrowing()
    {
        var slide = NewSlide();
        slide.SlideElements.Add(new ImageElement { X = 10, Y = 10, Width = 100, Height = 100, FilePath = "C:\\definitely\\missing\\nope.png" });
        using var bmp = Render(slide);
        Assert.AreEqual(Sk(Bg), bmp.GetPixel(50, 50));
    }

    [TestMethod]
    public void SlideImageCache_Get_ReturnsNullForMissingFile_AndSameInstanceForRepeatedHit()
    {
        Assert.IsNull(SlideImageCache.Get("C:\\definitely\\missing\\nope.png"));

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cache-{System.Guid.NewGuid():N}.png");
        try
        {
            using (var src = new SKBitmap(4, 4))
            using (var img = SKImage.FromBitmap(src))
            using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
            using (var fs = System.IO.File.Create(path))
                data.SaveTo(fs);

            var a = SlideImageCache.Get(path);
            var b = SlideImageCache.Get(path);
            Assert.IsNotNull(a);
            Assert.AreSame(a, b);
            // immutable, so DrawBitmap does not deep-copy its pixels into the recorded picture on every redraw
            Assert.IsTrue(a.IsImmutable, "cached bitmaps are never written to");
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
