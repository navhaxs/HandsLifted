using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Designer;

public partial class ScriptureSlideView : UserControl
{
    private IDisposable? _subscription;
    private IReadOnlyList<ScriptureVerseRef>? _previewVerses;
    private string? _previewHeader;

    public ScriptureSlideView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ScriptureSlideInstance slide)
            SetSlide(slide);
    }

    public void SetSlide(ScriptureSlideInstance? slide) => SetSlide(slide, null, null);

    // previewVerses/previewHeader: when provided, this view repaginates the given sample verses
    // against the slide's current Theme on every rebuild (so font-size/family/line-height edits
    // are reflected live), instead of rendering whatever Lines the caller already set.
    public void SetSlide(ScriptureSlideInstance? slide, IReadOnlyList<ScriptureVerseRef>? previewVerses, string? previewHeader)
    {
        _subscription?.Dispose();
        _subscription = null;
        _previewVerses = previewVerses;
        _previewHeader = previewHeader;

        if (slide == null)
        {
            SlideCanvas.Spec = null;
            return;
        }

        var themePropertyChanges = slide
            .WhenAnyValue(s => s.Theme)
            .Select(t => t?.Changed.Select(_ => Unit.Default) ?? Observable.Never<Unit>())
            .Switch();

        // In preview mode, RebuildSpec below WRITES slide.Lines each time it runs (from a fresh
        // Paginate call), so this subscription must NOT also watch Lines - doing so would re-fire
        // on every self-write (a freshly paginated list is never reference-equal to the last one,
        // even with identical content) and spin forever. Watch only Theme (reference swap) plus
        // its property edits; RebuildSpec recomputes Lines fresh from the current Theme every time.
        var driver = _previewVerses != null
            ? slide.WhenAnyValue(s => s.Theme).Select(_ => Unit.Default).Merge(themePropertyChanges)
            : slide.WhenAnyValue(s => s.Lines, s => s.Theme).Select(_ => Unit.Default).Merge(themePropertyChanges);

        _subscription = driver.Subscribe(_ => RebuildSpec(slide));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _subscription?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    private void RebuildSpec(ScriptureSlideInstance slide)
    {
        if (_previewVerses != null && slide.Theme != null)
        {
            var pages = ScriptureParagraphLayoutEngine.Paginate(_previewVerses, _previewHeader ?? "", slide.Theme);
            if (pages.Count > 0)
            {
                slide.Lines = pages[0].Lines;
                slide.EffectiveFontSize = pages[0].FontSize;
            }
        }
        SlideCanvas.Spec = ScriptureParagraphSpecBuilder.Build(slide);
    }
}
