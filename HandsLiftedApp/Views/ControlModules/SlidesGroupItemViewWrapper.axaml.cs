using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views.ControlModules
{
    public partial class SlidesGroupItemViewWrapper : UserControl
    {
        public SlidesGroupItemViewWrapper()
        {
            InitializeComponent();
        }


        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

    }
}
