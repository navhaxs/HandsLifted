using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace HandsLiftedApp.Core.Services;

/// <summary>
/// Implemented by slide types whose thumbnail bitmaps are generated off-thread.
/// </summary>
public interface IRenderable
{
    void Render();
}

/// <summary>
/// Renders slides on threadpool threads, bounded by processor count / 2.
/// Keeps headroom for the UI thread and prevents all cores saturating on thumbnail generation.
/// </summary>
public sealed class SlideRenderQueue
{
    private readonly SemaphoreSlim _gate;

    public SlideRenderQueue(int maxConcurrency = 0)
    {
        var concurrency = maxConcurrency > 0
            ? maxConcurrency
            : Math.Max(2, Environment.ProcessorCount / 2);
        _gate = new SemaphoreSlim(concurrency, concurrency);
    }

    /// <summary>
    /// Enqueues a single unit of render work. Returns a Task for testing; callers
    /// in production should fire-and-forget with <c>_ =</c>.
    /// </summary>
    public Task EnqueueAsync(Action work) => RunWithGate(work);

    /// <summary>
    /// Enqueues a batch of render work items and waits until all complete.
    /// Returns a Task for testing; callers in production should fire-and-forget.
    /// </summary>
    public Task EnqueueBatchAsync(IEnumerable<Action> workItems)
        => Task.WhenAll(workItems.Select(RunWithGate).ToList());

    /// <summary>Fire-and-forget convenience overload for production use.</summary>
    public void Enqueue(IRenderable renderable)
        => _ = EnqueueAsync(renderable.Render);

    /// <summary>Fire-and-forget batch convenience overload for production use.</summary>
    public void EnqueueBatch(IEnumerable<IRenderable> renderables)
        => _ = EnqueueBatchAsync(renderables.Select<IRenderable, Action>(r => r.Render));

    // Busy-period bookkeeping, only used to log how long a burst of renders took (see LogDrainedIfNotable).
    private readonly object _statsLock = new();
    private int _pending;
    private int _burstCount;
    private long _burstStartTimestamp;

    private async Task RunWithGate(Action work)
    {
        lock (_statsLock)
        {
            if (_pending++ == 0)
            {
                _burstCount = 0;
                _burstStartTimestamp = Stopwatch.GetTimestamp();
            }
            _burstCount++;
        }

        try
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try { await Task.Run(work).ConfigureAwait(false); }
            finally { _gate.Release(); }
        }
        finally
        {
            LogDrainedIfNotable();
        }
    }

    /// <summary>
    /// Logs when the queue goes idle after a burst of work, so "how long until every slide was rendered"
    /// after a playlist load can be read straight off the log. Small bursts (a single edited slide) are skipped.
    /// </summary>
    private void LogDrainedIfNotable()
    {
        int count;
        TimeSpan elapsed;
        lock (_statsLock)
        {
            if (--_pending != 0) return;
            count = _burstCount;
            elapsed = Stopwatch.GetElapsedTime(_burstStartTimestamp);
        }

        if (count >= 5 || elapsed.TotalMilliseconds >= 500)
            Log.Debug("Render queue drained: {RenderCount} renders in {ElapsedMs} ms", count, (long)elapsed.TotalMilliseconds);
    }
}
