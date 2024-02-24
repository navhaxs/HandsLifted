using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views.ControlModules
{
    public partial class VideoSlideControlViewWrapper : UserControl
    {
        public VideoSlideControlViewWrapper()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

    }
}
