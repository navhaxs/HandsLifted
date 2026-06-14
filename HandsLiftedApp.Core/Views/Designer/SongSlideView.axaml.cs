using System;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Designer;

public partial class SongSlideView : UserControl
{
    private IDisposable? _subscription;

    public SongSlideView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SongSlideInstance slide)
            SetSlide(slide);
    }

    public void SetSlide(SongSlideInstance? slide)
    {
        _subscription?.Dispose();
        _subscription = null;

        if (slide == null)
        {
            SlideCanvas.Spec = null;
            return;
        }

        var themePropertyChanges = slide
            .WhenAnyValue(s => s.Theme)
            .Select(t => t?.Changed.Select(_ => Unit.Default) ?? Observable.Never<Unit>())
            .Switch();

        _subscription = Observable
            .Merge(
                slide.WhenAnyValue(s => s.Text, s => s.Theme).Select(_ => Unit.Default),
                themePropertyChanges
            )
            .Subscribe(_ => RebuildSpec(slide));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _subscription?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    private void RebuildSpec(SongSlideInstance slide)
    {
        SlideCanvas.Spec = SongSlideSpecBuilder.Build(slide);
    }
}
