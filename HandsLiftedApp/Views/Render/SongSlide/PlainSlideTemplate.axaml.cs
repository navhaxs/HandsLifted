using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views
{
    public partial class PlainSlideTemplate : UserControl
    {
        public PlainSlideTemplate()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
