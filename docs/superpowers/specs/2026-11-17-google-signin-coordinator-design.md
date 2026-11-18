# Google sign-in: cancellable modal + resume of pending syncs

## Intent
Google sign-in opens a browser tab. If the user closes the tab (or never finishes), the app waits forever:
the sync item stays `IsBusy`, and the single shared `ImportWorkerThread` is blocked, freezing every import
(PDF, PowerPoint, video, Google Slides). The Setup screen's sign-in has no way to cancel either.
Give the user a way to recover (get the sign-in link again) and a way out (cancel), and once a sign-in
succeeds, finish whatever syncs were waiting for it.

## Goals
- A blocking modal for the whole browser wait, with **Get link** (copy the Google sign-in URL, for a closed tab or another browser) and **Cancel**.
- Cancel always works and leaves nothing stuck (`IsBusy` cleared, import thread free).
- After any successful sign-in (from a sync or from Setup), every sync that was waiting on sign-in is re-run automatically.
- One sign-in at a time, however many syncs hit an expired token.

## Non-goals / anti-goals
- No timeout; only Cancel ends the wait.
- Not changing token storage, scopes, or the stored-token refresh logic in `Main.RunGoogleSlidesImportTask`.
- Anti-goal: a cancelled sign-in must not re-prompt once per queued sync, and must not show an error.
- Anti-goal: a sign-in wait must not block other imports.

## Current state (findings)
- `GoogleSlidesGroupItemInstance.Sync()` queues work on `ImportWorkerThread.priorityQueue`; the callback holds a static `syncSlidesLock`.
  On `TokenExpiredImportException` it shows `GoogleSlidesReauthWindow` (confirm only), then calls `Main.RevokeAndReauth` synchronously on the worker thread, with `CancellationToken.None`. Success does not resume the sync.
- `SetupWindow.SignInWithGoogle_OnClick` calls `GoogleWebAuthorizationBroker.AuthorizeAsync` with `CancellationToken.None`; only the button is disabled.
- The library's default receiver opens the browser itself and does not expose the URL.
- Playlist load calls `Sync()` on every Google Slides item, so several syncs can be waiting at once.

## Approaches considered
- **A. Retry inside the blocked thread.** Smallest change, but other imports stay frozen during sign-in, and resume is only a side effect of queue order (a Setup sign-in would resume nothing). Rejected.
- **B. Central sign-in coordinator (chosen).** One service owns sign-in; syncs register as waiting and release the import thread; one modal; waiters are re-queued on success and released quietly on cancel. More code, but it is the behavior asked for and fixes the shared-thread freeze.

## Design (approach B)
**`GoogleSignInCoordinator`** (Core/Services, app-wide singleton):
- `RequestSignIn(Window? owner, bool showExplanation, IGoogleSyncWaiter? waiter)`. If a sign-in is already running, just add the waiter and return. Otherwise start one.
- Sign-in runs off the UI and import threads, with a `CancellationTokenSource`. It uses `AuthorizationCodeInstalledApp` with a wrapper `ICodeReceiver` around `LocalServerCodeReceiver`. The wrapper captures the authorization URL (for Get link) and forwards cancellation. Token is stored via the existing `FileDataStore("token.json")`, deleting stale `token.json` first as `RevokeAndReauth` does today.
- Outcomes: **success** → close modal, re-run `Sync()` for each waiter. **Cancel** → close modal, release waiters (`IsBusy = false`), no dialog. **Error** → close modal, one error dialog (existing `GoogleSlidesReauthWindow.ShowError`), release waiters.
- Missing Client ID/Secret behavior stays as today (one "not configured" dialog).

**Modal** (extend `GoogleSlidesReauthWindow` or a sibling window, builder's call): two states in one window.
1. Explanation ("session expired, sign in") with *Sign In with Google* / *Cancel* — sync path only.
2. "Waiting for Google sign-in…" with *Copy link* / *Cancel*. Setup starts here.
Owner: MainWindow for the sync path, SetupWindow for Setup (resolve the owner by walking the logical tree, per CLAUDE.md).

**Sync path change:** `Sync()` catches `TokenExpiredImportException`, hands itself to the coordinator as a waiter, and returns, so the import thread and `syncSlidesLock` are released. `IsBusy` stays true until the coordinator re-queues or releases it.
**Setup change:** `SignInWithGoogle_OnClick` calls the coordinator (no explanation state); on success it also refreshes the status text and resumes any waiters.

## Constraints
- UI-thread rules per CLAUDE.md; modal must be shown/closed on the UI thread.
- Waiter is a small interface (e.g. `ResumeAfterSignIn()` / `AbandonSignIn()`) so the coordinator doesn't depend on `GoogleSlidesGroupItemInstance`; tests can fake it.
- Verify during build: `LocalServerCodeReceiver` can be wrapped to expose the URL and honors cancellation (listener released).

## Left to the builder
Exact window layout/wording, whether to extend the existing window or add a new one, file names, the waiter interface shape, unit tests for the coordinator (single sign-in for N waiters, cancel releases all, success resumes all, error shows one dialog), and a manual click-through of both entry points in the running app.
