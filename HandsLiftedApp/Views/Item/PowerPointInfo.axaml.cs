using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views.Item
{
    public partial class PowerPointInfo : UserControl
    {
        public PowerPointInfo()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
