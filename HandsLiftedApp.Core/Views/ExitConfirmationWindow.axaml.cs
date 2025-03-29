using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Controls;
using HandsLiftedApp.Core.Views;

namespace HandsLiftedApp.Views.App
{
    public partial class ExitConfirmationWindow : Window
    {
        public MainWindow parentWindow;
        public ExitConfirmationWindow()
        {
            InitializeComponent();
            
            Win10DropshadowWorkaround.Register(this);

#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void OnExit(object? sender, RoutedEventArgs e)
        {
            Close();
            parentWindow.ExitApp();
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Close();
        }

    }
}
