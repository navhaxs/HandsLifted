using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.XTransitioningContentControl;
using System;

namespace HandsLiftedApp.Views.Render
{
    public partial class SlideRenderer : UserControl
    {
        public SlideRenderer()
        {
            InitializeComponent();

            //if (IsLive == false)
            //{
            //    TransitioningContentControl root = this.FindControl<TransitioningContentControl>("root");
            //    root.PageTransition = null;
            //}
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Defines the <see cref="PageTransition"/> property.
        /// </summary>
        //public static readonly StyledProperty<IPageTransition?> PageTransitionProperty =
        //    AvaloniaProperty.Register<SlideRenderer, IPageTransition?>(nameof(PageTransition),
        //       null);
        public static readonly DirectProperty<SlideRenderer, IPageTransition?> PageTransitionProperty =
            AvaloniaProperty.RegisterDirect<SlideRenderer, IPageTransition?>(
                nameof(PageTransition),
                o => o.PageTransition,
                (o, v) =>
                {
                    o.PageTransition = v;
                });

        /// <summary>
        /// Gets or sets the animation played when content appears and disappears.
        /// </summary>
        private IPageTransition? _pageTransition;
        public IPageTransition? PageTransition
        {
            get => _pageTransition;
            set => SetAndRaise(PageTransitionProperty, ref _pageTransition, value);
        }

        public static readonly DirectProperty<SlideRenderer, bool> IsLiveProperty =
            AvaloniaProperty.RegisterDirect<SlideRenderer, bool>(
                nameof(IsLive),
                o => o.IsLive,
                (o, v) => o.IsLive = v);

        private bool _items = true;

        public bool IsLive
        {
            get { return _items; }
            set
            {
                SetAndRaise(IsLiveProperty, ref _items, value);
            }
        }

        /// <summary>
        /// Gets or sets the active slide
        /// </summary>
        private Slide? _activeSlide;
        public Slide? ActiveSlide
        {
            get => _activeSlide;
            set => SetAndRaise(ActiveSlideProperty, ref _activeSlide, value);
        }

        public static readonly DirectProperty<SlideRenderer, Slide?> ActiveSlideProperty =
            AvaloniaProperty.RegisterDirect<SlideRenderer, Slide?>(
                nameof(ActiveSlide),
                o => o.ActiveSlide,
                (o, v) => o.ActiveSlide = v);

    }
}
