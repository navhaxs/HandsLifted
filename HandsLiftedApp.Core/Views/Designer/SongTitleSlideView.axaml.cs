using System;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Designer;

public partial class SongTitleSlideView : UserControl
{
    private IDisposable? _subscription;

    public SongTitleSlideView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SongTitleSlideInstance slide)
            SetSlide(slide);
    }

    public void SetSlide(SongTitleSlideInstance? slide)
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
                slide.WhenAnyValue(s => s.Title, s => s.Copyright, s => s.Theme).Select(_ => Unit.Default),
                themePropertyChanges
            )
            .Subscribe(_ => RebuildSpec(slide));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _subscription?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    private void RebuildSpec(SongTitleSlideInstance slide)
    {
        SlideCanvas.Spec = SongTitleSlideSpecBuilder.Build(slide);
    }
}
