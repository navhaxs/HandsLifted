# Google Sign-In Coordinator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Google sign-in cancellable (modal with Get link / Cancel) and automatically re-run every sync that was waiting on it once sign-in succeeds.

**Architecture:** A single app-wide `GoogleSignInCoordinator` runs one sign-in at a time. Syncs that hit an expired token register as waiters and free the shared import thread; Setup calls the same coordinator. The coordinator is pure orchestration over three small interfaces (runner, UI, waiter) so it is unit-testable; the Google-library runner and the Avalonia modal are thin implementations.

**Tech Stack:** C# / .NET 10, Avalonia, ReactiveUI, Google.Apis.Auth (`AuthorizationCodeInstalledApp`, `LocalServerCodeReceiver`), MSTest.

**Spec:** `docs/superpowers/specs/2026-11-17-google-signin-coordinator-design.md`

## Global Constraints

- No `find`/`grep` shellouts; use Glob/Grep tools scoped to the project dir (CLAUDE.md).
- Serilog `Log.*` messages: use "-" not an em dash.
- Tests: MSTest, in `HandsLiftedApp.Tests`. Tests touching `ReactiveObject` pipelines run in `DispatcherTestThread.Run(...)`; call `UndoStack.Clear()` after object-initializer setup (CLAUDE.md).
- Avalonia popups: any window opened from a flyout resolves its owner by walking the logical tree to a `Window`, never `TopLevel.GetTopLevel(...) as Window` (not needed here: Setup passes `this`, sync passes `MainWindow`).
- No timeout on the sign-in wait; only Cancel (or closing the modal) ends it.
- Cancelled sign-in: no error dialog, no re-prompt per queued sync. A sign-in wait must not block the import worker thread.
- Do not change token scopes, token file location (`token.json`), or the stored-token refresh logic in `Main.RunGoogleSlidesImportTask`.
- Core `Services` live under `HandsLiftedApp.Core/Services/`; new files go in `HandsLiftedApp.Core/Services/GoogleSignIn/`.

## Review Focus

Failure modes the spec implies; each is pinned by a named test in the task that owns the code.

1. Several syncs waiting at once must produce exactly one modal and resume all of them (Task 1: `OneSignInServesAllWaiters_AndResumesEachOnSuccess`).
2. User declines on the explanation screen: the browser must never open (Task 1: `DecliningTheExplanation_NeverRunsTheRunner`).
3. Browser step throws (user denies access in Google, port in use): one error dialog, waiters released, and the coordinator is not stuck "running" so the next request works (Task 1: `RunnerFailure_ShowsOneErrorDialog_AbandonsWaiters_AndAllowsNextRequest`).
4. Client ID/Secret missing: one "not configured" dialog, waiters released, no browser (Task 1: `MissingCredentials_...`).
5. Receiver ignores cancellation (library quirk): Cancel must still return promptly (Task 2: `Cancel_ReturnsPromptly_EvenIfInnerReceiverIgnoresCancellation`).

---

### Task 1: Coordinator and contracts

**Files:**
- Create: `HandsLiftedApp.Core/Services/GoogleSignIn/GoogleSignInContracts.cs`
- Create: `HandsLiftedApp.Core/Services/GoogleSignIn/GoogleSignInCoordinator.cs`
- Test: `HandsLiftedApp.Tests/Services/GoogleSignInCoordinatorTests.cs`

**Interfaces:**
- Produces (namespace `HandsLiftedApp.Core.Services.GoogleSignIn`):
  - `interface IGoogleSyncWaiter { void ResumeAfterSignIn(); void AbandonSignIn(); }`
  - `interface IGoogleSignInRunner { Task SignInAsync(string clientId, string clientSecret, Action<string> onAuthUrl, CancellationToken cancellationToken); }` — throws `OperationCanceledException` on cancel; calls `onAuthUrl` once the Google URL is known.
  - `interface IGoogleSignInSession : IDisposable { CancellationToken UserCancelled { get; } Task<bool> WaitForConfirmAsync(); void ShowWaiting(); void SetAuthUrl(string url); }` — `Dispose` closes the modal; `WaitForConfirmAsync` is true when the user clicks Sign In, false on cancel; all members callable from any thread.
  - `interface IGoogleSignInUi { IGoogleSignInSession Open(Window? owner, bool startWithExplanation); void ShowError(Window? owner, string title, string message); }`
  - `enum GoogleSignInOutcome { Success, Cancelled, Failed, NotConfigured }`; `record GoogleSignInResult(GoogleSignInOutcome Outcome, string? ErrorMessage = null)`
  - `class GoogleSignInCoordinator` with ctor `(IGoogleSignInRunner runner, IGoogleSignInUi ui, Func<(string? ClientId, string? ClientSecret)> credentials)`, and `Task<GoogleSignInResult> RequestSignIn(Window? owner, bool showExplanation, IGoogleSyncWaiter? waiter)`.

