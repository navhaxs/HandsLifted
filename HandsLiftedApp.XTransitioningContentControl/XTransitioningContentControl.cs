using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Skia;
using Avalonia.Skia.Helpers;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using System.Timers;

namespace HandsLiftedApp.XTransitioningContentControl
{
    /// <summary>
    /// Displays <see cref="ContentControl.Content"/> according to a <see cref="FuncDataTemplate"/>.
    /// Uses <see cref="PageTransition"/> to move between the old and new content values. 
    /// </summary>
    public class XTransitioningContentControl : ContentControl
    {
        private CancellationTokenSource? _lastTransitionCts;
        private bool _enableSlideEvents = true;
        private object? _currentContent;
        Image _previousImageSite;
        Image _currentImageSite;
        ContentPresenter _contentPresenter;
        Grid _contentPresenterContainer;

        /// <summary>
        /// Defines the <see cref="EnableSlideEvents"/> property.
        /// </summary>
        public static readonly DirectProperty<XTransitioningContentControl, bool> EnableSlideEventsProperty =
            AvaloniaProperty.RegisterDirect<XTransitioningContentControl, bool>(
                nameof(EnableSlideEvents),
                o => o.EnableSlideEvents,
                (o, v) => o.EnableSlideEvents = v);

        /// <summary>
        /// Defines the <see cref="PageTransition"/> property.
        /// </summary>
        public static readonly StyledProperty<IPageTransition?> PageTransitionProperty =
            AvaloniaProperty.Register<XTransitioningContentControl, IPageTransition?>(
                nameof(PageTransition),
                //new CrossFade(TimeSpan.FromSeconds(0.125)));
                new XFade(TimeSpan.FromSeconds(0.500)));

        /// <summary>
        /// Defines the <see cref="CurrentContent"/> property.
        /// </summary>
        public static readonly DirectProperty<XTransitioningContentControl, object?> CurrentContentProperty =
            AvaloniaProperty.RegisterDirect<XTransitioningContentControl, object?>(
                nameof(CurrentContent),
                o => o.CurrentContent);

        public XTransitioningContentControl()
        {
        }

        /// <summary>
        /// Gets or sets the animation played when content appears and disappears.
        /// </summary>
        public IPageTransition? PageTransition
        {
            get => GetValue(PageTransitionProperty);
            set => SetValue(PageTransitionProperty, value);
        }

        /// <summary>
        /// Gets the content currently displayed on the screen.
        /// </summary>
        public object? CurrentContent
        {
            get => _currentContent;
            private set => SetAndRaise(CurrentContentProperty, ref _currentContent, value);
        }

        /// <summary>
        /// </summary>
        public bool EnableSlideEvents
        {
            get => _enableSlideEvents;
            private set => SetAndRaise(EnableSlideEventsProperty, ref _enableSlideEvents, value);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            Dispatcher.UIThread.Post(() => UpdateContentWithTransition(Content));
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            _lastTransitionCts?.Cancel();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ContentProperty)
            {
                Dispatcher.UIThread.Post(() => UpdateContentWithTransition(Content));
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _previousImageSite = e.NameScope.Find<Image>("PART_PreviousImageSite");
            _currentImageSite = e.NameScope.Find<Image>("PART_CurrentImageSite");
            _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
            _contentPresenterContainer = e.NameScope.Find<Grid>("PART_ContentPresenterContainer");

            // todo: if this fails, did you forget to add Styles?
        }


        /// <summary>
        /// Updates the content with transitions.
        /// </summary>
        /// <param name="content">New content to set.</param>
        private async void UpdateContentWithTransition(object? content)
        {
            if (VisualRoot is null)
            {
                return;
            }

            IPageTransition transition = PageTransition;
            if (content is ISlideRender)
            {
                await Dispatcher.UIThread.InvokeAsync(() => ((ISlideRender)content).OnEnterSlide());

                // if slide has page transition override
                if (((ISlideRender)content).PageTransition != null)
                {
                    transition = ((ISlideRender)content).PageTransition;
                }
            }


            //if (Object.Equals(CurrentContent, content))
            //{
            //    return;
            //}

            //var clock = AvaloniaLocator.Current.GetService<IGlobalClock>();
            //clock.PlayState = PlayState.Pause;

            _lastTransitionCts?.Cancel();
            _lastTransitionCts = new CancellationTokenSource();

            if (_previousImageSite != null && CurrentContent is not IDynamicSlideRender)
            {
                // copy the 'current slide as Bitmap' to the previous image layer
                _previousImageSite.Source = _currentImageSite.Source;
                _previousImageSite.IsVisible = true;
                _previousImageSite.Opacity = 1;
            }


            if (CurrentContent is ISlideRender && EnableSlideEvents)
            {
                ((ISlideRender)CurrentContent).OnLeaveSlide();
            }

            CurrentContent = content;
            if (_contentPresenterContainer != null)
                _contentPresenterContainer.ZIndex = (CurrentContent is IDynamicSlideRender) ? 999 : 0;
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render); // required to wait for images to load

            _currentImageSite.IsVisible = true;
            _currentImageSite.Opacity = 0;
            _currentImageSite.Source = GetBitmap(CurrentContent);

            // maybe what this could instead is take screenshot of existing (bitmap no alpha), then take screenshot of new (bitmap no alpha), then fade between the bitmaps. this way, there should be no alpha multiplier issues during the fade effect.

            if (transition != null)
            {
                if (CurrentContent is IDynamicSlideRender)
                {
                    await transition.Start(_previousImageSite, null, true, _lastTransitionCts.Token);
                }
                else
                {
                    // TODO bug when fading to black (empty slide)
                    await transition.Start(_previousImageSite, _currentImageSite, true, _lastTransitionCts.Token);
                }
            }
            else
            {
                //_contentPresenterContainer.IsVisible = true;
                _previousImageSite.Opacity = 0;
            }

            _currentImageSite.IsVisible = true;
            _currentImageSite.Opacity = 1;
        }

        private Bitmap GetBitmap(object? visual) {
            if (visual == null)
                return null;

            if (visual is ISlideBitmapRender)
                return ((ISlideBitmapRender)visual).GetBitmap();

            if (visual is ISlideBitmapCacheable) {
                Bitmap? cached = ((ISlideBitmapCacheable)visual).GetBitmap();
                if (cached != null)
                    return cached;
            }

            Bitmap rendered = renderControlAsBitmap(_contentPresenter);

            if (visual is ISlideBitmapCacheable) {
                // Testing
                //((ISlideBitmapCacheable)visual).SetBitmap(rendered);
            }

            return rendered;
        }

        private Bitmap renderControlAsBitmap(Visual visual)
        {
            using (SKBitmap bitmap = new SKBitmap(1920, 1080))
            {
                using (SKCanvas canvas = new SKCanvas(bitmap)) {

                    using var contextImpl = DrawingContextHelper.WrapSkiaCanvas(canvas, SkiaPlatform.DefaultDpi);

                    using var renderedBitmap = new RenderTargetBitmap(new PixelSize(1920, 1080));
                    renderedBitmap.Render(visual);
                    contextImpl.DrawBitmap(renderedBitmap.PlatformImpl, 1,
                        new Rect(0, 0, 1920, 1080),
                        new Rect(0, 0, 1920, 1080));
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