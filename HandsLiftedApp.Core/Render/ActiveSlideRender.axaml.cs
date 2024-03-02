using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Render
{
    public partial class ActiveSlideRender : UserControl
    {
        public ActiveSlideRender()
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

        public static readonly DirectProperty<ActiveSlideRender, Slide?> ActiveSlideProperty =
            AvaloniaProperty.RegisterDirect<ActiveSlideRender, Slide?>(
                nameof(ActiveSlide),
                o => o.ActiveSlide,
                (o, v) => o.ActiveSlide = v);
    }
}