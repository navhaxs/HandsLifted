using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Serilog;

namespace HandsLiftedApp.Core.Services.GoogleSignIn;

/// <summary>
/// Runs at most one Google sign-in at a time and shares it between every caller.
/// Requests that arrive while a sign-in is running join it; each registered waiter is resumed
/// on success or abandoned otherwise.
/// </summary>
public class GoogleSignInCoordinator
{
    private const string NotConfiguredTitle = "Google Sign-In Not Configured";
    private const string NotConfiguredMessage =
        "Google Client ID and Client Secret are not set. Open Setup and enter them before syncing Google Slides.";
    private const string FailedTitle = "Google Sign-In Failed";

    private readonly IGoogleSignInRunner _runner;
    private readonly IGoogleSignInUi _ui;
    private readonly Func<(string? ClientId, string? ClientSecret)> _credentials;

    private readonly object _lock = new();
    private readonly List<IGoogleSyncWaiter> _waiters = new();
    private Task<GoogleSignInResult>? _running;

    public GoogleSignInCoordinator(IGoogleSignInRunner runner, IGoogleSignInUi ui,
        Func<(string? ClientId, string? ClientSecret)> credentials)
    {
        _runner = runner;
        _ui = ui;
        _credentials = credentials;
    }

    /// <summary>
    /// Starts a sign-in, or joins the one already running. The returned task completes when
    /// the sign-in has finished and every waiter has been notified. Never blocks the caller.
    /// </summary>
    /// <param name="waiter">Notified when the sign-in ends; null when the caller handles the result itself.</param>
    public Task<GoogleSignInResult> RequestSignIn(Window? owner, bool showExplanation, IGoogleSyncWaiter? waiter)
    {
        lock (_lock)
        {
            if (waiter != null) _waiters.Add(waiter);
            // RunAsync() takes _lock to finish, so it cannot clear _running before it is assigned here.
            _running ??= Task.Run(() => RunAsync(owner, showExplanation));
            return _running;
        }
    }

    private async Task<GoogleSignInResult> RunAsync(Window? owner, bool showExplanation)
    {
        var result = await ExecuteAsync(owner, showExplanation).ConfigureAwait(false);

        // Snapshot and clear under the lock so a request arriving from now on starts a fresh sign-in.
        IGoogleSyncWaiter[] waiters;
        lock (_lock)
        {
            waiters = _waiters.ToArray();
            _waiters.Clear();
            _running = null;
        }

        foreach (var waiter in waiters)
        {
            try
            {
                if (result.Outcome == GoogleSignInOutcome.Success) waiter.ResumeAfterSignIn();
                else waiter.AbandonSignIn();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Google sign-in waiter callback threw");
            }
        }

        // Only syncs get a dialog; the Setup window shows its own inline status.
        if (waiters.Length > 0)
        {
            try
            {
                switch (result.Outcome)
                {
                    case GoogleSignInOutcome.NotConfigured:
                        _ui.ShowError(owner, NotConfiguredTitle, NotConfiguredMessage);
                        break;
                    case GoogleSignInOutcome.Failed:
                        _ui.ShowError(owner, FailedTitle,
                            $"Could not complete Google sign-in: {result.ErrorMessage}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Showing the Google sign-in error dialog failed");
            }
        }

        return result;
    }

    private async Task<GoogleSignInResult> ExecuteAsync(Window? owner, bool showExplanation)
    {
        // Captured once the modal exists so the catch below can tell a user cancel from any other cancellation.
        var userCancelled = CancellationToken.None;
        try
        {
            var (clientId, clientSecret) = _credentials();
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                Log.Information("Google sign-in requested but client id/secret are not configured");
                return new GoogleSignInResult(GoogleSignInOutcome.NotConfigured);
            }

            using var session = _ui.Open(owner, showExplanation);
            userCancelled = session.UserCancelled;

            if (showExplanation && !await session.WaitForConfirmAsync().ConfigureAwait(false))
                return new GoogleSignInResult(GoogleSignInOutcome.Cancelled);

            session.ShowWaiting();
            await _runner.SignInAsync(clientId, clientSecret, session.SetAuthUrl, session.UserCancelled)
                .ConfigureAwait(false);
            return new GoogleSignInResult(GoogleSignInOutcome.Success);
        }
        catch (OperationCanceledException) when (userCancelled.IsCancellationRequested)
        {
            return new GoogleSignInResult(GoogleSignInOutcome.Cancelled);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Google sign-in failed");
            return new GoogleSignInResult(GoogleSignInOutcome.Failed, ex.Message);
        }
    }
}
