// HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace HandsLiftedApp.Core.Render.Skia;

public static class SlideRenderer
{
    // Entry point for SlideCanvas (called every frame during transitions)
    /// <remarks>
    /// Transition diffing uses <see cref="TextLineElement.Text"/> as the identity key via a <see cref="HashSet{T}"/>.
    /// If the same text appears on multiple lines within a single spec, only one instance is tracked —
    /// duplicate lines will not fade out correctly when removed. This is an accepted MVP limitation.
    /// </remarks>
    public static void Draw(
        SKCanvas canvas,
        SlideRenderSpec? current,
        SlideRenderSpec? previous,
        float progress,
        int width,
        int height)
    {
        canvas.Clear(SKColors.Transparent);

        DrawBackground(canvas, current?.Background ?? previous?.Background, width, height);

        // Elements in current: unchanged lines stay at 1.0, new lines fade in
        if (current != null)
        {
            var previousTexts = previous?.Elements
                .OfType<TextLineElement>()
                .Select(e => e.Text)
                .ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);

            foreach (var element in current.Elements)
            {
                if (element is TextLineElement textEl)
                {
                    float alpha = previousTexts.Contains(textEl.Text) ? 1f : progress;
                    DrawTextElement(canvas, textEl, alpha);
                }
            }
        }

        // Elements only in previous: fade out
        if (previous != null && progress < 1f)
        {
            var currentTexts = current?.Elements
                .OfType<TextLineElement>()
                .Select(e => e.Text)
                .ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);

            foreach (var element in previous.Elements)
            {
                if (element is TextLineElement textEl && !currentTexts.Contains(textEl.Text))
                    DrawTextElement(canvas, textEl, 1f - progress);
            }
        }
    }

    // Convenience overload for static rendering (no transition)
    public static SKBitmap RenderToSKBitmap(
        SlideRenderSpec spec,
        int width = 1920,
        int height = 1080,
        SlideRenderSpec? previous = null,
        float progress = 1f)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        if (surface is null)
            throw new InvalidOperationException($"SKSurface.Create failed for {width}x{height}.");
        Draw(surface.Canvas, spec, previous, progress, width, height);
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static void DrawBackground(SKCanvas canvas, BackgroundSpec? bg, int width, int height)
    {
        switch (bg)
        {
            case SolidBackground solid:
            {
                using var solidPaint = new SKPaint { Color = solid.Color };
                canvas.DrawRect(0, 0, width, height, solidPaint);
                break;
            }

            case ImageBackground img:
                using (var bmp = SKBitmap.Decode(img.FilePath))
                {
                    if (bmp != null)
                    {
                        var dest = new SKRect(0, 0, width, height);
                        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
                        canvas.DrawBitmap(bmp, dest, paint);
                    }
                }
                break;

            case TransparentBackground:
            default:
                // leave the canvas cleared to transparent (done above)
                break;
        }
    }

    private static void DrawTextElement(SKCanvas canvas, TextLineElement element, float alpha)
    {
        if (alpha <= 0f) return;

        using var font = new SKFont(element.Typeface, element.FontSize)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Subpixel = true,
        };

        font.GetFontMetrics(out var metrics);
        // DrawText baseline = Bounds.Top + ascent height (ascent is negative in Skia)
        float baselineY = element.Bounds.Top - metrics.Ascent;

        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = element.Color.WithAlpha((byte)(element.Color.Alpha * alpha));

        if (element.Shadow is { } shadow)
        {
            // BlurRadius → Gaussian sigma: sigma = blurRadius / 2
            float sigma = shadow.BlurRadius / 2f;
            paint.ImageFilter = SKImageFilter.CreateDropShadow(
                shadow.OffsetX,
                shadow.OffsetY,
                sigma,
                sigma,
                shadow.Color.WithAlpha((byte)(shadow.Color.Alpha * alpha)));
        }

        canvas.DrawText(element.Text, element.Bounds.Left, baselineY, font, paint);
    }
}
