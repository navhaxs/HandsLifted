using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Controls;

namespace HandsLiftedApp.Tests.Controls;

[TestClass]
public class JustifiedWrapPanelTests
{
    private static JustifiedWrapPanel Make(int count, double aspect = 2, double extraW = 0, double extraH = 0)
    {
        var panel = new JustifiedWrapPanel
        {
            ItemWidth = 100,
            Spacing = 10,
            AspectRatio = aspect,
            ExtraWidth = extraW,
            ExtraHeight = extraH,
        };
        for (int i = 0; i < count; i++)
            panel.Children.Add(new Border());
        return panel;
    }

    private static void Layout(JustifiedWrapPanel panel, double width)
    {
        panel.Measure(new Size(width, 10000));
        panel.Arrange(new Rect(0, 0, width, 10000));
    }

    [TestMethod]
    public void AspectMode_StretchesWidthAndDerivesHeight()
    {
        var panel = Make(7);
        Layout(panel, 350);
        Assert.AreEqual(new Rect(0, 55 + 10, 110, 55), panel.Children[3].Bounds);
        Assert.AreEqual(110, panel.Children[0].Bounds.Width, 1e-9);
        Assert.AreEqual(55, panel.Children[0].Bounds.Height, 1e-9);
    }

    [TestMethod]
    public void AspectMode_HeightIncludesExtras()
    {
        var panel = Make(7, extraW: 10, extraH: 20);
        Layout(panel, 350);
        Assert.AreEqual(70, panel.Children[0].Bounds.Height, 1e-9);
    }

    [TestMethod]
    public void AspectMode_NarrowerThanItemWidth_UsesOneColumnAtItemWidth()
    {
        var panel = Make(3);
        Layout(panel, 60);
        Assert.AreEqual(100, panel.Children[0].Bounds.Width, 1e-9);
    }

    [TestMethod]
    public void AspectMode_SingleColumnFillsWidthWhenWiderThanItemWidth()
    {
        var panel = Make(3);
        Layout(panel, 150);
        Assert.AreEqual(0, panel.Children[1].Bounds.X, 1e-9);
        Assert.AreEqual(150, panel.Children[0].Bounds.Width, 1e-9);
        Assert.AreEqual(75, panel.Children[0].Bounds.Height, 1e-9);
    }

    [TestMethod]
    public void AspectMode_MeasureReturnsTotalDerivedHeight()
    {
        var panel = Make(7);
        panel.Measure(new Size(350, 10000));
        Assert.AreEqual(3 * 55 + 2 * 10, panel.DesiredSize.Height, 1e-9);
    }

    [TestMethod]
    public void AspectMode_InfiniteWidth_NoNaN()
    {
        var panel = Make(7);
        panel.Measure(new Size(double.PositiveInfinity, 10000));
        Assert.IsFalse(double.IsNaN(panel.DesiredSize.Width) || double.IsInfinity(panel.DesiredSize.Width));
        Assert.IsFalse(double.IsNaN(panel.DesiredSize.Height) || double.IsInfinity(panel.DesiredSize.Height));
    }

    [TestMethod]
    public void AspectMode_ExtraWidthLargerThanItem_HeightNotNegative()
    {
        var panel = Make(3, extraW: 500, extraH: 0);
        Layout(panel, 350);
        Assert.IsTrue(panel.Children[0].Bounds.Height >= 0);
        Assert.IsTrue(panel.DesiredSize.Height >= 0);
    }

    [TestMethod]
    public void LegacyMode_Unchanged()
    {
        var panel = Make(7, aspect: 0);
        panel.ItemHeight = 40;
        Layout(panel, 350);
        Assert.AreEqual(40, panel.Children[0].Bounds.Height, 1e-9);
        Assert.AreEqual(110, panel.Children[0].Bounds.Width, 1e-9);
    }
}
