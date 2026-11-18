using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace HandsLiftedApp.Core.Services.GoogleSignIn;

/// <summary>
/// Something (a Google Slides sync) that is paused waiting for sign-in to finish.
/// Exactly one of the two methods is called once the shared sign-in completes.
/// </summary>
public interface IGoogleSyncWaiter
{
    /// <summary>Sign-in succeeded; retry the paused work.</summary>
    void ResumeAfterSignIn();

    /// <summary>Sign-in was cancelled, failed or is not configured; give up on the paused work.</summary>
    void AbandonSignIn();
}

/// <summary>Performs the actual Google OAuth flow (browser + loopback listener).</summary>
public interface IGoogleSignInRunner
{
    /// <summary>
    /// Runs the sign-in. Calls <paramref name="onAuthUrl"/> once the Google URL is known.
    /// Throws <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    Task SignInAsync(string clientId, string clientSecret, Action<string> onAuthUrl,
        CancellationToken cancellationToken);
}

/// <summary>The modal shown for one sign-in. All members are callable from any thread.</summary>
public interface IGoogleSignInSession : IDisposable
{
    /// <summary>Cancelled when the user dismisses the modal.</summary>
    CancellationToken UserCancelled { get; }

    /// <summary>True when the user clicks Sign In on the explanation page, false when they cancel.</summary>
    Task<bool> WaitForConfirmAsync();

    /// <summary>Switch the modal to its "waiting for the browser" page.</summary>
    void ShowWaiting();

    /// <summary>Show the Google URL in the waiting page so the user can open or copy it.</summary>
    void SetAuthUrl(string url);

    // Dispose() closes the modal.
}

/// <summary>Creates sign-in modals and error dialogs.</summary>
public interface IGoogleSignInUi
{
    IGoogleSignInSession Open(Window? owner, bool startWithExplanation);
    void ShowError(Window? owner, string title, string message);
}

public enum GoogleSignInOutcome
{
    Success,
    Cancelled,
    Failed,
    NotConfigured,
}

public record GoogleSignInResult(GoogleSignInOutcome Outcome, string? ErrorMessage = null);
