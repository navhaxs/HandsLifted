using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Core.Utils;
using LibMpv.Thumbnailing;
using Serilog;
using ShellThumbs;
using SkiaSharp;

namespace HandsLiftedApp.Core.Services;

/// <summary>
/// First-frame cache for motion background videos, used to draw title-slide thumbnails.
///
/// Pulling a frame out of a video is slow (0.8-2.4 s each on a video from a network drive) and a playlist
/// full of songs normally shares just a few videos, so extracting per slide repeats the same work dozens of
/// times. Each distinct video is extracted once; callers get a private copy they may draw with and dispose.
/// Concurrent requests for the same video wait for the single extraction instead of starting their own.
/// </summary>
public static class MotionVideoFrameCache
{
    private const int MaxEntries = 8;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, Lazy<SKBitmap?>> Entries = new();
    private static readonly Queue<string> InsertionOrder = new();

    /// <summary>A copy of the video's cached first frame (extracting it if needed), or null if extraction failed.</summary>
    public static SKBitmap? GetFrame(string videoPath) => GetFrame(videoPath, ExtractFrame);

    internal static SKBitmap? GetFrame(string videoPath, Func<string, SKBitmap?> extract)
    {
        var key = KeyFor(videoPath);
        Lazy<SKBitmap?> entry;
        lock (Gate)
        {
            if (!Entries.TryGetValue(key, out entry!))
            {
                entry = new Lazy<SKBitmap?>(() => extract(videoPath));
                Entries[key] = entry;
                InsertionOrder.Enqueue(key);
                // Evicted frames are not disposed here: another thread may be copying one right now.
                // SKBitmap has a finalizer, so the memory is reclaimed once nothing references it.
                while (InsertionOrder.Count > MaxEntries)
                    Entries.Remove(InsertionOrder.Dequeue());
            }
        }

        var frame = entry.Value; // concurrent callers for the same video block here until the first finishes
        if (frame == null)
        {
            // Do not remember failures: the file may simply not be reachable yet.
            lock (Gate)
            {
                if (Entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
                    Entries.Remove(key);
            }
            return null;
        }

        return frame.Copy();
    }

    /// <summary>Forgets every cached frame (tests).</summary>
    internal static void Clear()
    {
        lock (Gate)
        {
            Entries.Clear();
            InsertionOrder.Clear();
        }
    }

    // The file's write time is part of the key so replacing a video in place does not show a stale frame.
    private static string KeyFor(string videoPath)
    {
        try
        {
            return videoPath + "|" + File.GetLastWriteTimeUtc(videoPath).Ticks;
        }
        catch (Exception)
        {
            return videoPath;
        }
    }

    private static SKBitmap? ExtractFrame(string videoPath)
    {
        if (ThumbnailEngineSettings.UseMpvEngine)
        {
            try
            {
                using var avaBmp = MpvThumbnailExtractor.ExtractAsync(videoPath, maxWidth: 1920, maxHeight: 1080)
                    .GetAwaiter().GetResult();
                return avaBmp != null ? BitmapUtils.AvaloniaToSKBitmap(avaBmp) : null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[MotionVideoFrameCache] Failed to extract video thumbnail from {Path}", videoPath);
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var avaBmp = WindowsThumbnailProvider.GetThumbnail(videoPath, 1920, 1080, ThumbnailOptions.None);
                return avaBmp != null ? BitmapUtils.AvaloniaToSKBitmap(avaBmp) : null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[MotionVideoFrameCache] Failed to extract video thumbnail from {Path}", videoPath);
            }
        }

        return null;
    }
}
