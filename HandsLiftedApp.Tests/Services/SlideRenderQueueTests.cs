using System.Collections.Concurrent;
using HandsLiftedApp.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class SlideRenderQueueTests
{
    [TestMethod]
    public async Task EnqueueAsync_ExecutesWork()
    {
        var queue = new SlideRenderQueue(maxConcurrency: 1);
        bool ran = false;

        await queue.EnqueueAsync(() => ran = true);

        Assert.IsTrue(ran);
    }

    [TestMethod]
    public async Task EnqueueBatchAsync_RunsAllWorkItems()
    {
        var queue = new SlideRenderQueue(maxConcurrency: 2);
        var executed = new ConcurrentBag<int>();

        await queue.EnqueueBatchAsync(
            Enumerable.Range(0, 5).Select<int, Action>(i => () => executed.Add(i)));

        Assert.AreEqual(5, executed.Count);
    }

    [TestMethod]
    public async Task EnqueueBatchAsync_RespectsConcurrencyLimit()
    {
        const int maxConcurrency = 2;
        var queue = new SlideRenderQueue(maxConcurrency: maxConcurrency);
        int maxObserved = 0;
        int current = 0;
        var sync = new object();

        await queue.EnqueueBatchAsync(
            Enumerable.Range(0, 10).Select<int, Action>(_ => () =>
            {
                lock (sync)
                {
                    current++;
                    if (current > maxObserved) maxObserved = current;
                }
                Thread.Sleep(20);
                lock (sync) { current--; }
            }));

        Assert.IsTrue(maxObserved <= maxConcurrency,
            $"Expected max {maxConcurrency}, observed {maxObserved}");
    }

    [TestMethod]
    public async Task EnqueueAsync_DefaultConcurrency_IsAtLeastTwo()
    {
        var queue = new SlideRenderQueue();
        bool ran = false;
        await queue.EnqueueAsync(() => ran = true);
        Assert.IsTrue(ran);
    }
}
