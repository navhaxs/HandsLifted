using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views.Preferences
{
    public partial class PreferencesWindow : Window
    {
        public PreferencesWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
