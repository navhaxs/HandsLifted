// HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Platform;
using Serilog;
using SkiaSharp;

namespace HandsLiftedApp.Core.Render.Skia;

public static class SlideRenderer
{
    // ── Image bitmap cache ─────────────────────────────────────────────────────
    // Avoids re-decoding the same image from disk on every frame during transitions.
    // Cache is keyed by file path. FIFO eviction, max 8 entries.
    // Cache owns the bitmaps and disposes evicted ones.

    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, SKBitmap> BitmapCache = new(8);
    private static readonly Queue<string> CacheOrder = new(8);
    private const int MaxCacheEntries = 8;

    private static SKBitmap? LoadCachedBitmap(string filePath)
    {
        lock (CacheLock)
        {
            if (BitmapCache.TryGetValue(filePath, out var cached))
                return cached;
        }

        // Decode outside lock — expensive operation
        SKBitmap? decoded = null;
        try
        {
            Stream? imgStream = filePath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
                ? AssetLoader.Open(new Uri(filePath))
                : File.Exists(filePath) ? File.OpenRead(filePath) : null;

            if (imgStream == null)
            {
                Log.Warning("[SlideRenderer] Image not found: {FilePath}", filePath);
                return null;
            }

            using (imgStream)
                decoded = SKBitmap.Decode(imgStream);

            if (decoded == null)
            {
                Log.Warning("[SlideRenderer] SKBitmap.Decode returned null for: {FilePath}", filePath);
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SlideRenderer] Exception decoding image: {FilePath}", filePath);
            return null;
        }

        lock (CacheLock)
        {
            // Another thread may have populated it while we decoded outside the lock
            if (BitmapCache.TryGetValue(filePath, out var race))
            {
                decoded.Dispose();
                return race;
            }

            if (BitmapCache.Count >= MaxCacheEntries)
            {
                var oldest = CacheOrder.Dequeue();
                if (BitmapCache.Remove(oldest, out var evicted))
                    evicted.Dispose();
            }

            BitmapCache[filePath] = decoded;
            CacheOrder.Enqueue(filePath);
            return decoded;
        }
    }

    // ── Draw entry point ───────────────────────────────────────────────────────

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
        var prevBg = previous?.Background;
        var currBg = current?.Background;

        if (progress < 1f && prevBg != null && currBg != null && prevBg != currBg)
        {
            // Fade-over: previous stays at full opacity, current fades in on top.
            // Avoids the mid-transition darkening that occurs when both images are
            // drawn at partial opacity over a black canvas.
            DrawBackground(canvas, prevBg, 1f, width, height);
            DrawBackground(canvas, currBg, progress, width, height);
        }
        else
        {
            DrawBackground(canvas, currBg ?? prevBg, 1f, width, height);
        }

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

    private static void DrawBackground(SKCanvas canvas, BackgroundSpec? bg, float alpha, int width, int height)
    {
        if (alpha <= 0f) return;

        switch (bg)
        {
            case SolidBackground solid:
            {
                var color = solid.Color.WithAlpha((byte)(solid.Color.Alpha * alpha));
                using var solidPaint = new SKPaint { Color = color };
                canvas.DrawRect(0, 0, width, height, solidPaint);
                break;
            }

            case ImageBackground img:
                if (string.IsNullOrWhiteSpace(img.FilePath))
                {
                    Log.Warning("[SlideRenderer] ImageBackground has null/empty FilePath");
                    break;
                }

                var bmp = LoadCachedBitmap(img.FilePath);
                if (bmp != null)
                {
                    var dest = new SKRect(0, 0, width, height);
                    using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
                    if (alpha < 1f)
                        paint.Color = SKColors.White.WithAlpha((byte)(255 * alpha));
                    canvas.DrawBitmap(bmp, dest, paint);
                }
                break;

            case TransparentBackground:
            default:
                // leave the canvas cleared to transparent
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