- [ ] **Step 1: Write the failing tests** in `GoogleSignInCoordinatorTests` with hand-written fakes (`FakeRunner` driven by a `TaskCompletionSource` and honouring the token, `FakeUi`/`FakeSession` recording `Open`/`ShowError` call counts and exposing `Confirm(bool)`, `Cancel()`, `FakeWaiter` counting resume/abandon calls). Tests, all `async Task`, passing `owner: null`:
  - `OneSignInServesAllWaiters_AndResumesEachOnSuccess` — three waiters requested while the runner is pending; assert `Open` called once, runner called once; completing the runner gives `Success` and each waiter `Resumed == 1`, `Abandoned == 0`.
  - `DecliningTheExplanation_NeverRunsTheRunner` — `showExplanation: true`, session confirm returns false; assert outcome `Cancelled`, runner call count 0, waiter abandoned.
  - `Cancel_AbandonsEveryWaiter_WithoutErrorDialog` — two waiters, session `Cancel()` while runner pending; outcome `Cancelled`, both abandoned, `ShowError` count 0.
  - `RunnerFailure_ShowsOneErrorDialog_AbandonsWaiters_AndAllowsNextRequest` — runner throws `InvalidOperationException("boom")` with two waiters: outcome `Failed` with message "boom", `ShowError` count 1, both abandoned; a following `RequestSignIn` calls `Open` a second time.
  - `MissingCredentials_ShowsNotConfiguredOnce_AndAbandonsWaiters` — credentials func returns blank values; outcome `NotConfigured`, `ShowError` count 1 with title "Google Sign-In Not Configured", runner count 0, waiter abandoned.
  - `RequestWhileRunning_JoinsTheSameSession` — second request with `waiter: null` (the Setup case) during a pending sign-in returns a task whose result equals the first's and `Open` count stays 1.
  - `NewRequestAfterCancel_StartsAFreshSignIn` — cancel one sign-in, request again, `Open` count 2 and the second can succeed.
- [ ] **Step 2: Run to verify failure**
  Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~GoogleSignInCoordinatorTests"`
  Expected: build fails (types not defined).
- [ ] **Step 3: Implement the contracts file and `GoogleSignInCoordinator`.** State is guarded by one lock: a `List<IGoogleSyncWaiter>` of waiters, a shared `Task<GoogleSignInResult>?` for the running sign-in. `RequestSignIn` adds the waiter and returns the shared task if one is running, otherwise creates it. The run (started with `Task.Run`, never blocking the caller): validate credentials; `using var session = ui.Open(owner, showExplanation)`; if `showExplanation` and `!await session.WaitForConfirmAsync()` → Cancelled; `session.ShowWaiting()`; `await runner.SignInAsync(id, secret, session.SetAuthUrl, session.UserCancelled)`; map `OperationCanceledException` → Cancelled, other exceptions → Failed (log with Serilog). Finish: under the lock snapshot the waiters, clear them, and null the running task; then outside the lock — Success → `ResumeAfterSignIn()` each; otherwise `AbandonSignIn()` each. Show `ui.ShowError` (once) only for `Failed` and `NotConfigured`, and only when the snapshot had at least one waiter (Setup shows its own inline status). Error title/message for NotConfigured: "Google Sign-In Not Configured" / "Google Client ID and Client Secret are not set. Open Setup and enter them before syncing Google Slides."; for Failed: "Google Sign-In Failed" / "Could not complete Google sign-in: {message}". Waiter callbacks must be wrapped so one throwing waiter does not stop the others.
- [ ] **Step 4: Run to verify pass** — same command; Expected: 7 tests PASS.
- [ ] **Step 5: Commit** — `git add HandsLiftedApp.Core/Services/GoogleSignIn HandsLiftedApp.Tests/Services; git commit -m "google sign-in: coordinator that runs one cancellable sign-in and resumes waiting syncs"`

