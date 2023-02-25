using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views.ControlModules
{
    public partial class AutoAdvanceControlViewWrapper : UserControl
    {
        public AutoAdvanceControlViewWrapper()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
