// HandsLiftedApp.Core/Render/Skia/Builders/CustomSlideSpecBuilder.cs
using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Layout;
using Avalonia.Media;
using HandsLiftedApp.Data.Models.SlideElement;
using Serilog;
using SkiaSharp;
using CustomSlideModel = HandsLiftedApp.Data.Data.Models.Slides.CustomSlide;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

// Native SkiaSharp renderer for CustomSlide (element-based).
// Avoids off-screen Avalonia control rendering, which requires an active visual tree.
public static class CustomSlideSpecBuilder
{
    private const int CanvasWidth = 1920;
    private const int CanvasHeight = 1080;

    public static SlideRenderSpec? Build(CustomSlideModel slide)
    {
        try
        {
            var info = new SKImageInfo(CanvasWidth, CanvasHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            if (surface == null)
            {
                Log.Warning("[CustomSlideSpecBuilder] SKSurface.Create failed");
                return null;
            }

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            DrawBackground(canvas, slide);

            // Reverse matches CustomSlideRender.Render() z-order: SlideElements[0] is frontmost
            foreach (var element in slide.SlideElements)
            {
                if (element is ImageElement img)
                    DrawImageElement(canvas, img);
            }
            foreach (var element in slide.SlideElements)
            {
                if (element is TextElement txt)
                    DrawTextElement(canvas, txt);
            }

            using var skImage = surface.Snapshot();
            var bitmap = SKBitmap.FromImage(skImage);
            return new SlideRenderSpec(new SkiaBitmapBackground(bitmap), Array.Empty<RenderElement>());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CustomSlideSpecBuilder] Failed to render CustomSlide");
            return null;
        }
    }

    private static void DrawBackground(SKCanvas canvas, CustomSlideModel slide)
    {
        if (!string.IsNullOrWhiteSpace(slide.BackgroundGraphicFilePath))
        {
            Stream? imgStream = slide.BackgroundGraphicFilePath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
                ? Avalonia.Platform.AssetLoader.Open(new Uri(slide.BackgroundGraphicFilePath))
                : File.Exists(slide.BackgroundGraphicFilePath) ? File.OpenRead(slide.BackgroundGraphicFilePath) : null;

            if (imgStream != null)
            {
                using (imgStream)
                using (var bmp = SKBitmap.Decode(imgStream))
                {
                    if (bmp != null)
                    {
                        canvas.DrawBitmap(bmp, new SKRect(0, 0, CanvasWidth, CanvasHeight));
                        return;
                    }
                }
            }
        }

        // Fall back to solid background color
        var bg = ToSkColor(slide.BackgroundAvaloniaColour);
        using var bgPaint = new SKPaint { Color = bg };
        canvas.DrawRect(0, 0, CanvasWidth, CanvasHeight, bgPaint);
    }

    private static void DrawImageElement(SKCanvas canvas, ImageElement el)
    {
        if (string.IsNullOrWhiteSpace(el.FilePath)) return;

        Stream? imgStream = el.FilePath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
            ? Avalonia.Platform.AssetLoader.Open(new Uri(el.FilePath))
            : File.Exists(el.FilePath) ? File.OpenRead(el.FilePath) : null;

        if (imgStream == null) return;

        using (imgStream)
        using (var bmp = SKBitmap.Decode(imgStream))
        {
            if (bmp == null) return;
            var dest = new SKRect(el.X, el.Y, el.X + el.Width, el.Y + el.Height);
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
            canvas.DrawBitmap(bmp, dest, paint);
        }
    }

    private static void DrawTextElement(SKCanvas canvas, TextElement el)
    {
        // Background rect
        var bgColor = ToSkColor(el.BackgroundAvaloniaColour);
        if (bgColor.Alpha > 0)
        {
            using var bgPaint = new SKPaint { Color = bgColor };
            canvas.DrawRect(el.X, el.Y, el.Width, el.Height, bgPaint);
        }

        if (string.IsNullOrWhiteSpace(el.Text)) return;

        var weight = el.FontWeight == FontWeight.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        using var typeface = SKTypeface.FromFamilyName(el.FontFamilyAsText, weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                             ?? SKTypeface.Default;
        using var font = new SKFont(typeface, el.FontSize)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Subpixel = true,
        };
        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = ToSkColor(el.ForegroundAvaloniaColour);

        font.GetFontMetrics(out var metrics);
        float ascent = -metrics.Ascent; // ascent is negative in Skia

        var wrappedLines = WrapText(el.Text, font, el.Width);
        float lineHeight = el.LineHeight > 0 ? el.LineHeight : el.FontSize * 1.2f;
        float totalTextHeight = wrappedLines.Count * lineHeight;

        float startY = el.VerticalAlignment switch
        {
            VerticalAlignment.Bottom => el.Y + el.Height - totalTextHeight,
            VerticalAlignment.Center => el.Y + (el.Height - totalTextHeight) / 2f,
            _ => (float)el.Y, // Top or Stretch
        };

        canvas.Save();
        canvas.ClipRect(new SKRect(el.X, el.Y, el.X + el.Width, el.Y + el.Height));

        for (int i = 0; i < wrappedLines.Count; i++)
        {
            string line = wrappedLines[i];
            float baselineY = startY + i * lineHeight + ascent;

            using var measurePaint = new SKPaint(font);
            float textWidth = measurePaint.MeasureText(line);
            float x = el.TextAlignment switch
            {
                TextAlignment.Right   => el.X + el.Width - textWidth,
                TextAlignment.Center  => el.X + (el.Width - textWidth) / 2f,
                _ => el.X, // Left or Justify
            };

            canvas.DrawText(line, x, baselineY, font, paint);
        }

        canvas.Restore();
    }

    // Word-wraps text to fit within maxWidth pixels using the given font.
    // Handles existing newlines first, then word-wraps each segment.
    private static List<string> WrapText(string text, SKFont font, int maxWidth)
    {
        var result = new List<string>();
        using var measurePaint = new SKPaint(font);

        var paragraphs = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var para in paragraphs)
        {
            if (string.IsNullOrEmpty(para))
            {
                result.Add("");
                continue;
            }

            var words = para.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string current = "";

            foreach (var word in words)
            {
                string test = current.Length == 0 ? word : current + " " + word;
                if (measurePaint.MeasureText(test) <= maxWidth)
                {
                    current = test;
                }
                else
                {
                    if (current.Length > 0) result.Add(current);
                    current = word;
                }
            }
            if (current.Length > 0) result.Add(current);
        }

        return result;
    }

    private static SKColor ToSkColor(Color color) =>
        new SKColor(color.R, color.G, color.B, color.A);
}
