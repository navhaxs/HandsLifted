using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Skia.Helpers;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HandsLiftedApp.XTransitioningContentControl
{
    /// <summary>
    /// Displays <see cref="ContentControl.Content"/> according to a <see cref="FuncDataTemplate"/>.
    /// Uses <see cref="PageTransition"/> to move between the old and new content values. 
    /// </summary>
    public class XTransitioningContentControl : ContentControl
    {
        private CancellationTokenSource? _currentTransition;
        private ContentPresenter? _presenter2;
        private bool _isFirstFull;
        private bool _shouldAnimate;

        /// <summary>
        /// Defines the <see cref="PageTransition"/> property.
        /// </summary>
        public static readonly StyledProperty<IPageTransition?> PageTransitionProperty =
            AvaloniaProperty.Register<TransitioningContentControl, IPageTransition?>(
                nameof(PageTransition),
                defaultValue: new ImmutableCrossFade(TimeSpan.FromMilliseconds(125)));

        /// <summary>
        /// Gets or sets the animation played when content appears and disappears.
        /// </summary>
        public IPageTransition? PageTransition
        {
            get => GetValue(PageTransitionProperty);
            set => SetValue(PageTransitionProperty, value);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var result = base.ArrangeOverride(finalSize);

            if (_shouldAnimate)
            {
                _currentTransition?.Cancel();

                if (_presenter2 is not null &&
                    Presenter is Visual presenter &&
                    PageTransition is { } transition)
                {
                    _shouldAnimate = false;

                    var cancel = new CancellationTokenSource();
                    _currentTransition = cancel;

                    var from = _isFirstFull ? _presenter2 : presenter;
                    var to = _isFirstFull ? presenter : _presenter2;

                    from.ZIndex = 0;
                    to.ZIndex = 1;

                    transition.Start(from, to, true, cancel.Token).ContinueWith(x =>
                    {
                        if (!cancel.IsCancellationRequested)
                        {
                            HideOldPresenter();
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }

                _shouldAnimate = false;
            }

            return result;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdateContent(false);
        }

        protected override bool RegisterContentPresenter(ContentPresenter presenter)
        {
            if (base.RegisterContentPresenter(presenter))
            {
                return true;
            }

            if (presenter is ContentPresenter p &&
                p.Name == "PART_ContentPresenter2")
            {
                _presenter2 = p;
                _presenter2.IsVisible = false;
                UpdateContent(false);
                return true;
            }

            return false;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == ContentProperty)
            {
                UpdateContent(true);
                return;
            }

            base.OnPropertyChanged(change);
        }

        private void UpdateContent(bool withTransition)
        {
            if (VisualRoot is null || _presenter2 is null || Presenter is null)
            {
                return;
            }

            var currentPresenter = _isFirstFull ? _presenter2 : Presenter;
            currentPresenter.Content = Content;
            currentPresenter.IsVisible = true;

            _isFirstFull = !_isFirstFull;

            if (PageTransition is not null && withTransition)
            {
                _shouldAnimate = true;
                InvalidateArrange();
            }
            else
            {
                HideOldPresenter();
            }
        }

        private void HideOldPresenter()
        {
            var oldPresenter = _isFirstFull ? _presenter2 : Presenter;
            if (oldPresenter is not null)
            {
                oldPresenter.Content = null;
                oldPresenter.IsVisible = false;

                oldPresenter.ZIndex = 1;
            }
            var newPresenter = _isFirstFull ? Presenter : _presenter2;
            if (newPresenter is not null)
            {
                newPresenter.ZIndex = 0;
            }
        }

        private class ImmutableCrossFade : IPageTransition
        {
            private readonly CrossFade _inner;

            public ImmutableCrossFade(TimeSpan duration) => _inner = new CrossFade(duration);

            public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
            {
                return _inner.Start(from, to, cancellationToken);
            }
        }
        //protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        //{
        //    base.OnApplyTemplate(e);

        //    // todo: if this fails, did you forget to add Styles?
        //    _previousImageSite = e.NameScope.Find<Image>("PART_PreviousImageSite");
        //    _currentImageSite = e.NameScope.Find<Image>("PART_CurrentImageSite");
        //    _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        //    _contentPresenterContainer = e.NameScope.Find<Grid>("PART_ContentPresenterContainer");

        //    Dispatcher.UIThread.Invoke(() => UpdateContentWithTransition(Content));
        //}

        ///// <summary>
        ///// Updates the content with transitions.
        ///// </summary>
        ///// <param name="content">New content to set.</param>
        //private async void UpdateContentWithTransition(object? content)
        //{
        //    if (VisualRoot is null)
        //    {
        //        return;
        //    }

        //    IPageTransition transition = PageTransition;
        //    if (content is ISlideRender)
        //    {
        //        await Dispatcher.UIThread.InvokeAsync(() => ((ISlideRender)content).OnEnterSlide());

        //        // if slide has page transition override
        //        if (((ISlideRender)content).PageTransition != null)
        //        {
        //            transition = ((ISlideRender)content).PageTransition;
        //        }
        //    }

        //    _lastTransitionCts?.Cancel();
        //    _lastTransitionCts = new CancellationTokenSource();

        //    if (_previousImageSite != null && CurrentContent is not IDynamicSlideRender)
        //    {
        //        // copy the 'current slide as Bitmap' to the previous image layer
        //        _previousImageSite.Source = _currentImageSite.Source;
        //        _previousImageSite.IsVisible = true;
        //        _previousImageSite.Opacity = 1;
        //    }


        //    if (CurrentContent is ISlideRender && EnableSlideEvents)
        //    {
        //        ((ISlideRender)CurrentContent).OnLeaveSlide();
        //    }

        //    CurrentContent = content;
        //    if (_contentPresenterContainer != null)
        //        _contentPresenterContainer.ZIndex = (CurrentContent is IDynamicSlideRender) ? 999 : 0;
        //    Dispatcher.UIThread.RunJobs(DispatcherPriority.Render); // required to wait for images to load

        //    _currentImageSite.IsVisible = true;
        //    _currentImageSite.Opacity = 0;
        //    _currentImageSite.Source = GetBitmap(CurrentContent);

        //    // maybe what this could instead is take screenshot of existing (bitmap no alpha), then take screenshot of new (bitmap no alpha), then fade between the bitmaps. this way, there should be no alpha multiplier issues during the fade effect.

        //    if (transition != null)
        //    {
        //        //if (CurrentContent is IDynamicSlideRender)
        //        //{
        //        //    await transition.Start(_previousImageSite, null, true, _lastTransitionCts.Token);
        //        //}
        //        //else
        //        //{
        //        //    // TODO bug when fading to black (empty slide)
        //        await transition.Start(_previousImageSite, _currentImageSite, true, _lastTransitionCts.Token);
        //        //}
        //    }
        //    else
        //    {
        //        //_contentPresenterContainer.IsVisible = true;
        //        _previousImageSite.Opacity = 0;
        //    }

        //    _currentImageSite.IsVisible = true;
        //    _currentImageSite.Opacity = 1;

        //    _previousImageSite.IsVisible = false;
        //    _previousImageSite.Opacity = 0;
        //    _previousImageSite.Source = null;

        //    Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
        //    InvalidateArrange();
        //}

        //private Bitmap GetBitmap(object? visual)
        //{
        //    // TODO Cache blank black bitmap
        //    if (visual == null)
        //        return renderControlAsBitmap(null);

        //    if (visual is ISlideBitmapRender)
        //        return ((ISlideBitmapRender)visual).GetBitmap();

        //    if (visual is ISlideBitmapCacheable)
        //    {
        //        Bitmap? cached = ((ISlideBitmapCacheable)visual).GetBitmap();
        //        if (cached != null)
        //            return cached;
        //    }

        //    Bitmap rendered = renderControlAsBitmap(_contentPresenter);

        //    if (visual is ISlideBitmapCacheable)
        //    {
        //        // Testing
        //        ((ISlideBitmapCacheable)visual).SetBitmap(rendered);
        //    }

        //    return rendered;
        //}

        private Bitmap renderControlAsBitmap(Visual? visual)
        {
            using (SKBitmap bitmap = new SKBitmap(1920, 1080))
            {
                using (SKCanvas canvas = new SKCanvas(bitmap))
                {

                    canvas.DrawRect(0, 0, 1920, 1080, new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.Black });


                    int xres = 1920;
                    int yres = 1080;
                    int stride = (xres * 32/*BGRA bpp*/ + 7) / 8;
                    int bufferSize = yres * stride;
                    IntPtr bufferPtr = Marshal.AllocCoTaskMem(bufferSize);

                    // define the surface properties
                    var info = new SKImageInfo(xres, yres);

                    // construct a surface around the existing memory
                    var destinationSurface = SKSurface.Create(info, bufferPtr, info.RowBytes);

                    SKImage image = destinationSurface.Snapshot();

                    if (visual != null)
                    {
                        using IDrawingContextImpl contextImpl = DrawingContextHelper.WrapSkiaCanvas(canvas, SkiaPlatform.DefaultDpi);
                        using RenderTargetBitmap rtb = new RenderTargetBitmap(new PixelSize(1920, 1080));
                        try
                        {
                            rtb.Render(visual); // System.ArgumentException: 'An item with the same key has already been added. Key: SubpixelAntialias'
                        }
                        catch (Exception ex1)
                        {
                            try
                            {
                                rtb.Render(visual); // System.ArgumentException: 'An item with the same key has already been added. Key: SubpixelAntialias'
                            }
                            catch (Exception ex2)
                            {
                                // retry
                            }
                        }

                        rtb.CopyPixels(new PixelRect(0, 0, xres, yres), bufferPtr, bufferSize, stride);
                        bitmap.SetPixels(bufferPtr);

                        // Create WriteableBitmap to copy the pixel data to.      
                        //WriteableBitmap target = new WriteableBitmap(
                        //  rtb.PixelSize,
                        //  rtb.Dpi,
                        //  rtb.Format);

                        //rtb.CopyPixels(new PixelRect(0, 0, xres, yres), bufferPtr, bufferSize, stride);

                        //IRenderTargetBitmapImpl item = rtb.PlatformImpl.Item;
                        //IDrawingContextImpl drawingContextImpl = item.CreateDrawingContext();
                        //var leaseFeature = drawingContextImpl.GetFeature<ISkiaSharpApiLeaseFeature>();
                        //using var lease = leaseFeature.Lease();
                        //using SKImage skImage = lease.SkSurface.Snapshot();

                        //canvas.DrawImage(skImage, new SKPoint(0, 0));
                    }
                }

                // BmpSharp as workaround to encode to BMP. This is MUCH faster than using SkiaSharp to encode to PNG.
                // https://github.com/mono/SkiaSharp/issues/320#issuecomment-582132563
                BmpSharp.BitsPerPixelEnum bitsPerPixel = bitmap.BytesPerPixel == 4 ? BmpSharp.BitsPerPixelEnum.RGBA32 : BmpSharp.BitsPerPixelEnum.RGB24;
                BmpSharp.Bitmap bmp = new BmpSharp.Bitmap(bitmap.Width, bitmap.Height, bitmap.Bytes, bitsPerPixel);
                return new Bitmap(bmp.GetBmpStream(fliped: true));
            }
        }
    }
}