using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views.ControlModules
{
    public partial class AutoAdvanceControlView : UserControl
    {
        public AutoAdvanceControlView()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
