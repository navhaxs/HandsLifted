using System;
using Avalonia.Media;
using HandsLiftedApp.Data.Models.SlideElement;
using SkiaSharp;
using CustomSlideModel = HandsLiftedApp.Data.Data.Models.Slides.CustomSlide;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

/// <summary>Draws a custom slide (background, then elements in true z-order) onto a canvas in slide pixels.</summary>
public static class CustomSlideDrawer
{
    public const int CanvasWidth = 1920;
    public const int CanvasHeight = 1080;

    /// <summary>
    /// Draws onto <paramref name="canvas"/> without clearing it. <c>SlideElements[0]</c> is frontmost, so elements are
    /// drawn last to first. <paramref name="hideTextOf"/> skips that element's glyphs but still draws its fill.
    /// </summary>
    public static void Draw(SKCanvas canvas, CustomSlideModel slide, TextElement? hideTextOf = null)
    {
        DrawBackground(canvas, slide);

        for (int i = slide.SlideElements.Count - 1; i >= 0; i--)
        {
            switch (slide.SlideElements[i])
            {
                case ImageElement img:
                    DrawImage(canvas, img);
                    break;
                case TextElement txt:
                    DrawText(canvas, txt, hidden: ReferenceEquals(txt, hideTextOf));
                    break;
            }
        }
    }

    private static void DrawBackground(SKCanvas canvas, CustomSlideModel slide)
    {
        if (!string.IsNullOrWhiteSpace(slide.BackgroundGraphicFilePath))
        {
            var bmp = SlideImageCache.Get(slide.BackgroundGraphicFilePath);
            if (bmp != null)
            {
                canvas.DrawBitmap(bmp, new SKRect(0, 0, CanvasWidth, CanvasHeight));
                return;
            }
        }

        using var bgPaint = new SKPaint { Color = ToSkColor(slide.BackgroundAvaloniaColour) };
        canvas.DrawRect(0, 0, CanvasWidth, CanvasHeight, bgPaint);
    }

    private static void DrawImage(SKCanvas canvas, ImageElement el)
    {
        if (string.IsNullOrWhiteSpace(el.FilePath)) return;
        var bmp = SlideImageCache.Get(el.FilePath);
        if (bmp == null) return;

        var dest = new SKRect(el.X, el.Y, el.X + el.Width, el.Y + el.Height);
        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
        canvas.DrawBitmap(bmp, dest, paint);
    }

    private static void DrawText(SKCanvas canvas, TextElement el, bool hidden)
    {
        var bgColor = ToSkColor(el.BackgroundAvaloniaColour);
        if (bgColor.Alpha > 0)
        {
            using var bgPaint = new SKPaint { Color = bgColor };
            canvas.DrawRect(el.X, el.Y, el.Width, el.Height, bgPaint);
        }

        if (hidden || string.IsNullOrWhiteSpace(el.Text)) return;

        // Fresh typeface per element; disposed with the font (see CLAUDE.md SkiaSharp notes).
        using var font = CustomSlideTextLayout.CreateFont(el);
        try
        {
            var lines = CustomSlideTextLayout.Layout(el, font);
            using var paint = new SKPaint { IsAntialias = true, Color = ToSkColor(el.ForegroundAvaloniaColour) };

            font.GetFontMetrics(out var metrics);
            float underlineThickness = metrics.UnderlineThickness is { } t && t > 0 ? t : el.FontSize * 0.08f;
            float underlineOffset = metrics.UnderlinePosition is { } p ? p : el.FontSize * 0.1f;

            canvas.Save();
            canvas.ClipRect(new SKRect(el.X, el.Y, el.X + el.Width, el.Y + el.Height));
            foreach (var line in lines)
            {
                canvas.DrawText(line.Text, line.X, line.Baseline, font, paint);
                if (el.Underline && line.Width > 0)
                {
                    float top = line.Baseline + underlineOffset;
                    canvas.DrawRect(line.X, top, line.Width, underlineThickness, paint);
                }
            }
            canvas.Restore();
        }
        finally
        {
            font.Typeface?.Dispose();
        }
    }

    private static SKColor ToSkColor(Color color) => new(color.R, color.G, color.B, color.A);
}
