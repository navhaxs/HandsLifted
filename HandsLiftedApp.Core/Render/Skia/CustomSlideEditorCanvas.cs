using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.Models.SlideElement;
using SkiaSharp;
using CustomSlideModel = HandsLiftedApp.Data.Data.Models.Slides.CustomSlide;

namespace HandsLiftedApp.Core.Render.Skia;

/// <summary>
/// Draws a custom slide in the slide designer through <see cref="CustomSlideDrawer"/>, so the designer shows exactly
/// what the projector shows. The slide is drawn in 1920x1080 slide pixels, scaled to this control's bounds.
/// Redraws itself whenever the slide, its element collection or any element changes.
/// </summary>
public sealed class CustomSlideEditorCanvas : Control
{
    private CustomSlideModel? _slide;
    private TextElement? _hiddenTextElement;

    // What we are currently subscribed to, so every subscription can be undone exactly.
    private CustomSlideModel? _listenedSlide;
    private ObservableCollection<SlideElement>? _listenedElements;
    private readonly List<SlideElement> _listenedElementItems = new();
    private bool _detached;

    public CustomSlideEditorCanvas()
    {
        IsHitTestVisible = false; // the transparent proxies above handle the pointer
        ClipToBounds = true;
    }

    public CustomSlideModel? Slide
    {
        get => _slide;
        set
        {
            if (ReferenceEquals(_slide, value)) return;
            _slide = value;
            // Listen straight away (also before first attach, so it works without a window), but not after a
            // detach: then the slide would keep this off-screen control alive until it is attached again.
            if (!_detached) Listen(value);
            Invalidate();
        }
    }

    /// <summary>A text element whose glyphs are not drawn (its fill still is) - used while it is edited inline.</summary>
    public TextElement? HiddenTextElement
    {
        get => _hiddenTextElement;
        set
        {
            if (ReferenceEquals(_hiddenTextElement, value)) return;
            _hiddenTextElement = value;
            Invalidate();
        }
    }

    /// <summary>Number of redraw requests; for tests only.</summary>
    internal int InvalidationCount { get; private set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _detached = false;
        Listen(_slide);
        Invalidate();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _detached = true;
        Listen(null);
    }

    private void Invalidate()
    {
        InvalidationCount++;
        InvalidateVisual();
    }

    // ── Subscriptions ───────────────────────────────────────────────────────

    private void Listen(CustomSlideModel? slide)
    {
        if (ReferenceEquals(_listenedSlide, slide)) return;

        if (_listenedSlide != null) _listenedSlide.PropertyChanged -= OnSlidePropertyChanged;
        ListenElements(null);

        _listenedSlide = slide;
        if (slide == null) return;

        slide.PropertyChanged += OnSlidePropertyChanged;
        ListenElements(slide.SlideElements);
    }

    private void ListenElements(ObservableCollection<SlideElement>? elements)
    {
        if (_listenedElements != null) _listenedElements.CollectionChanged -= OnElementsChanged;
        foreach (var el in _listenedElementItems) el.PropertyChanged -= OnElementPropertyChanged;
        _listenedElementItems.Clear();

        _listenedElements = elements;
        if (elements == null) return;

        elements.CollectionChanged += OnElementsChanged;
        foreach (var el in elements)
        {
            el.PropertyChanged += OnElementPropertyChanged;
            _listenedElementItems.Add(el);
        }
    }

    private void OnSlidePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CustomSlideModel.SlideElements) && sender is CustomSlideModel slide)
            ListenElements(slide.SlideElements);
        Invalidate();
    }

    private void OnElementsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Re-follow the whole list: simple, and correct for Reset/Move/Replace alike.
        ListenElements(_listenedElements);
        Invalidate();
    }

    private void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e) => Invalidate();

    // ── Rendering ───────────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_slide == null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        // Record on the UI thread, where the slide model lives; the render thread only replays the picture,
        // so it never reads the model while it is being edited.
        SKPicture picture;
        using (var recorder = new SKPictureRecorder())
        {
            var recordCanvas = recorder.BeginRecording(
                new SKRect(0, 0, CustomSlideDrawer.CanvasWidth, CustomSlideDrawer.CanvasHeight));
            CustomSlideDrawer.Draw(recordCanvas, _slide, _hiddenTextElement);
            picture = recorder.EndRecording();
        }

        context.Custom(new PictureDrawOperation(new Rect(Bounds.Size), picture));
    }

    private sealed class PictureDrawOperation : ICustomDrawOperation
    {
        private readonly SKPicture _picture;

        public Rect Bounds { get; }

        public PictureDrawOperation(Rect bounds, SKPicture picture)
        {
            Bounds = bounds;
            _picture = picture;
        }

        public void Dispose() => _picture.Dispose();
        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point p) => false;

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature leaseFeature)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            canvas.ClipRect(new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height));
            canvas.Scale((float)(Bounds.Width / CustomSlideDrawer.CanvasWidth),
                (float)(Bounds.Height / CustomSlideDrawer.CanvasHeight));
            canvas.DrawPicture(_picture);
            canvas.Restore();
        }
    }
}
