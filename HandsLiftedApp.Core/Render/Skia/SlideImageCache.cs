using System;
using System.Collections.Generic;
using System.IO;
using HandsLiftedApp.Core.Render.Skia.Builders;
using Serilog;
using SkiaSharp;

namespace HandsLiftedApp.Core.Render.Skia;

/// <summary>
/// Small bounded cache of decoded images for custom slide drawing. Returned bitmaps are shared and owned by
/// the cache: callers must not dispose them.
/// </summary>
public static class SlideImageCache
{
    private const int MaxEntries = 32;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, (SKBitmap Bitmap, DateTime Stamp)> Entries = new();
    private static readonly Queue<string> Order = new();

    public static SKBitmap? Get(string path)
    {
        try
        {
            var normalized = SlideSpecResolver.NormalizeMediaPath(path);
            if (string.IsNullOrWhiteSpace(normalized)) return null;

            bool isAvares = normalized.StartsWith("avares://", StringComparison.OrdinalIgnoreCase);
            DateTime stamp = DateTime.MinValue;
            if (!isAvares)
            {
                if (!File.Exists(normalized)) return null;
                stamp = File.GetLastWriteTimeUtc(normalized);
            }

            lock (Gate)
            {
                if (Entries.TryGetValue(normalized, out var hit) && hit.Stamp == stamp)
                    return hit.Bitmap;
            }

            Stream? stream = isAvares
                ? Avalonia.Platform.AssetLoader.Open(new Uri(normalized))
                : File.OpenRead(normalized);
            if (stream == null) return null;

            SKBitmap? bmp;
            using (stream) bmp = SKBitmap.Decode(stream);
            if (bmp == null) return null;

            // Never written to once cached. A mutable bitmap is deep-copied by DrawBitmap (SKImage.FromBitmap) on every
            // redraw and held by the recorded picture; an immutable one is shared.
            bmp.SetImmutable();

            lock (Gate)
            {
                if (Entries.ContainsKey(normalized))
                {
                    // Stale (changed on disk): replace. Old bitmap may still be in use by a caller, so let the GC finalize it.
                    Entries[normalized] = (bmp, stamp);
                }
                else
                {
                    Entries[normalized] = (bmp, stamp);
                    Order.Enqueue(normalized);
                    while (Entries.Count > MaxEntries && Order.Count > 0)
                        Entries.Remove(Order.Dequeue());
                }
            }
            return bmp;
        }
        catch (Exception ex)
        {
            WarnOnce(path, ex);
            return null;
        }
    }

    private const int MaxWarnedPaths = 64;
    private static readonly HashSet<string> Warned = new();

    // A failing image is asked for again on every redraw: log it once per path (bounded, so it cannot grow forever).
    private static void WarnOnce(string? path, Exception ex)
    {
        var key = path ?? "";
        lock (Gate)
        {
            if (Warned.Count >= MaxWarnedPaths) Warned.Clear();
            if (!Warned.Add(key)) return;
        }
        Log.Warning("Could not load slide image {Path} - {Message}", key, ex.Message);
    }
}
