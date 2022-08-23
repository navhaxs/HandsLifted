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
        private object? _currentContent;
        Image _previousImageSite;
        ContentPresenter _contentPresenter;
        Grid _contentPresenterContainer;

        /// <summary>
        /// Defines the <see cref="PageTransition"/> property.
        /// </summary>
        public static readonly StyledProperty<IPageTransition?> PageTransitionProperty =
            AvaloniaProperty.Register<XTransitioningContentControl, IPageTransition?>(nameof(PageTransition),
                //new CrossFade(TimeSpan.FromSeconds(0.125)));
                new XFade(TimeSpan.FromSeconds(0.300)));

        /// <summary>
        /// Defines the <see cref="CurrentContent"/> property.
        /// </summary>
        public static readonly DirectProperty<XTransitioningContentControl, object?> CurrentContentProperty =
            AvaloniaProperty.RegisterDirect<XTransitioningContentControl, object?>(nameof(CurrentContent),
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

        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
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
            _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
            _contentPresenterContainer = e.NameScope.Find<Grid>("PART_ContentPresenterContainer");
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

            //if (Object.Equals(CurrentContent, content))
            //{
            //    return;
            //}

            var clock = AvaloniaLocator.Current.GetService<IGlobalClock>();
            clock.PlayState = PlayState.Pause;

            _lastTransitionCts?.Cancel();
            _lastTransitionCts = new CancellationTokenSource();

            if (_previousImageSite != null)
            {
                //_contentPresenterContainer.IsVisible = true;
                //_contentPresenterContainer.Opacity = 1;

                _previousImageSite.Source = renderControlAsBitmap(_contentPresenter);
                _previousImageSite.IsVisible = true;
                _previousImageSite.Opacity = 1;
            }

            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render); // required to wait for images to load

            if (CurrentContent is ISlideRender)
            {
                ((ISlideRender)CurrentContent).OnLeaveSlide();
            }

            _contentPresenterContainer.IsVisible = false;

            CurrentContent = content;

            // maybe what this could instead is take screenshot of existing (bitmap no alpha), then take screenshot of new (bitmap no alpha), then fade between the bitmaps. this way, there should be no alpha multiplier issues during the fade effect.

            clock.PlayState = PlayState.Run;

            if (PageTransition != null)
            {
                //aTimer.Start();

                await PageTransition.Start(_previousImageSite, _contentPresenterContainer, true, _lastTransitionCts.Token);


            }

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

        private Bitmap renderControlAsBitmap(IVisual visual)
        {
            SKBitmap bitmap = new SKBitmap(1920, 1080);
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                // render the Avalonia visual into the buffer
                IDrawingContextImpl m = DrawingContextHelper.WrapSkiaCanvas(canvas, SkiaPlatform.DefaultDpi);

                if (visual is not null)
                {
                    using var context = new DrawingContext(m);
                    ImmediateRenderer.Render(visual, context);
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