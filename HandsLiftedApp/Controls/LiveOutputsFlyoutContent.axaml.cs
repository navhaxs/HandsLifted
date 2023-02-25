using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Controls
{
    public partial class LiveOutputsFlyoutContent : UserControl
    {
        public LiveOutputsFlyoutContent()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

    }
}
