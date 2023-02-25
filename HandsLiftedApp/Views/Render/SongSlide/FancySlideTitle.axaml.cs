using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views
{
    public partial class FancySlideTitle : UserControl
    {
        public FancySlideTitle()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
