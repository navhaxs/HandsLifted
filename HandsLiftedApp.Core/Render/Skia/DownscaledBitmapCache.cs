using System;
using System.Collections.Generic;
using SkiaSharp;

namespace HandsLiftedApp.Core.Render.Skia;

/// <summary>
/// Small LRU of area-averaged (see <see cref="BitmapScaling.Downscale"/>) copies of big background bitmaps, used when a
/// slide is drawn much smaller than its 1920x1080 layout (the live preview, editor previews). Shrinking per frame with
/// the canvas's own filter aliases, and shrinking per frame ourselves would be too slow during a transition, so each
/// (source, size) is shrunk once and reused.
/// </summary>
internal static class DownscaledBitmapCache
{
    private const int MaxEntries = 8;

    private sealed record Entry(WeakReference<SKBitmap> Source, int Width, int Height, SKBitmap Scaled)
    {
        public bool Matches(SKBitmap source, int width, int height) =>
            Width == width && Height == height && Source.TryGetTarget(out var s) && ReferenceEquals(s, source);
    }

    private static readonly object Gate = new();
    private static readonly LinkedList<Entry> Entries = new(); // first = least recently used

    /// <summary>
    /// Returns a <paramref name="width"/> x <paramref name="height"/> copy of <paramref name="source"/>, or null if
    /// it could not be made. The caller must NOT dispose it: it is shared, and an evicted copy may still be mid-draw on
    /// another thread, so eviction leaves it to the finalizer instead of disposing it.
    /// </summary>
    public static SKBitmap? GetOrCreate(SKBitmap source, int width, int height)
    {
        lock (Gate)
        {
            var hit = Find(source, width, height);
            if (hit != null)
                return hit;
        }

        var scaled = BitmapScaling.Downscale(source, width, height); // outside the lock: slow
        if (scaled == null)
            return null;

        lock (Gate)
        {
            var raced = Find(source, width, height); // another thread may have made the same one meanwhile
            if (raced != null)
            {
                scaled.Dispose(); // ours was never shared
                return raced;
            }

            Entries.AddLast(new Entry(new WeakReference<SKBitmap>(source), width, height, scaled));
            while (Entries.Count > MaxEntries)
                Entries.RemoveFirst();
            return scaled;
        }
    }

    // Must be called under Gate. Marks a hit as most recently used.
    private static SKBitmap? Find(SKBitmap source, int width, int height)
    {
        for (var node = Entries.First; node != null; node = node.Next)
        {
            if (!node.Value.Matches(source, width, height))
                continue;
            Entries.Remove(node);
            Entries.AddLast(node);
            return node.Value.Scaled;
        }
        return null;
    }

    /// <summary>Drops every cached copy (tests).</summary>
    internal static void Clear()
    {
        lock (Gate)
            Entries.Clear();
    }

    internal static int Count
    {
        get { lock (Gate) return Entries.Count; }
    }
}
