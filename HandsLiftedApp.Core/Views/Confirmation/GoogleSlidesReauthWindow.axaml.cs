using Avalonia.Controls;
using Avalonia.Interactivity;

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