### Task 2: Google-library runner and capturing receiver

**Files:**
- Create: `HandsLiftedApp.Core/Services/GoogleSignIn/CapturingCodeReceiver.cs`
- Create: `HandsLiftedApp.Core/Services/GoogleSignIn/GoogleSignInRunner.cs`
- Modify: `HandsLiftedApp.GoogleSlidesImporter/Main.cs` (`Scopes` → `public static readonly string[]` only; leave `RevokeAndReauth` in place, Task 4 deletes it together with its only caller)
- Test: `HandsLiftedApp.Tests/Services/CapturingCodeReceiverTests.cs`

**Interfaces:**
- Consumes: `IGoogleSignInRunner` (Task 1); `Main.Scopes`.
- Produces: `class CapturingCodeReceiver : ICodeReceiver` with ctor `(ICodeReceiver inner, Action<string> onAuthUrl)`; `class GoogleSignInRunner : IGoogleSignInRunner` (parameterless ctor).

- [ ] **Step 1: Write the failing tests** in `CapturingCodeReceiverTests` using a fake `ICodeReceiver` (`RedirectUri` returns a fixed string; `ReceiveCodeAsync` returns a `TaskCompletionSource` task): 
  - `ForwardsRedirectUri_AndReportsTheAuthUrlOnce` — build an `AuthorizationCodeRequestUrl(new Uri("https://accounts.example/auth")) { ClientId = "id", RedirectUri = "http://localhost:1/authorize/" }`; assert the captured string equals `url.Build().AbsoluteUri` and the inner receiver got the same url object.
  - `Cancel_ReturnsPromptly_EvenIfInnerReceiverIgnoresCancellation` — inner never completes; cancel the token; assert the call throws `OperationCanceledException` (use `Assert.ThrowsExactlyAsync` with `TaskCanceledException` or catch-based check) within one second.
