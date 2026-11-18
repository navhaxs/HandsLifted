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

    private static readonly Lazy<GoogleSignInCoordinator> _instance = new(() =>
        new GoogleSignInCoordinator(new GoogleSignInRunner(), new AvaloniaGoogleSignInUi(),
            () => (Globals.Instance.AppPreferences.GoogleClientId,
                Globals.Instance.AppPreferences.GoogleClientSecret)));

    /// <summary>The app-wide coordinator, shared by every sync and the Setup window.</summary>
    public static GoogleSignInCoordinator Instance => _instance.Value;

    private readonly IGoogleSignInRunner _runner;
    private readonly IGoogleSignInUi _ui;
    private readonly Func<(string? ClientId, string? ClientSecret)> _credentials;

    private readonly object _lock = new();
    private readonly List<IGoogleSyncWaiter> _waiters = new();
    private Task<GoogleSignInResult>? _running;

    // All of the following are read and written under _lock (the two counters are also readable lock-free).
    // The live modal of the running sign-in; null until Open returns and again once it has finished.
    private IGoogleSignInSession? _session;
    // A join without explanation arrived before Open returned the session; applied right after Open.
    private bool _skipExplanationRequested;
    private int _cancelEpoch;
    private int _successGeneration;

    /// <summary>
    /// Incremented each time a sign-in ends as <see cref="GoogleSignInOutcome.Cancelled"/> (the user cancelled
    /// or declined the explanation). Work that queued up before a cancel compares it with the value it
    /// captured when it was queued, so a cancel is not followed by one prompt per queued sync.
    /// </summary>
    public int CancelEpoch => Volatile.Read(ref _cancelEpoch);

    /// <summary>
    /// Incremented each time a sign-in succeeds, before its waiters are resumed. Lets resumed work that is
    /// still rejected report that failure once per sign-in rather than once per item.
    /// </summary>
    public int SuccessGeneration => Volatile.Read(ref _successGeneration);

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
    /// <param name="showExplanation">
    /// Whether a new sign-in starts on the explanation page. A request that joins a running sign-in with
    /// false makes that sign-in skip its explanation page (if it is still on it) and brings its modal to the front.
    /// </param>
    /// <param name="waiter">Notified when the sign-in ends; null when the caller handles the result itself.</param>
    public Task<GoogleSignInResult> RequestSignIn(Window? owner, bool showExplanation, IGoogleSyncWaiter? waiter)
    {
        Task<GoogleSignInResult> task;
        IGoogleSignInSession? sessionToSkip = null;
        lock (_lock)
        {
            if (waiter != null) _waiters.Add(waiter);

            if (_running != null)
            {
                // Joining. Without explanation the caller will not look for a modal, so skip its explanation
                // page. If the modal does not exist yet, remember the request; ExecuteAsync applies it after Open.
                if (!showExplanation)
                {
                    if (_session != null) sessionToSkip = _session;
                    else _skipExplanationRequested = true;
                }
                task = _running;
            }
            else
            {
                // RunAsync() takes _lock to finish, so it cannot clear _running before it is assigned here.
                task = _running = Task.Run(() => RunAsync(owner, showExplanation));
            }
        }

        // Never call into the session while holding the lock.
        if (sessionToSkip != null) SkipExplanation(sessionToSkip);
        return task;
    }

    private static void SkipExplanation(IGoogleSignInSession session)
    {
        try
        {
            session.Confirm();
            session.Activate();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Skipping the Google sign-in explanation for a joined request failed");
        }
    }

    private async Task<GoogleSignInResult> RunAsync(Window? owner, bool showExplanation)
    {
        var result = await ExecuteAsync(owner, showExplanation).ConfigureAwait(false);

        // Snapshot and clear under the lock so a request arriving from now on starts a fresh sign-in.
        // The counters change in the same critical section, before any waiter is notified.
        IGoogleSyncWaiter[] waiters;
        lock (_lock)
        {
            if (result.Outcome == GoogleSignInOutcome.Cancelled) Interlocked.Increment(ref _cancelEpoch);
            else if (result.Outcome == GoogleSignInOutcome.Success) Interlocked.Increment(ref _successGeneration);

            waiters = _waiters.ToArray();
            _waiters.Clear();
            _session = null;
            _skipExplanationRequested = false;
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

            // Publish the session so joins that arrive from now on act on it directly, and pick up a join
            // that arrived while Open was still running. Cleared before the session is disposed below.
            bool skipExplanation;
            lock (_lock)
            {
                _session = session;
                skipExplanation = _skipExplanationRequested;
            }

            try
            {
                if (skipExplanation) SkipExplanation(session);

                if (showExplanation && !await session.WaitForConfirmAsync().ConfigureAwait(false))
                    return new GoogleSignInResult(GoogleSignInOutcome.Cancelled);

                session.ShowWaiting();
                await _runner.SignInAsync(clientId, clientSecret, session.SetAuthUrl, session.UserCancelled)
                    .ConfigureAwait(false);
                return new GoogleSignInResult(GoogleSignInOutcome.Success);
            }
            finally
            {
                lock (_lock) _session = null;
            }
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
