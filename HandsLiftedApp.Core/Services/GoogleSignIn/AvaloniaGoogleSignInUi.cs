using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using HandsLiftedApp.Core.Views.Confirmation;
using Serilog;

namespace HandsLiftedApp.Core.Services.GoogleSignIn;

/// <summary>
/// Avalonia implementation of <see cref="IGoogleSignInUi"/>. The coordinator calls it from a
/// thread-pool thread, so everything that touches a window is marshalled to the UI thread here.
/// </summary>
public class AvaloniaGoogleSignInUi : IGoogleSignInUi
{
    public IGoogleSignInSession Open(Window? owner, bool startWithExplanation)
    {
        return Dispatcher.UIThread.Invoke(() =>
        {
            var window = new GoogleSignInWindow();
            var session = new Session(window, startWithExplanation);

            if (!startWithExplanation)
                window.ShowWaiting();

            if (owner != null)
            {
                try
                {
                    // Do not await: this must not block, and the dialog task completes when the window closes.
                    _ = window.ShowDialog(owner);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Showing the Google sign-in dialog over its owner failed, showing it non-modally");
                    window.Show();
                }
            }
            else
            {
                window.Show();
            }

            return (IGoogleSignInSession)session;
        });
    }

    public void ShowError(Window? owner, string title, string message)
    {
        // Already posts to the UI thread; it always uses the main window as owner.
        GoogleSlidesReauthWindow.ShowError(title, message);
    }

    private sealed class Session : IGoogleSignInSession
    {
        private readonly GoogleSignInWindow _window;
        // Deliberately never disposed: Cancel() on a disposed CTS throws, and a user-driven
        // close can race with Dispose. It holds no handles (no timers, no linked sources).
        private readonly CancellationTokenSource _cancelSource = new();
        private readonly TaskCompletionSource<bool> _confirm =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool _startWithExplanation;
        // Only read/written on the UI thread.
        private bool _closed;

        public Session(GoogleSignInWindow window, bool startWithExplanation)
        {
            _window = window;
            _startWithExplanation = startWithExplanation;

            window.SignInRequested += () => _confirm.TrySetResult(true);
            window.CancelRequested += OnUserCancelled;
            window.Closed += (_, _) =>
            {
                _closed = true;
                // Any close that was not ours already raised CancelRequested; make sure a pending
                // confirm never hangs even if the window closed silently.
                _confirm.TrySetResult(false);
            };
        }

        public CancellationToken UserCancelled => _cancelSource.Token;

        public Task<bool> WaitForConfirmAsync() =>
            _startWithExplanation ? _confirm.Task : Task.FromResult(true);

        public void ShowWaiting() => OnUi(() => _window.ShowWaiting());

        public void SetAuthUrl(string url) => OnUi(() => _window.SetAuthUrl(url));

        public void Dispose()
        {
            OnUi(() =>
            {
                try
                {
                    _window.CloseSilently();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Closing the Google sign-in dialog failed");
                }
            });
        }

        private void OnUserCancelled()
        {
            _confirm.TrySetResult(false);
            try
            {
                _cancelSource.Cancel();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "A Google sign-in cancellation callback threw");
            }
        }

        /// <summary>Runs on the UI thread, skipping the call if the window already closed.</summary>
        private void OnUi(Action action)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_closed) return;
                action();
            });
        }
    }
}
