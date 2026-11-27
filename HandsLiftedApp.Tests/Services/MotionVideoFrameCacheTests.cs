using System.Collections.Generic;
using HandsLiftedApp.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class MotionVideoFrameCacheTests
{
    [TestInitialize]
    public void Setup() => MotionVideoFrameCache.Clear();

    [TestCleanup]
    public void Teardown() => MotionVideoFrameCache.Clear();

    private static SKBitmap Frame() => new SKBitmap(4, 3);

    [TestMethod]
    public void SameVideo_IsExtractedOnce()
    {
        var calls = 0;
        SKBitmap? Extract(string _) { calls++; return Frame(); }

        using var first = MotionVideoFrameCache.GetFrame("video-a.mp4", Extract);
        using var second = MotionVideoFrameCache.GetFrame("video-a.mp4", Extract);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreEqual(1, calls, "the second request for the same video must come from the cache");
    }

    [TestMethod]
    public void EachCaller_GetsItsOwnCopy_SoDisposingOneLeavesTheOthersUsable()
    {
        SKBitmap? Extract(string _) => Frame();

        var first = MotionVideoFrameCache.GetFrame("video-a.mp4", Extract)!;
        using var second = MotionVideoFrameCache.GetFrame("video-a.mp4", Extract)!;

        Assert.AreNotSame(first, second);
        first.Dispose(); // what a slide render does once it has drawn the frame

        Assert.AreEqual(4, second.Width);
        Assert.AreEqual(3, second.Height);
        using var third = MotionVideoFrameCache.GetFrame("video-a.mp4", Extract);
        Assert.IsNotNull(third, "the cached original must survive callers disposing their copies");
        Assert.AreEqual(4, third!.Width);
    }

    [TestMethod]
    public void FailedExtraction_IsNotRemembered()
    {
        var calls = 0;
        SKBitmap? Extract(string _) { calls++; return null; }

        Assert.IsNull(MotionVideoFrameCache.GetFrame("unreachable.mp4", Extract));
        Assert.IsNull(MotionVideoFrameCache.GetFrame("unreachable.mp4", Extract));

        Assert.AreEqual(2, calls, "a video that could not be read (drive not mounted yet) must be retried");
    }

    [TestMethod]
    public void DifferentVideos_AreCachedSeparately()
    {
        var extracted = new List<string>();
        SKBitmap? Extract(string path) { extracted.Add(path); return Frame(); }

        using var a = MotionVideoFrameCache.GetFrame("a.mp4", Extract);
        using var b = MotionVideoFrameCache.GetFrame("b.mp4", Extract);
        using var a2 = MotionVideoFrameCache.GetFrame("a.mp4", Extract);

        CollectionAssert.AreEqual(new[] { "a.mp4", "b.mp4" }, extracted);
    }

    [TestMethod]
    public void OldestEntries_AreEvicted_SoMemoryStaysBounded()
    {
        var calls = 0;
        SKBitmap? Extract(string _) { calls++; return Frame(); }

        for (var i = 0; i < 9; i++)
            MotionVideoFrameCache.GetFrame($"video-{i}.mp4", Extract)?.Dispose();
        Assert.AreEqual(9, calls);

        MotionVideoFrameCache.GetFrame("video-0.mp4", Extract)?.Dispose(); // evicted: extracted again
        Assert.AreEqual(10, calls);
        MotionVideoFrameCache.GetFrame("video-8.mp4", Extract)?.Dispose(); // most recent: still cached
        Assert.AreEqual(10, calls);
    }
}
