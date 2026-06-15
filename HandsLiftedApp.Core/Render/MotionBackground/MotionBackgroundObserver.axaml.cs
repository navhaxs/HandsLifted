using System;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Core.Services;
using LibMpv.Client;

namespace HandsLiftedApp.Core.Render.MotionBackground;

public partial class MotionBackgroundObserver : UserControl, IDisposable
{
    private IDisposable? _contextSubscription;
    private IDisposable? _fadeOutSubscription;
    private IDisposable? _fadeInSubscription;

    public MotionBackgroundObserver()
    {
        InitializeComponent();

        // Connect/disconnect the video context. Opacity is NOT changed here during a
        // cross-fade — the fade-out and fade-in subscriptions below own that.
        _contextSubscription = MotionBackgroundService.ActiveContext
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(OnContextChanged);

        // When a cross-fade starts, begin fading out in sync with MotionBackgroundLayer.
        _fadeOutSubscription = MotionBackgroundService.IsTransitioning
            .Where(v => v)
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(_ => Opacity = 0.0);

        // When the new video's first frame is ready, begin fading in in sync with the layer.
        // This fires only from FadeIn() — not from cancel/stop paths — so we never
        // accidentally fade in when no new video is actually starting.
        _fadeInSubscription = MotionBackgroundService.WhenNewVideoFadesIn
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(_ => Opacity = 1.0);
    }

    private void OnContextChanged(MpvContext? context)
    {
        VideoView.MpvContext = context;

        // During a cross-fade the fade-out/fade-in subscriptions own opacity.
        // For all other cases (fresh start, simple stop) set it from context presence.
        if (!MotionBackgroundService.IsCurrentlyTransitioning)
            Opacity = context != null ? 1.0 : 0.0;
    }

    public void Dispose()
    {
        _contextSubscription?.Dispose();
        _contextSubscription = null;
        _fadeOutSubscription?.Dispose();
        _fadeOutSubscription = null;
        _fadeInSubscription?.Dispose();
        _fadeInSubscription = null;
        VideoView.MpvContext = null;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Dispose();
    }
}
