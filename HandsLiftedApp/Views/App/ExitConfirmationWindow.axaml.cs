using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using HandsLiftedApp.Logic;

namespace HandsLiftedApp.Views.App
{
    public partial class ExitConfirmationWindow : Window
    {
        public ExitConfirmationWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void OnExit(object? sender, RoutedEventArgs e)
        {
            HandsLiftedWebServer.Stop();

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.Shutdown();
            }
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Close();
        }

    }
}
