using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Render
{
    public partial class RenderedSlideView : UserControl
    {
        public RenderedSlideView()
        {
            InitializeComponent();
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

        public static readonly DirectProperty<RenderedSlideView, Slide?> ActiveSlideProperty =
            AvaloniaProperty.RegisterDirect<RenderedSlideView, Slide?>(
                nameof(ActiveSlide),
                o => o.ActiveSlide,
                (o, v) => o.ActiveSlide = v);
    }
}