- [ ] **Step 2: Run to verify failure** — `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~CapturingCodeReceiverTests"`; Expected: build fails.
- [ ] **Step 3: Implement `CapturingCodeReceiver`**: `RedirectUri` delegates to inner; `ReceiveCodeAsync(AuthorizationCodeRequestUrl url, CancellationToken ct)` invokes `onAuthUrl(url.Build().AbsoluteUri)` then returns `inner.ReceiveCodeAsync(url, ct).WaitAsync(ct)` (WaitAsync guarantees prompt cancellation even if the library's listener lingers).
- [ ] **Step 4: Implement `GoogleSignInRunner.SignInAsync`.** Build `new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer { ClientSecrets, Scopes = Main.Scopes, DataStore = <in-memory no-op store> })` (this mirrors what `GoogleWebAuthorizationBroker` builds, so token behaviour is unchanged; use `NullDataStore` if present in the referenced Google.Apis, else a ten-line dictionary-backed `IDataStore`). The throw-away store is deliberate: it forces the browser to open every time (a valid or revoked token on disk is never silently reused) and means Cancel never destroys an existing token. Authorize with `new AuthorizationCodeInstalledApp(flow, new CapturingCodeReceiver(new LocalServerCodeReceiver(), onAuthUrl)).AuthorizeAsync("user", cancellationToken)`, then store `credential.Token` under key `"user"` in `new FileDataStore("token.json", true)` via `StoreAsync<TokenResponse>`.
- [ ] **Step 5: Run to verify pass** — same filter; Expected: 2 PASS. Then `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj` — Expected: success.
- [ ] **Step 6: Commit** — `git add -A HandsLiftedApp.Core/Services/GoogleSignIn HandsLiftedApp.Tests/Services HandsLiftedApp.GoogleSlidesImporter/Main.cs; git commit -m "google sign-in: capturing receiver and runner that never reuses or destroys the stored token"`

### Task 3: Modal window and Avalonia UI adapter

**Files:**
- Create: `HandsLiftedApp.Core/Views/Confirmation/GoogleSignInWindow.axaml` and `.axaml.cs`
- Create: `HandsLiftedApp.Core/Services/GoogleSignIn/AvaloniaGoogleSignInUi.cs`
- Leave `GoogleSlidesReauthWindow` untouched (still the generic alert via `ShowError`).

**Interfaces:**
- Consumes: `IGoogleSignInUi`, `IGoogleSignInSession` (Task 1).
- Produces: `class AvaloniaGoogleSignInUi : IGoogleSignInUi`; `class GoogleSignInWindow : Window` with methods `ShowExplanation()`, `ShowWaiting()`, `SetAuthUrl(string)` and events/properties it needs for the session adapter.

- [ ] **Step 1: Build `GoogleSignInWindow`** styled like `GoogleSlidesReauthWindow` (same border/background resources, `WindowDecorations="None"`, centered on owner, ~400px wide, same `dialog-buttons` class). Two states in one window. Explanation: title "Google Sign-In Required", the existing message text ("Your Google account session has expired. Click Sign In to open a browser window and re-authenticate."), buttons *Sign In with Google* (default) and *Cancel*. Waiting: title "Waiting for Google sign-in…", message "Finish signing in in your browser. If you closed the tab, copy the link and open it in a browser.", buttons *Copy link* (disabled until `SetAuthUrl` is called; copies via the window's clipboard) and *Cancel*. Esc and closing the window count as Cancel.
- [ ] **Step 2: Implement `AvaloniaGoogleSignInUi`.** `Open` creates the window on the UI thread (`Dispatcher.UIThread.Invoke`), shows it with `ShowDialog(owner)` when `owner != null` (else `Show()`), and returns an `IGoogleSignInSession` whose members marshal to the UI thread: `UserCancelled` is a `CancellationTokenSource` token cancelled on Cancel/close; `WaitForConfirmAsync` is a `TaskCompletionSource<bool>` completed true by Sign In, false by Cancel/close (and returns true immediately when `startWithExplanation` was false); `Dispose` closes the window without raising cancel. `ShowError` reuses `GoogleSlidesReauthWindow.ShowError`.
- [ ] **Step 3: Build** — `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`; Expected: success.
- [ ] **Step 4: Commit** — `git add HandsLiftedApp.Core/Views/Confirmation HandsLiftedApp.Core/Services/GoogleSignIn; git commit -m "google sign-in: modal with Copy link and Cancel, and its Avalonia adapter"`

### Task 4: Sync path uses the coordinator

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/GoogleSlidesGroupItemInstance.cs` (`Sync()` catch block at lines 230-292, and the trailing `IsBusy = false` at line 318)
- Modify: `HandsLiftedApp.Core/Services/GoogleSignIn/GoogleSignInCoordinator.cs` (add the real `Instance`)
- Modify: `HandsLiftedApp.GoogleSlidesImporter/Main.cs` (delete `RevokeAndReauth`)
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/GoogleSlidesGroupItemInstanceSignInTests.cs`

**Interfaces:**
- Consumes: `IGoogleSyncWaiter`, `GoogleSignInCoordinator.Instance`, `RequestSignIn`.
- Produces: `GoogleSlidesGroupItemInstance` implements `IGoogleSyncWaiter`; `public static GoogleSignInCoordinator Instance { get; }` on the coordinator (added in this task, not Task 1).

- [ ] **Step 1: Write the failing test** `AbandonSignIn_ClearsIsBusy` — construct the instance the way `ImportInteractionTests.Group` constructs items (inside `DispatcherTestThread.Run`), set `IsBusy = true`, call `AbandonSignIn()`, assert `IsBusy == false`.
- [ ] **Step 2: Run to verify failure** — `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~GoogleSlidesGroupItemInstanceSignInTests"`; Expected: build fails (`AbandonSignIn` missing).
- [ ] **Step 3: Implement the waiter on `GoogleSlidesGroupItemInstance`.** `AbandonSignIn()` sets `IsBusy = false`. `ResumeAfterSignIn()` sets a private `_resumedAfterSignIn = true`, sets `IsBusy = false` (`Sync()` returns early when busy), then calls `Sync()`.
- [ ] **Step 4: Replace the `TokenExpiredImportException` catch body in `Sync()`.** Read and clear `_resumedAfterSignIn` into a local at the top of the queued callback. In the catch: if that local is true (a fresh sign-in was just accepted yet Google still rejects the token), do **not** request another sign-in; show `GoogleSlidesReauthWindow.ShowError("Google Sign-In Failed", "Google did not accept the new sign-in. Try signing in again from Setup.")` and let `IsBusy` clear normally. Otherwise call `GoogleSignInCoordinator.Instance.RequestSignIn(MainWindow, showExplanation: true, waiter: this)` (MainWindow resolved as the existing code does via `IClassicDesktopStyleApplicationLifetime`), set a local `handedOff = true`, and skip the trailing `IsBusy = false` when `handedOff` so the item stays busy while it waits. The call must not block (the coordinator runs the sign-in on its own task), so the import worker thread and `syncSlidesLock` are released immediately. Delete the old reauth dialog/`tcs.GetAwaiter().GetResult()`/`RevokeAndReauth` code and the not-configured dialog (the coordinator now owns it).
- [ ] **Step 5: Delete `Main.RevokeAndReauth` (`HandsLiftedApp.GoogleSlidesImporter/Main.cs:209-221`)** now that its only caller is gone, and add `GoogleSignInCoordinator.Instance` (a new property, absent from Task 1) as a lazily created singleton: `new GoogleSignInCoordinator(new GoogleSignInRunner(), new AvaloniaGoogleSignInUi(), () => (Globals.Instance.AppPreferences.GoogleClientId, Globals.Instance.AppPreferences.GoogleClientSecret))`.
- [ ] **Step 6: Run to verify pass, then the whole suite** — filter above then `dotnet test HandsLiftedApp.Tests`; Expected: all PASS (confirm no other caller of `RevokeAndReauth` via the Grep tool over `*.cs` in the repo, per CLAUDE.md; `HandsLiftedApp.GoogleSlidesImporter/Program.cs` is a separate console tool with its own call and stays).
- [ ] **Step 7: Commit** — `git add HandsLiftedApp.Core HandsLiftedApp.Tests; git commit -m "google slides sync: hand expired-token syncs to the sign-in coordinator and resume them after sign-in"`

### Task 5: Setup window uses the coordinator, then end-to-end check

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml.cs` (`SignInWithGoogle_OnClick`, lines 200-243)

**Interfaces:**
- Consumes: `GoogleSignInCoordinator.Instance.RequestSignIn(this, showExplanation: false, waiter: null)` returning `GoogleSignInResult`.

- [ ] **Step 1: Make `SignInWithGoogle_OnClick` `async void`.** Keep the existing blank Client ID/Secret check and inline status. Disable the button, await `RequestSignIn(this, false, null)` (no `Task.Run`/`.Wait()`), re-enable it, then: `Success` → `RefreshGoogleSignInStatus()`; `Cancelled` → status "Sign-in cancelled." (Neutral); `Failed` → "Sign-in failed: {ErrorMessage}" (Error); `NotConfigured` → "Enter Client ID and Client Secret first." (Error). Remove the now-unused `GoogleWebAuthorizationBroker`/`FileDataStore` usage from this method only (the status check below still uses both).
- [ ] **Step 2: Build and run the suite** — `dotnet build HandsLiftedApp.Desktop/HandsLiftedApp.Desktop.csproj` then `dotnet test HandsLiftedApp.Tests`; Expected: success, all PASS.
- [ ] **Step 3: Manual click-through (cannot be unit-tested; required before calling this done).** Run the app and check each: (a) Setup → Sign in → modal appears over Setup; close the browser tab; *Copy link* then paste into a browser completes sign-in and the modal closes with status "Signed in."; (b) Setup → Sign in → *Cancel* closes modal, status "Sign-in cancelled.", previous token still valid; (c) delete/expire `token.json` with two Google Slides items in a playlist, load it: one explanation modal, then waiting state; sign in; **both** items sync without further action; (d) same setup, *Cancel* during waiting: both items stop showing busy, no error popup, other imports (e.g. a PDF added meanwhile) still import during the wait; (e) Esc on the modal behaves as Cancel. Report which of these were run.
- [ ] **Step 4: Commit** — `git add HandsLiftedApp.Core/Views/Setup; git commit -m "setup: sign in through the coordinator so it can be cancelled"`

---

## Self-review notes
- Spec coverage: modal two states + Get link + Cancel (T3); one sign-in at a time and single dialogs (T1); resume all / abandon all (T1, T4); free import thread (T4); Setup shares the flow (T5); receiver URL capture and cancellation (T2); no timeout (constraint, no code); error dialog once (T1).
- Calls beyond the spec, flagged for the user: the runner authorizes against a throw-away store and copies the token into `token.json` only on success (so Setup always opens the browser, enabling account switching, and Cancel never loses a valid token); a post-sign-in `TokenExpired` shows an error instead of re-prompting (loop guard); `Failed`/`NotConfigured` dialogs appear only when a sync is waiting, since Setup has inline status.
