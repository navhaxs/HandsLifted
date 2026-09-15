using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace HandsLiftedApp.Core.Views.Confirmation
{
    public partial class GoogleSlidesReauthWindow : Window
    {
        public bool Confirmed = false;

        public GoogleSlidesReauthWindow()
        {
            InitializeComponent();
        }

        // Error-only mode: single "OK" button, no Google sign-in triggered.
        public GoogleSlidesReauthWindow(string title, string message, bool isError)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            if (isError)
            {
                SignInButton.IsVisible = false;
                CancelButton.Content = "OK";
            }
        }

        /// <summary>
        /// Convenience wrapper for the error-only mode: shows a title/message/"OK" alert over the
        /// main window from any thread. Despite the class name, this is this codebase's only
        /// reusable generic alert — <see cref="GoogleSlidesGroupItemInstance"/>'s reauth-failure
        /// paths use it the same way.
        /// </summary>
        public static void ShowError(string title, string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var mainWindow =
                    (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                    ?.MainWindow;
                new GoogleSlidesReauthWindow(title, message, isError: true).ShowDialog(mainWindow);
            });
        }

        private void OnConfirm(object? sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
