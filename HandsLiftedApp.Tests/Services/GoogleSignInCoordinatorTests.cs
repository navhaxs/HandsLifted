using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using HandsLiftedApp.Core.Services.GoogleSignIn;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class GoogleSignInCoordinatorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class FakeWaiter : IGoogleSyncWaiter
    {
        public int Resumed;
        public int Abandoned;
        public bool ThrowOnResume;

        public void ResumeAfterSignIn()
        {
            Interlocked.Increment(ref Resumed);
            if (ThrowOnResume) throw new InvalidOperationException("waiter blew up");
        }

        public void AbandonSignIn() => Interlocked.Increment(ref Abandoned);
    }

    private sealed class FakeRunner : IGoogleSignInRunner
    {
        private readonly object _gate = new();
        private TaskCompletionSource<bool> _started = NewTcs();
        private TaskCompletionSource<bool> _complete = NewTcs();
        public int Calls;
        public Exception? ThrowOnRun;

        private static TaskCompletionSource<bool> NewTcs() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes once the current runner call has started and is pending.</summary>
        public Task Started { get { lock (_gate) return _started.Task; } }

        public void Complete()
        {
            lock (_gate) _complete.TrySetResult(true);
        }

        public async Task SignInAsync(string clientId, string clientSecret, Action<string> onAuthUrl,
            CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> complete;
            lock (_gate)
            {
                Interlocked.Increment(ref Calls);
                complete = _complete;
                _started.TrySetResult(true);
            }

            onAuthUrl("https://accounts.google.com/fake");
            using var reg = cancellationToken.Register(() => complete.TrySetCanceled(cancellationToken));
            await complete.Task;

            // Fails only once the test calls Complete(), so every waiter has joined before the failure.
            if (ThrowOnRun != null) throw ThrowOnRun;
        }

        /// <summary>Resets the pending/started signals so a second sign-in can be sequenced.</summary>
        public void Reset()
        {
            lock (_gate)
            {
                _started = NewTcs();
                _complete = NewTcs();
            }
        }
    }

    private sealed class FakeSession : IGoogleSignInSession
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly TaskCompletionSource<bool> _confirm =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken UserCancelled => _cts.Token;
        public int WaitingShown;
        public string? AuthUrl;
        public bool Disposed;
        public int ConfirmCalls;
        public int ActivateCalls;

        public Task<bool> WaitForConfirmAsync() => _confirm.Task;
        public void ShowWaiting() => Interlocked.Increment(ref WaitingShown);
        public void SetAuthUrl(string url) => AuthUrl = url;

        /// <summary>Test-side confirm/decline, as if the user clicked a button.</summary>
        public void Confirm(bool signIn) => _confirm.TrySetResult(signIn);

        /// <summary>Coordinator-side confirm (skip the explanation).</summary>
        public void Confirm()
        {
            Interlocked.Increment(ref ConfirmCalls);
            _confirm.TrySetResult(true);
        }

        public void Activate() => Interlocked.Increment(ref ActivateCalls);

        public void Cancel()
        {
            _cts.Cancel();
            _confirm.TrySetResult(false);
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeUi : IGoogleSignInUi
    {
        public readonly List<FakeSession> Sessions = new();
        public readonly List<(string Title, string Message)> Errors = new();
        private readonly object _gate = new();
        private readonly TaskCompletionSource<bool> _opened =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes when the first session has been opened.</summary>
        public Task Opened => _opened.Task;

        /// <summary>When set, Open blocks (on its caller thread) until the gate is released.</summary>
        public ManualResetEventSlim? OpenGate;
        private readonly TaskCompletionSource<bool> _openEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes when Open has been entered (possibly still blocked on the gate).</summary>
        public Task OpenEntered => _openEntered.Task;

        public int OpenCalls { get { lock (_gate) return Sessions.Count; } }
        public int ErrorCalls { get { lock (_gate) return Errors.Count; } }
        public FakeSession LastSession { get { lock (_gate) return Sessions[^1]; } }

        public IGoogleSignInSession Open(Window? owner, bool startWithExplanation)
        {
            _openEntered.TrySetResult(true);
            OpenGate?.Wait(Timeout);
            var session = new FakeSession();
            if (!startWithExplanation) session.Confirm(true);
            lock (_gate) Sessions.Add(session);
            _opened.TrySetResult(true);
            return session;
        }

        public void ShowError(Window? owner, string title, string message)
        {
            lock (_gate) Errors.Add((title, message));
        }
    }

    private static GoogleSignInCoordinator Create(FakeRunner runner, FakeUi ui,
        string? id = "id", string? secret = "secret") =>
        new(runner, ui, () => (id, secret));

    [TestMethod]
    public async Task OneSignInServesAllWaiters_AndResumesEachOnSuccess()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);
        var waiters = new[] { new FakeWaiter(), new FakeWaiter(), new FakeWaiter() };

        var first = coordinator.RequestSignIn(null, false, waiters[0]);
        var second = coordinator.RequestSignIn(null, false, waiters[1]);
        var third = coordinator.RequestSignIn(null, false, waiters[2]);
        await runner.Started.WaitAsync(Timeout);

        Assert.AreEqual(1, ui.OpenCalls);
        Assert.AreEqual(1, runner.Calls);

        runner.Complete();
        var results = await Task.WhenAll(first, second, third).WaitAsync(Timeout);

        foreach (var r in results) Assert.AreEqual(GoogleSignInOutcome.Success, r.Outcome);
        foreach (var w in waiters)
        {
            Assert.AreEqual(1, w.Resumed);
            Assert.AreEqual(0, w.Abandoned);
        }
        Assert.AreEqual(1, ui.OpenCalls);
        Assert.AreEqual(1, runner.Calls);
        Assert.IsTrue(ui.LastSession.Disposed, "modal should be closed when the sign-in finishes");
    }

    [TestMethod]
    public async Task DecliningTheExplanation_NeverRunsTheRunner()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);
        var waiter = new FakeWaiter();

        var task = coordinator.RequestSignIn(null, true, waiter);
        await ui.Opened.WaitAsync(Timeout);
        ui.LastSession.Confirm(false);
        var result = await task.WaitAsync(Timeout);

        Assert.AreEqual(GoogleSignInOutcome.Cancelled, result.Outcome);
        Assert.AreEqual(0, runner.Calls);
        Assert.AreEqual(1, waiter.Abandoned);
        Assert.AreEqual(0, waiter.Resumed);
        Assert.AreEqual(0, ui.ErrorCalls);
    }

    [TestMethod]
    public async Task Cancel_AbandonsEveryWaiter_WithoutErrorDialog()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);
        var waiters = new[] { new FakeWaiter(), new FakeWaiter() };

        var first = coordinator.RequestSignIn(null, false, waiters[0]);
        var second = coordinator.RequestSignIn(null, false, waiters[1]);
        await runner.Started.WaitAsync(Timeout);

        ui.LastSession.Cancel();
        var results = await Task.WhenAll(first, second).WaitAsync(Timeout);

        foreach (var r in results) Assert.AreEqual(GoogleSignInOutcome.Cancelled, r.Outcome);
        foreach (var w in waiters)
        {
            Assert.AreEqual(1, w.Abandoned);
            Assert.AreEqual(0, w.Resumed);
        }
        Assert.AreEqual(0, ui.ErrorCalls);
    }

    [TestMethod]
    public async Task RunnerFailure_ShowsOneErrorDialog_AbandonsWaiters_AndAllowsNextRequest()
    {
        var runner = new FakeRunner { ThrowOnRun = new InvalidOperationException("boom") };
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);
        var waiters = new[] { new FakeWaiter(), new FakeWaiter() };

        var first = coordinator.RequestSignIn(null, false, waiters[0]);
        var second = coordinator.RequestSignIn(null, false, waiters[1]);
        await runner.Started.WaitAsync(Timeout);
        runner.Complete();
        var results = await Task.WhenAll(first, second).WaitAsync(Timeout);

        foreach (var r in results)
        {
            Assert.AreEqual(GoogleSignInOutcome.Failed, r.Outcome);
            Assert.AreEqual("boom", r.ErrorMessage);
        }
        Assert.AreEqual(1, ui.ErrorCalls);
        Assert.AreEqual("Google Sign-In Failed", ui.Errors[0].Title);
        StringAssert.Contains(ui.Errors[0].Message, "boom");
        foreach (var w in waiters)
        {
            Assert.AreEqual(1, w.Abandoned);
            Assert.AreEqual(0, w.Resumed);
        }

        var next = await coordinator.RequestSignIn(null, false, new FakeWaiter()).WaitAsync(Timeout);
        Assert.AreEqual(GoogleSignInOutcome.Failed, next.Outcome);
        Assert.AreEqual(2, ui.OpenCalls);
    }

    [TestMethod]
    public async Task CancellationThatTheUserDidNotRequest_IsAFailure_NotACancel()
    {
        // e.g. an HTTP timeout surfacing as TaskCanceledException while the modal is still open
        var runner = new FakeRunner { ThrowOnRun = new TaskCanceledException("The request timed out") };
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);
        var waiter = new FakeWaiter();

        var task = coordinator.RequestSignIn(null, false, waiter);
        await runner.Started.WaitAsync(Timeout);
        runner.Complete();
        var result = await task.WaitAsync(Timeout);

        Assert.AreEqual(GoogleSignInOutcome.Failed, result.Outcome);
        Assert.AreEqual(1, waiter.Abandoned);
        Assert.AreEqual(0, waiter.Resumed);
        Assert.AreEqual(1, ui.ErrorCalls);
        Assert.AreEqual("Google Sign-In Failed", ui.Errors[0].Title);
    }

    [TestMethod]
    public async Task MissingCredentials_ShowsNotConfiguredOnce_AndAbandonsWaiters()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi();
        var coordinator = Create(runner, ui, id: " ", secret: null);
        var waiter = new FakeWaiter();

        var result = await coordinator.RequestSignIn(null, false, waiter).WaitAsync(Timeout);

        Assert.AreEqual(GoogleSignInOutcome.NotConfigured, result.Outcome);
        Assert.AreEqual(1, ui.ErrorCalls);
        Assert.AreEqual("Google Sign-In Not Configured", ui.Errors[0].Title);
        Assert.AreEqual(0, runner.Calls);
        Assert.AreEqual(1, waiter.Abandoned);
        Assert.AreEqual(0, waiter.Resumed);
    }

    [TestMethod]
    public async Task RequestWhileRunning_JoinsTheSameSession()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);
        var waiter = new FakeWaiter();

        var first = coordinator.RequestSignIn(null, false, waiter);
        await runner.Started.WaitAsync(Timeout);
        var setup = coordinator.RequestSignIn(null, true, null);

        Assert.AreSame(first, setup, "a request while running must return the running sign-in's task");
        Assert.AreEqual(1, ui.OpenCalls);

        runner.Complete();
        var firstResult = await first.WaitAsync(Timeout);
        var setupResult = await setup.WaitAsync(Timeout);

        Assert.AreEqual(GoogleSignInOutcome.Success, firstResult.Outcome);
        Assert.AreEqual(GoogleSignInOutcome.Success, setupResult.Outcome);
        Assert.AreEqual(1, ui.OpenCalls);
        Assert.AreEqual(1, runner.Calls);
        Assert.AreEqual(1, waiter.Resumed);
    }

    [TestMethod]
    public async Task NewRequestAfterCancel_StartsAFreshSignIn()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);

        var first = coordinator.RequestSignIn(null, false, new FakeWaiter());
        await runner.Started.WaitAsync(Timeout);
        ui.LastSession.Cancel();
        var firstResult = await first.WaitAsync(Timeout);
        Assert.AreEqual(GoogleSignInOutcome.Cancelled, firstResult.Outcome);

        runner.Reset();
        var waiter = new FakeWaiter();
        var second = coordinator.RequestSignIn(null, false, waiter);
        await runner.Started.WaitAsync(Timeout);
        runner.Complete();
        var secondResult = await second.WaitAsync(Timeout);

        Assert.AreEqual(GoogleSignInOutcome.Success, secondResult.Outcome);
        Assert.AreEqual(2, ui.OpenCalls);
        Assert.AreEqual(1, waiter.Resumed);
    }

    [TestMethod]
    public async Task CancelEpoch_IncrementsOnUserCancel_AndOnDeclinedExplanation_ButNotOnSuccessOrFailure()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);
        Assert.AreEqual(0, coordinator.CancelEpoch);

        // user cancel while waiting for the browser
        var cancelled = coordinator.RequestSignIn(null, false, new FakeWaiter());
        await runner.Started.WaitAsync(Timeout);
        ui.LastSession.Cancel();
        await cancelled.WaitAsync(Timeout);
        Assert.AreEqual(1, coordinator.CancelEpoch);

        // declined on the explanation page
        var declined = coordinator.RequestSignIn(null, true, new FakeWaiter());
        await WaitForSessionCount(ui, 2);
        ui.LastSession.Confirm(false);
        var declinedResult = await declined.WaitAsync(Timeout);
        Assert.AreEqual(GoogleSignInOutcome.Cancelled, declinedResult.Outcome);
        Assert.AreEqual(2, coordinator.CancelEpoch);

        // success
        runner.Reset();
        var success = coordinator.RequestSignIn(null, false, new FakeWaiter());
        await runner.Started.WaitAsync(Timeout);
        runner.Complete();
        Assert.AreEqual(GoogleSignInOutcome.Success, (await success.WaitAsync(Timeout)).Outcome);
        Assert.AreEqual(2, coordinator.CancelEpoch);

        // runner failure
        runner.Reset();
        runner.ThrowOnRun = new InvalidOperationException("boom");
        var failed = coordinator.RequestSignIn(null, false, new FakeWaiter());
        await runner.Started.WaitAsync(Timeout);
        runner.Complete();
        Assert.AreEqual(GoogleSignInOutcome.Failed, (await failed.WaitAsync(Timeout)).Outcome);
        Assert.AreEqual(2, coordinator.CancelEpoch);
    }

    [TestMethod]
    public async Task SuccessGeneration_IncrementsOnlyOnSuccess()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);
        Assert.AreEqual(0, coordinator.SuccessGeneration);

        // cancel
        var cancelled = coordinator.RequestSignIn(null, false, new FakeWaiter());
        await runner.Started.WaitAsync(Timeout);
        ui.LastSession.Cancel();
        await cancelled.WaitAsync(Timeout);
        Assert.AreEqual(0, coordinator.SuccessGeneration);

        // success
        runner.Reset();
        var generationSeenByWaiter = -1;
        var waiter = new GenerationWaiter(coordinator, g => generationSeenByWaiter = g);
        var success = coordinator.RequestSignIn(null, false, waiter);
        await runner.Started.WaitAsync(Timeout);
        runner.Complete();
        await success.WaitAsync(Timeout);
        Assert.AreEqual(1, coordinator.SuccessGeneration);
        Assert.AreEqual(1, generationSeenByWaiter, "the generation must already be bumped when waiters are resumed");

        // failure
        runner.Reset();
        runner.ThrowOnRun = new InvalidOperationException("boom");
        var failed = coordinator.RequestSignIn(null, false, new FakeWaiter());
        await runner.Started.WaitAsync(Timeout);
        runner.Complete();
        await failed.WaitAsync(Timeout);
        Assert.AreEqual(1, coordinator.SuccessGeneration);

        // second success
        runner.Reset();
        runner.ThrowOnRun = null;
        var again = coordinator.RequestSignIn(null, false, new FakeWaiter());
        await runner.Started.WaitAsync(Timeout);
        runner.Complete();
        await again.WaitAsync(Timeout);
        Assert.AreEqual(2, coordinator.SuccessGeneration);
    }

    private sealed class GenerationWaiter : IGoogleSyncWaiter
    {
        private readonly GoogleSignInCoordinator _coordinator;
        private readonly Action<int> _onResume;

        public GenerationWaiter(GoogleSignInCoordinator coordinator, Action<int> onResume)
        {
            _coordinator = coordinator;
            _onResume = onResume;
        }

        public void ResumeAfterSignIn() => _onResume(_coordinator.SuccessGeneration);
        public void AbandonSignIn() { }
    }

    [TestMethod]
    public async Task JoinWithoutExplanation_WhileFirstRequestWaitsOnConfirm_SkipsTheExplanationAndRunsTheRunner()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);

        var first = coordinator.RequestSignIn(null, true, new FakeWaiter());
        await ui.Opened.WaitAsync(Timeout);
        Assert.AreEqual(0, runner.Calls, "still on the explanation page");

        var setup = coordinator.RequestSignIn(null, false, null);
        Assert.AreSame(first, setup);

        // The test never confirms: the join itself must skip the explanation.
        await runner.Started.WaitAsync(Timeout);
        Assert.AreEqual(1, ui.OpenCalls);
        Assert.IsTrue(ui.LastSession.ActivateCalls >= 1, "the modal must be brought to the front");
        Assert.IsTrue(ui.LastSession.ConfirmCalls >= 1);

        runner.Complete();
        Assert.AreEqual(GoogleSignInOutcome.Success, (await setup.WaitAsync(Timeout)).Outcome);
    }

    [TestMethod]
    public async Task JoinWithoutExplanation_BeforeOpenReturns_StillSkipsTheExplanation()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi { OpenGate = new ManualResetEventSlim(false) };
        var coordinator = Create(runner, ui);

        var first = coordinator.RequestSignIn(null, true, new FakeWaiter());
        await ui.OpenEntered.WaitAsync(Timeout);

        // Open is blocked, so the session does not exist yet when this join arrives.
        var setup = coordinator.RequestSignIn(null, false, null);
        Assert.AreSame(first, setup);
        Assert.AreEqual(0, ui.OpenCalls);

        ui.OpenGate.Set();
        await runner.Started.WaitAsync(Timeout);
        Assert.AreEqual(1, ui.OpenCalls);
        Assert.IsTrue(ui.LastSession.ActivateCalls >= 1);
        Assert.IsTrue(ui.LastSession.ConfirmCalls >= 1);

        runner.Complete();
        Assert.AreEqual(GoogleSignInOutcome.Success, (await setup.WaitAsync(Timeout)).Outcome);
    }

    [TestMethod]
    public async Task JoinWithExplanation_DoesNotSkipTheExplanation()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);

        var first = coordinator.RequestSignIn(null, true, new FakeWaiter());
        await ui.Opened.WaitAsync(Timeout);
        var second = coordinator.RequestSignIn(null, true, new FakeWaiter());
        Assert.AreSame(first, second);

        await Task.Delay(200);
        Assert.AreEqual(0, runner.Calls);
        Assert.AreEqual(0, ui.LastSession.ConfirmCalls);

        ui.LastSession.Confirm(false);
        await first.WaitAsync(Timeout);
    }

    [TestMethod]
    public async Task ThrowingWaiter_DoesNotStopOtherWaiters()
    {
        var runner = new FakeRunner();
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);
        var thrower = new FakeWaiter { ThrowOnResume = true };
        var other = new FakeWaiter();

        var first = coordinator.RequestSignIn(null, false, thrower);
        var second = coordinator.RequestSignIn(null, false, other);
        await runner.Started.WaitAsync(Timeout);
        runner.Complete();
        await Task.WhenAll(first, second).WaitAsync(Timeout);

        Assert.AreEqual(1, thrower.Resumed);
        Assert.AreEqual(1, other.Resumed);
        Assert.AreEqual(0, other.Abandoned);
    }

    [TestMethod]
    public async Task FailureWithoutWaiters_ShowsNoDialog()
    {
        var runner = new FakeRunner { ThrowOnRun = new InvalidOperationException("boom") };
        var ui = new FakeUi();
        var coordinator = Create(runner, ui);

        var failed = coordinator.RequestSignIn(null, false, null);
        await runner.Started.WaitAsync(Timeout);
        runner.Complete();
        var result = await failed.WaitAsync(Timeout);
        Assert.AreEqual(GoogleSignInOutcome.Failed, result.Outcome);
        Assert.AreEqual(0, ui.ErrorCalls);

        var notConfigured = Create(new FakeRunner(), ui, id: null, secret: null);
        var ncResult = await notConfigured.RequestSignIn(null, false, null).WaitAsync(Timeout);
        Assert.AreEqual(GoogleSignInOutcome.NotConfigured, ncResult.Outcome);
        Assert.AreEqual(0, ui.ErrorCalls);
    }

    private static async Task WaitForSessionCount(FakeUi ui, int count)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (ui.OpenCalls < count)
        {
            if (DateTime.UtcNow > deadline) Assert.Fail($"session {count} was never opened");
            await Task.Delay(5);
        }
    }
}
