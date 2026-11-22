using System;
using System.Collections.Generic;
using System.IO;
using HandsLiftedApp.Core.Render.Skia.Builders;
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
        catch
        {
            return null;
        }
    }
}
