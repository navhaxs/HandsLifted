using HandsLiftedApp.Core.Views;

namespace HandsLiftedApp.Tests.Views;

[TestClass]
public class SlideGridMetricsNotchTests
{
    [TestMethod]
    public void ColumnNotches_FeedingEachNotchToPanelFormulaGivesExactColumns()
    {
        const double W = 900, itemWidth = 290;
        var notches = SlideGridMetrics.ColumnNotches(W, itemWidth);
        Assert.IsTrue(notches.Length > 0);
        // Ascending notches map to columns maxCount..1 descending.
        for (int i = 0; i < notches.Length; i++)
        {
            int expected = notches.Length - i;
            int cols = (int)((W + SlideGridMetrics.Spacing) / (itemWidth * notches[i] / 100 + SlideGridMetrics.Spacing));
            Assert.AreEqual(expected, cols, $"notch {i} = {notches[i]}");
        }
    }

    [TestMethod]
    public void ColumnNotches_AreAscendingAndDistinct()
    {
        var notches = SlideGridMetrics.ColumnNotches(1200, 290);
        for (int i = 1; i < notches.Length; i++)
            Assert.IsTrue(notches[i] > notches[i - 1]);
    }

    [TestMethod]
    public void ColumnNotches_NarrowWidth_StillHasOneNotch()
    {
        // W=100 still yields 3 notches above the 10 clamp (3 columns at ~10.1); use truly narrow widths.
        var notches = SlideGridMetrics.ColumnNotches(40, 290);
        Assert.AreEqual(1, notches.Length);
        Assert.IsTrue(notches[0] >= 10);

        var tiny = SlideGridMetrics.ColumnNotches(5, 290);
        Assert.AreEqual(1, tiny.Length);
        Assert.IsTrue(tiny[0] >= 10);
    }

    [TestMethod]
    public void ColumnNotches_UsesMaxColumns()
    {
        var notches = SlideGridMetrics.ColumnNotches(3000, 290, 8);
        Assert.IsTrue(notches.Length <= 8);
    }

    [TestMethod]
    public void ColumnNotches_UltrawideReachesDefaultMaxColumns()
    {
        var notches = SlideGridMetrics.ColumnNotches(3400, 290);
        Assert.AreEqual(SlideGridMetrics.DefaultMaxColumns, notches.Length);
    }

    [TestMethod]
    public void NextNotch_StepsPastValueBetweenNotches()
    {
        var n = new[] { 50.0, 75, 100 };
        Assert.AreEqual(100, SlideGridMetrics.NextNotch(n, 80, 1));
        Assert.AreEqual(75, SlideGridMetrics.NextNotch(n, 80, -1));
    }

    [TestMethod]
    public void NextNotch_AtEndsStays()
    {
        var n = new[] { 50.0, 75, 100 };
        Assert.AreEqual(100, SlideGridMetrics.NextNotch(n, 100, 1));
        Assert.AreEqual(50, SlideGridMetrics.NextNotch(n, 50, -1));
    }

    [TestMethod]
    public void NextNotch_EmptyReturnsValue()
    {
        Assert.AreEqual(42, SlideGridMetrics.NextNotch(System.Array.Empty<double>(), 42, 1));
    }

    [TestMethod]
    public void NearestNotch_PicksClosest()
    {
        var n = new[] { 50.0, 75, 100 };
        Assert.AreEqual(75, SlideGridMetrics.NearestNotch(n, 70));
        Assert.AreEqual(100, SlideGridMetrics.NearestNotch(n, 90));
    }
}
