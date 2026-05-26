using System;
using System.Reactive.Linq;
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
        _subscription?.Dispose();
        _subscription = null;

        if (DataContext is not SongSlideInstance slide) return;

        _subscription = slide
            .WhenAnyValue(s => s.Text, s => s.Theme)
            .Subscribe(_ => RebuildSpec(slide));

        RebuildSpec(slide);
    }

    private void RebuildSpec(SongSlideInstance slide)
    {
        SlideCanvas.Spec = SongSlideSpecBuilder.Build(slide);
    }
}
