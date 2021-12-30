using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views
{
    public partial class LowerThirdSlideTemplate : UserControl
    {
        public LowerThirdSlideTemplate()
        {
            InitializeComponent();

            System.Diagnostics.Debug.Print("Constructor LowerThirdSlideTemplate");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
