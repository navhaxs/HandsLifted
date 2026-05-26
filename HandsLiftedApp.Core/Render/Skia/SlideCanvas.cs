// HandsLiftedApp.Core/Render/Skia/SlideCanvas.cs
using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;

namespace HandsLiftedApp.Core.Render.Skia;

public sealed class SlideCanvas : Control
{
    // ── Transition state ────────────────────────────────────────────────────

    private SlideRenderSpec? _current;
    private SlideRenderSpec? _previous;
    private float _transitionProgress = 1f;
    private TimeSpan _transitionDuration;
    private Stopwatch? _transitionStopwatch;
    private DispatcherTimer? _timer;

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the spec immediately with no animation. Use in the editor.
    /// </summary>
    public SlideRenderSpec? Spec
    {
        get => _current;
        set
        {
            StopTimer();
            _previous = null;
            _current = value;
            _transitionProgress = 1f;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Transitions to <paramref name="next"/> with a cross-fade over <paramref name="duration"/>.
    /// Unchanged text lines stay at full opacity; only added/removed lines animate.
    /// Use in the projector code-behind.
    /// </summary>
    public void Transition(SlideRenderSpec? next, TimeSpan duration)
    {
        StopTimer();
        _previous = _current;
        _current = next;
        _transitionDuration = duration;
        _transitionProgress = 0f;
        _transitionStopwatch = Stopwatch.StartNew();
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            OnTick);
        _timer.Start();
    }

    // ── Avalonia rendering ─────────────────────────────────────────────────

    protected override void OnMeasureInvalidated()
    {
        base.OnMeasureInvalidated();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.Custom(new SlideDrawOperation(
            new Rect(Bounds.Size),
            _current,
            _previous,
            _transitionProgress));
    }

    // ── Timer ───────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        if (_transitionStopwatch is null) return;

        _transitionProgress = _transitionDuration > TimeSpan.Zero
            ? (float)(_transitionStopwatch.Elapsed / _transitionDuration)
            : 1f;

        if (_transitionProgress >= 1f)
        {
            _transitionProgress = 1f;
            StopTimer();
            _previous = null;
        }

        InvalidateVisual();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
        _transitionStopwatch?.Stop();
        _transitionStopwatch = null;
    }

    // ── ICustomDrawOperation ────────────────────────────────────────────────

    private sealed class SlideDrawOperation : ICustomDrawOperation
    {
        private readonly SlideRenderSpec? _current;
        private readonly SlideRenderSpec? _previous;
        private readonly float _progress;

        public Rect Bounds { get; }

        public SlideDrawOperation(
            Rect bounds,
            SlideRenderSpec? current,
            SlideRenderSpec? previous,
            float progress)
        {
            Bounds = bounds;
            _current = current;
            _previous = previous;
            _progress = progress;
        }

        public void Dispose() { }
        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point p) => true;

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature leaseFeature)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            SlideRenderer.Draw(
                canvas,
                _current,
                _previous,
                _progress,
                (int)Bounds.Width,
                (int)Bounds.Height);
            canvas.Restore();
        }
    }
}
