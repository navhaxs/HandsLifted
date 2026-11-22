using System.Collections.Generic;
using Avalonia;
using HandsLiftedApp.Core.Views.Editors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Views;

[TestClass]
public class SnapCalculatorTests
{
    private static readonly IReadOnlyList<Rect> None = new List<Rect>();

    [TestMethod]
    public void SnapsCentreToSlideCentre_WithinThreshold()
    {
        // 200x100 rect whose centre is (965, 545): 5px off the slide centre on both axes
        var result = SnapCalculator.Snap(new Rect(865, 495, 200, 100), None, 1920, 1080);

        Assert.AreEqual(860, result.Rect.X, 0.001);
        Assert.AreEqual(490, result.Rect.Y, 0.001);
        Assert.AreEqual(200, result.Rect.Width, 0.001);
        Assert.AreEqual(100, result.Rect.Height, 0.001);
        Assert.AreEqual(960, result.GuideX);
        Assert.AreEqual(540, result.GuideY);
    }

    [TestMethod]
    public void SnapsLeftEdgeToSlideLeft()
    {
        var result = SnapCalculator.Snap(new Rect(7, 300, 200, 100), None, 1920, 1080);

        Assert.AreEqual(0, result.Rect.X, 0.001);
        Assert.AreEqual(300, result.Rect.Y, 0.001);
        Assert.AreEqual(0, result.GuideX);
        Assert.IsNull(result.GuideY);
    }

    [TestMethod]
    public void SnapsToOtherElementsRightEdge()
    {
        var other = new Rect(100, 100, 300, 200); // right edge 400
        var result = SnapCalculator.Snap(new Rect(406, 600, 200, 100), new[] { other }, 1920, 1080);

        Assert.AreEqual(400, result.Rect.X, 0.001);
        Assert.AreEqual(400, result.GuideX);
        Assert.IsNull(result.GuideY);
    }

    [TestMethod]
    public void NoSnap_OutsideThreshold_ReturnsOriginalRectAndNoGuides()
    {
        var dragged = new Rect(300, 200, 200, 100);
        var result = SnapCalculator.Snap(dragged, new[] { new Rect(700, 700, 100, 100) }, 1920, 1080);

        Assert.AreEqual(dragged, result.Rect);
        Assert.IsNull(result.GuideX);
        Assert.IsNull(result.GuideY);
    }

    [TestMethod]
    public void PicksNearestCandidate()
    {
        // left edge 303: other's left at 300 (3 away) beats other's left at 308 (5 away)
        var result = SnapCalculator.Snap(new Rect(303, 600, 200, 100),
            new[] { new Rect(308, 100, 50, 50), new Rect(300, 300, 50, 50) }, 1920, 1080);

        Assert.AreEqual(300, result.Rect.X, 0.001);
        Assert.AreEqual(300, result.GuideX);
    }

    [TestMethod]
    public void AxesSnapIndependently()
    {
        // x snaps to slide right (1920), y has nothing nearby
        var result = SnapCalculator.Snap(new Rect(1715, 200, 200, 100), None, 1920, 1080);

        Assert.AreEqual(1720, result.Rect.X, 0.001);
        Assert.AreEqual(200, result.Rect.Y, 0.001);
        Assert.AreEqual(1920, result.GuideX);
        Assert.IsNull(result.GuideY);
    }
}

[TestClass]
public class DragSnapSessionTests
{
    private static readonly IReadOnlyList<Rect> None = new List<Rect>();

    [TestMethod]
    public void SlowDragLeavesSnapPoint()
    {
        var session = new DragSnapSession(new Rect(0, 300, 200, 100)); // starts snapped to x=0
        SnapResult r = null!;
        for (var i = 0; i < 5; i++) r = session.Move(3, 0, None, 1920, 1080); // raw x = 15
        Assert.AreEqual(15, r.Rect.X, 0.001);
        Assert.IsNull(r.GuideX);
    }

    [TestMethod]
    public void WithinThresholdStillSnapsWithGuide()
    {
        var session = new DragSnapSession(new Rect(0, 300, 200, 100));
        var r = session.Move(3, 0, None, 1920, 1080);
        r = session.Move(3, 0, None, 1920, 1080); // raw x = 6
        Assert.AreEqual(0, r.Rect.X, 0.001);
        Assert.AreEqual(0, r.GuideX);
    }

    [TestMethod]
    public void SnappedRectNeverFeedsBack()
    {
        var session = new DragSnapSession(new Rect(0, 300, 200, 100));
        for (var i = 0; i < 3; i++) session.Move(4, 0, None, 1920, 1080); // snapped at 0 while raw reaches 12
        var r = session.Move(4, 0, None, 1920, 1080); // raw 16, not 4
        Assert.AreEqual(16, r.Rect.X, 0.001);
    }
}
