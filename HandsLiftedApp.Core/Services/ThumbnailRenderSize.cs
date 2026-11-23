using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Views;
using ReactiveUI;

namespace HandsLiftedApp.Core.Services;

/// <summary>
/// Decides how many pixels wide a generated slide thumbnail should be, so it is rasterised close to the size it
/// is shown at. Drawing the slide straight at that size (vector text, one image downscale) is much crisper than
/// rendering at 1920x1080 and shrinking the pixels twice. The width follows the thumbnail size sliders and the
/// window's display scaling, in <see cref="Step"/> pixel buckets so a slider drag does not re-render constantly.
/// When the bucket changes, every slide that has been rendered is re-rendered (debounced).
/// </summary>
public static class ThumbnailRenderSize
{
    public const int MinWidth = 160;
    public const int MaxWidth = 1280;
    // Small, so the thumbnail is rarely much larger than its display size: Avalonia resamples it once more when
    // drawing, and that second resample is what softens an oversized thumbnail.
    public const int Step = 16;

    private const double DefaultGridItemWidth = 290;
    private const double SongEditorItemWidth = 240;
    private const int RefreshDelayMs = 500;

    private static int _currentWidth = ComputeWidth(80, DefaultGridItemWidth, 80, 1);
    private static double _renderScaling = 1;
    private static bool _started;
    private static CancellationTokenSource? _refreshCts;

    // Slides that have rendered at least once; weak so a discarded slide is not kept alive.
    private static readonly ConditionalWeakTable<IRenderable, object> Tracked = new();
    private static readonly object Marker = new();

    /// <summary>Pixel width new thumbnails are rendered at (height follows at 16:9).</summary>
    public static int CurrentWidth => Volatile.Read(ref _currentWidth);

    /// <summary>
    /// Thumbnail pixel width for the largest place a slide thumbnail is shown: the slide grid (item width times
    /// its size multiplier, minus the item margin) or the song editor, times the display scaling, rounded up to
    /// the next <see cref="Step"/> and clamped to [<see cref="MinWidth"/>, <see cref="MaxWidth"/>].
    /// </summary>
    public static int ComputeWidth(
        double gridSizeMultiplierPercent,
        double gridItemWidth,
        double songEditorSizeMultiplierPercent,
        double renderScaling)
    {
        var gridDip = Math.Max(1, gridItemWidth * gridSizeMultiplierPercent * 0.01 - SlideGridMetrics.ExtraWidth);
        var editorDip = Math.Max(1, SongEditorItemWidth * songEditorSizeMultiplierPercent * 0.01);
        var pixels = Math.Max(gridDip, editorDip) * Math.Max(1, renderScaling);
        var bucket = (int)Math.Ceiling(pixels / Step) * Step;
        return Math.Clamp(bucket, MinWidth, MaxWidth);
    }

    /// <summary>Call once the main window is open. Starts following the size sliders.</summary>
    public static void Start(double renderScaling)
    {
        _renderScaling = renderScaling;
        if (_started) { Recompute(); return; }
        _started = true;

        var prefs = Globals.Instance.AppPreferences;
        if (prefs != null)
        {
            prefs.WhenAnyValue(p => p.SlideThumbnailSizeMultiplier, p => p.SongEditorThumbnailSizeMultiplier)
                .Subscribe(_ => Recompute());
        }
        Recompute();
    }

    /// <summary>Call when the main window's display scaling changes (moved to another monitor, OS setting).</summary>
    public static void UpdateRenderScaling(double renderScaling)
    {
        _renderScaling = renderScaling;
        Recompute();
    }

    /// <summary>Remembers a slide so it is re-rendered when the thumbnail size bucket changes.</summary>
    public static void Track(IRenderable renderable)
    {
        Tracked.AddOrUpdate(renderable, Marker);
    }

    private static void Recompute()
    {
        var prefs = Globals.Instance.AppPreferences;
        if (prefs == null) return;

        var itemWidth = Globals.Instance.MainViewModel?.ItemWidth ?? DefaultGridItemWidth;
        var width = ComputeWidth(
            prefs.SlideThumbnailSizeMultiplier, itemWidth, prefs.SongEditorThumbnailSizeMultiplier, _renderScaling);

        if (Interlocked.Exchange(ref _currentWidth, width) == width)
            return;

        ScheduleRefresh();
    }

    private static void ScheduleRefresh()
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _refreshCts, cts);
        previous?.Cancel();
        previous?.Dispose();

        _ = Task.Delay(RefreshDelayMs, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
                RenderAllTracked();
        }, TaskScheduler.Default);
    }

    private static void RenderAllTracked()
    {
        var queue = Globals.Instance.SlideRenderQueue;
        var live = new List<IRenderable>();
        foreach (var pair in Tracked)
            live.Add(pair.Key);
        if (live.Count > 0)
            queue.EnqueueBatch(live);
    }
}
