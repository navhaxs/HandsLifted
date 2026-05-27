using System;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using HandsLiftedApp.Core.Services;
using LibMpv.Client;

namespace HandsLiftedApp.Core.Render.MotionBackground;

public partial class MotionBackgroundObserver : UserControl, IDisposable
{
    private IDisposable? _subscription;

    public MotionBackgroundObserver()
    {
        InitializeComponent();
        _subscription = MotionBackgroundService.ActiveContext
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(OnContextChanged);
    }

    private void OnContextChanged(MpvContext? context)
    {
        VideoView.MpvContext = context;
        Opacity = context != null ? 1.0 : 0.0;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
        VideoView.MpvContext = null;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Dispose();
    }
}
