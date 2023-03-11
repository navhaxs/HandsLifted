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

        //System.Timers.Timer aTimer = new System.Timers.Timer();
        public XTransitioningContentControl()
        {
            //aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            //aTimer.Enabled = true;
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

                //if (EnableSlideEvents)
                //{
                // call enter slide (e.g. ensure image will load here)
                //Dispatcher.UIThread.InvokeAsync(() => ((ISlideRender)content).OnEnterSlide());
                //}
                await Dispatcher.UIThread.InvokeAsync(() => ((ISlideRender)content).OnEnterSlide());

                // TODO:
                // TODO:
                // TODO:
                // TODO:
                // TODO:
                // TODO:
                // TODO:
                // TODO:
                // TODO: must ensure data is *ready* before proceeding. tricky if data load call was async :/
                // TODO:
                // TODO:
                // TODO:
                // TODO:
                // TODO:
                // TODO:

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
                _previousImageSite.Source = renderControlAsBitmap(_contentPresenter);
                _previousImageSite.IsVisible = true;
                _previousImageSite.Opacity = 1;
            }


            if (CurrentContent is ISlideRender && EnableSlideEvents)
            {
                ((ISlideRender)CurrentContent).OnLeaveSlide();
            }


            //_contentPresenterContainer.IsVisible = true;
            CurrentContent = content;
            if (_contentPresenterContainer != null)
                _contentPresenterContainer.ZIndex = (CurrentContent is IDynamicSlideRender) ? 999 : 0;
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render); // required to wait for images to load
            //_contentPresenterContainer.IsVisible = false;

            //if (content is IStaticSlideRender)
            //{
            _currentImageSite.IsVisible = true;
            _currentImageSite.Opacity = 0;
            _currentImageSite.Source = renderControlAsBitmap(_contentPresenter);
            //}
            //else
            //{
            //    _currentImageSite.IsVisible = false;
            //}

            // maybe what this could instead is take screenshot of existing (bitmap no alpha), then take screenshot of new (bitmap no alpha), then fade between the bitmaps. this way, there should be no alpha multiplier issues during the fade effect.

            //clock.PlayState = PlayState.Run;

            if (transition != null)
            {
                if (CurrentContent is IDynamicSlideRender)
                {
                    await transition.Start(_previousImageSite, null, true, _lastTransitionCts.Token);
                }
                else
                {
                    await transition.Start(_previousImageSite, _currentImageSite, true, _lastTransitionCts.Token);

                }
            }
            else
            {
                //_contentPresenterContainer.IsVisible = true;
                _previousImageSite.Opacity = 0;
            }

            //_contentPresenterContainer.IsVisible = false;
            _currentImageSite.IsVisible = true;
            _currentImageSite.Opacity = 1;

            //if (_previousImageSite != null)
            //{
            //    _previousImageSite.Opacity = 0;
            //}

            //if (PageTransition != null)
            //    await PageTransition.Start(null, this, true, _lastTransitionCts.Token);
        }

        // Specify what you want to happen when the Elapsed event is raised.
        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {

            //Dispatcher.UIThread.Post(() =>
            //{

            //    if (_contentPresenterContainer.Opacity != 1)
            //    {
            //        System.Diagnostics.Debug.Print($"{_previousImageSite.Opacity}+{_contentPresenterContainer.Opacity}={_previousImageSite.Opacity + _contentPresenterContainer.Opacity}");
            //    }
            //});
        }


        // TODO: pre-render outside of this control. there is just too much delay to be responsive by UX.
        private Bitmap renderControlAsBitmap(Visual visual)
        {
            using (SKBitmap bitmap = new SKBitmap(1920, 1080))
            {
                using (SKCanvas canvas = new SKCanvas(bitmap)) {

                    using var contextImpl = DrawingContextHelper.WrapSkiaCanvas(canvas, SkiaPlatform.DefaultDpi);
                    using var context = new DrawingContext(contextImpl);

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