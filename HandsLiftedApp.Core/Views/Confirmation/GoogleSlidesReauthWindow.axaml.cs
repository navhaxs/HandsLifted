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
