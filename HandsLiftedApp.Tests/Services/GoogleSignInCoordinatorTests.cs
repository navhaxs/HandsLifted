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
        public void ResumeAfterSignIn() => Interlocked.Increment(ref Resumed);
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

        public Task<bool> WaitForConfirmAsync() => _confirm.Task;
        public void ShowWaiting() => Interlocked.Increment(ref WaitingShown);
        public void SetAuthUrl(string url) => AuthUrl = url;

        public void Confirm(bool signIn) => _confirm.TrySetResult(signIn);

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

        public int OpenCalls { get { lock (_gate) return Sessions.Count; } }
        public int ErrorCalls { get { lock (_gate) return Errors.Count; } }
        public FakeSession LastSession { get { lock (_gate) return Sessions[^1]; } }

        public IGoogleSignInSession Open(Window? owner, bool startWithExplanation)
        {
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

        Assert.AreEqual(1, ui.OpenCalls);

        runner.Complete();
        var firstResult = await first.WaitAsync(Timeout);
        var setupResult = await setup.WaitAsync(Timeout);

        Assert.AreEqual(GoogleSignInOutcome.Success, firstResult.Outcome);
        Assert.AreEqual(firstResult, setupResult);
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
}
