using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace HandsLiftedApp.Tests.TestSupport;

// Fixes a thread-affinity flake: Dispatcher.UIThread binds permanently to whichever thread first
// *accesses* it (lazy construction on first touch - Avalonia platform init alone does not trigger
// this). Production code that reaches Dispatcher.UIThread.Post/InvokeAsync from a continuation that
// resumed on an arbitrary ThreadPool thread (e.g. after a ConfigureAwait(false) disk read, or a plain
// Task.Run) can - if it's the first thing in the whole test run to touch Dispatcher.UIThread -
// permanently bind the dispatcher to that ThreadPool thread instead of any thread the tests control.
// Later, when a test's own code calls Dispatcher.UIThread.RunJobs() (or awaits something that ends up
// calling Dispatcher.VerifyAccess()), it throws "different thread owns it" unless it happens to be
// running on that same ThreadPool thread - a coin flip that depends on ThreadPool scheduling, hence
// non-determinism (worse under full-suite contention, but possible even in a single sequential run).
//
// The fix has two parts, both required:
// (1) EnsureStarted() forces the first-ever touch of Dispatcher.UIThread to happen on one dedicated
//     background thread, before any test or production code gets a chance to touch it first from
//     elsewhere - so Dispatcher.UIThread is permanently bound to a thread the tests actually control.
//     This must run from a single, assembly-wide [AssemblyInitialize] (MSTest only allows one per
//     assembly, and guarantees it runs before any test in the assembly regardless of which test class
//     declares it) - see ScriptureItemInstanceTests.AssemblyInit, the sole declaration for this assembly.
// (2) Run() then runs a Dispatcher-touching test body on that same thread, via a SynchronizationContext
//     installed on it, so any `await` without ConfigureAwait(false) posts its continuation back onto
//     that thread instead of resuming on whatever thread completed the antecedent task - meaning a
//     later `Dispatcher.UIThread.RunJobs()` always ends up called from the one thread Dispatcher.UIThread
//     is bound to. The dedicated thread runs a simple perpetual pump loop, so posted continuations are
//     always eventually run - unlike Dispatcher.UIThread's own queue, which only drains when a test
//     explicitly calls RunJobs().
internal static class DispatcherTestThread
{
    private sealed class QueueSynchronizationContext(BlockingCollection<(SendOrPostCallback Callback, object? State)> queue) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) => d(state);
    }

    private static readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> Queue = new();
    private static readonly object InitLock = new();
    private static bool _started;

    public static void EnsureStarted()
    {
        lock (InitLock)
        {
            if (_started) return;
            _started = true;

            using var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new QueueSynchronizationContext(Queue));
                Avalonia.Skia.SkiaPlatform.Initialize();
                ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithPlatformServices().BuildApp();

                // Dispatcher.UIThread is lazily constructed on whichever thread first accesses it -
                // SkiaPlatform.Initialize() only registers render services, it does not itself touch
                // the dispatcher. Force that first touch here, on this thread, before returning control
                // to AssemblyInit - otherwise production code's own Dispatcher.UIThread.Post(...) call
                // (reached via a ConfigureAwait(false) continuation, so on an arbitrary ThreadPool
                // thread) would win the race and bind the dispatcher to the wrong thread instead.
                Dispatcher.UIThread.VerifyAccess();
                ready.Set();

                foreach (var (callback, state) in Queue.GetConsumingEnumerable())
                {
                    callback(state);
                }
            })
            {
                IsBackground = true,
                Name = "HandsLiftedApp.Tests.DispatcherTestThread"
            };
            thread.Start();
            ready.Wait();
        }
    }

    public static Task Run(Func<Task> body)
    {
        EnsureStarted();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue.Add((async _ =>
        {
            try
            {
                await body();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null));
        return tcs.Task;
    }
}
