using System.Linq;
using System.Threading.Tasks;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Render;

[TestClass]
public class CustomSlideTextLayoutTests
{
    private static TextElement Make(string text, TextAlignment align = TextAlignment.Left,
        VerticalAlignment v = VerticalAlignment.Top, int width = 800) => new()
    {
        X = 100, Y = 50, Width = width, Height = 400, Text = text, FontSize = 40,
        TextAlignment = align, VerticalAlignment = v,
    };

    private static System.Collections.Generic.IReadOnlyList<LayoutLine> Run(TextElement el)
    {
        using var font = CustomSlideTextLayout.CreateFont(el);
        var lines = CustomSlideTextLayout.Layout(el, font);
        font.Typeface?.Dispose();
        return lines;
    }

    [TestMethod]
    public void LeftCentreRight_PlaceLineXAsExpected()
    {
        var left = Run(Make("Hello", TextAlignment.Left)).Single();
        var centre = Run(Make("Hello", TextAlignment.Center)).Single();
        var right = Run(Make("Hello", TextAlignment.Right)).Single();
        Assert.AreEqual(100f, left.X, 0.01f);
        Assert.AreEqual(100 + (800 - centre.Width) / 2f, centre.X, 0.01f);
        Assert.AreEqual(100 + 800 - right.Width, right.X, 0.01f);
    }

    [TestMethod]
    public void TopCentreBottom_PlaceFirstBaselineAsExpected()
    {
        var top = Run(Make("Hello", v: VerticalAlignment.Top)).First().Baseline;
        var centre = Run(Make("Hello", v: VerticalAlignment.Center)).First().Baseline;
        var bottom = Run(Make("Hello", v: VerticalAlignment.Bottom)).First().Baseline;
        Assert.IsTrue(top < centre && centre < bottom);
        Assert.AreEqual((top + bottom) / 2f, centre, 1f);
    }

    [TestMethod]
    public void LongWord_OnOwnLine_DoesNotLoop()
    {
        var lines = Run(Make(new string('W', 200), width: 50));
        Assert.AreEqual(1, lines.Count);
    }

    [TestMethod]
    public void ExplicitNewlines_ProduceLines()
    {
        var lines = Run(Make("one\ntwo\nthree"));
        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual("two", lines[1].Text);
    }

    [TestMethod]
    public void EmptyText_ReturnsNoLines()
    {
        Assert.AreEqual(0, Run(Make("")).Count);
    }

    [TestMethod]
    public Task LineHeightEm_ChangesBaselineSpacing() => DispatcherTestThread.Run(() =>
    {
        var a = Make("one\ntwo");
        var b = Make("one\ntwo");
        b.LineHeightEm = 2.0M;
        Dispatcher.UIThread.RunJobs();
        var la = Run(a);
        var lb = Run(b);
        var da = la[1].Baseline - la[0].Baseline;
        var db = lb[1].Baseline - lb[0].Baseline;
        Assert.AreEqual(40f, da, 0.01f);
        Assert.AreEqual(80f, db, 0.01f);
        return Task.CompletedTask;
    });
}
