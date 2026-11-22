using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.TestSupport;

/// <summary>
/// Awaits an editor action that hops to a pool thread (e.g. a file copy in <c>Task.Run</c>) and resumes on the
/// test's thread. Use it for every such await inside <see cref="DispatcherTestThread.Run"/>.
/// </summary>
/// <remarks>
/// Which synchronization context the continuation is posted to depends on what the test has touched: once an
/// Avalonia control exists the thread's context is usually <see cref="AvaloniaSynchronizationContext"/>, whose queue
/// only drains on <c>Dispatcher.UIThread.RunJobs()</c> (so block and pump - yielding would never come back);
/// otherwise it is the test thread's own pump (so yield - blocking would deadlock the resume). Either way any
/// dispatcher work the action queued is run, from the dispatcher's own thread only.
/// </remarks>
internal static class AsyncPump
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static async Task Pumped(Task task)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            var canRunJobs = Dispatcher.UIThread.CheckAccess();
            if (canRunJobs) Dispatcher.UIThread.RunJobs();

            if (canRunJobs && SynchronizationContext.Current is AvaloniaSynchronizationContext) Thread.Sleep(5);
            else await Task.Delay(5);
        }

        Assert.IsTrue(task.IsCompleted, "the editor action did not finish within " + Timeout.TotalSeconds + " s");
        await task;
    }

    public static async Task<T> Pumped<T>(Task<T> task)
    {
        await Pumped((Task)task);
        return await task;
    }
}
