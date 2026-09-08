using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HandsLiftedApp.Core.Views
{
    public partial class OnlineVideoUrlDialog : Window
    {
        public string? Result { get; private set; }

        public OnlineVideoUrlDialog()
        {
            InitializeComponent();
        }

        private void OnUrlTextChanged(object? sender, TextChangedEventArgs e)
        {
            var text = UrlTextBox.Text?.Trim() ?? "";
            var isValid = Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                          (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            AddButton.IsEnabled = isValid;
            HintText.Text = text.Length > 0 && !isValid ? "Enter a valid http(s) URL." : "";
        }

        private void OnConfirmAdd(object? sender, RoutedEventArgs e)
        {
            var text = UrlTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            Result = text;
            Close();
        }

        private void OnCancel(object? sender, RoutedEventArgs e) => Close();
    }
}
