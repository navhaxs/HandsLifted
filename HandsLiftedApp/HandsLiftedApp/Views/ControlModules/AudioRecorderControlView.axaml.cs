using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views.ControlModules
{
    public partial class AudioRecorderControlView : UserControl
    {
        public AudioRecorderControlView()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
