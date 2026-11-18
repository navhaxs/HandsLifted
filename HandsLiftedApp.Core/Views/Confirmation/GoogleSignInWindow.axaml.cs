using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Serilog;

namespace HandsLiftedApp.Core.Views.Confirmation
{
    /// <summary>
    /// One modal with two pages: the "sign in required" explanation and the "waiting for the browser"
    /// page. Must only be touched on the UI thread. <see cref="CancelRequested"/> fires when the user
    /// dismisses the window by any means (Cancel, Esc, close), but NOT when <see cref="CloseSilently"/> closed it.
    /// </summary>
    public partial class GoogleSignInWindow : Window
    {
        private string? _authUrl;
        private bool _closedSilently;
        private bool _cancelRaised;

        /// <summary>The user clicked Sign In on the explanation page. The window stays open.</summary>
        public event Action? SignInRequested;

        /// <summary>The user dismissed the window (Cancel, Esc or any other close). Raised at most once.</summary>
        public event Action? CancelRequested;

        public GoogleSignInWindow()
        {
            InitializeComponent();
        }

        public void ShowExplanation()
        {
            Title = "Google Sign-In Required";
            TitleText.Text = "Google Sign-In Required";
            MessageText.Text =
                "Your Google account session has expired. Click Sign In to open a browser window and re-authenticate.";
            SignInButton.IsVisible = true;
            SignInButton.IsEnabled = true;
            SignInButton.IsDefault = true;
            CopyLinkButton.IsVisible = false;
        }

        public void ShowWaiting()
        {
            Title = "Waiting for Google sign-in…";
            TitleText.Text = "Waiting for Google sign-in…";
            MessageText.Text =
                "Finish signing in in your browser. If you closed the tab, copy the link and open it in a browser.";
            SignInButton.IsVisible = false;
            SignInButton.IsDefault = false;
            CopyLinkButton.IsVisible = true;
            CopyLinkButton.IsEnabled = _authUrl != null;
        }

        public void SetAuthUrl(string url)
        {
            _authUrl = url;
            CopyLinkButton.IsEnabled = true;
        }

        /// <summary>Closes the window without raising <see cref="CancelRequested"/>.</summary>
        public void CloseSilently()
        {
            _closedSilently = true;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled && e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (!_closedSilently)
                RaiseCancel();
        }

        private void RaiseCancel()
        {
            if (_cancelRaised) return;
            _cancelRaised = true;
            CancelRequested?.Invoke();
        }

        private void OnSignIn(object? sender, RoutedEventArgs e)
        {
            // Stay open: the coordinator calls ShowWaiting() next. Disable so a double click does nothing.
            SignInButton.IsEnabled = false;
            SignInRequested?.Invoke();
        }

        private async void OnCopyLink(object? sender, RoutedEventArgs e)
        {
            var url = _authUrl;
            if (url == null) return;
            try
            {
                var clipboard = GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                    await clipboard.SetTextAsync(url);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not copy Google sign-in link to the clipboard");
            }
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
