using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views.Render.CustomSlide
{
    public partial class BibleReadingSlideTemplate : UserControl
    {
        public BibleReadingSlideTemplate()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
