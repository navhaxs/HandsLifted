using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HandsLiftedApp.Controls;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Controls;

[TestClass]
public class AutoFitTextBlockTests
{
    // a 1280x720 slide box with the same 48,36 padding as the text-mode item template
    private const double BoxW = 1280, BoxH = 720, PadX = 48, PadY = 36;
    private const double InnerW = BoxW - 2 * PadX, InnerH = BoxH - 2 * PadY;

    private const string Stanza =
        "Amazing grace how sweet the sound\nThat saved a wretch like me\nI once was lost but now am found\nWas blind but now I see";

    private const string LongStanza = Stanza + "\n" + Stanza + "\n" + Stanza;

    private static AutoFitTextBlock Make(string text, double maxFontSize, bool autoFit) => new()
    {
        Text = text,
        MaxFontSize = maxFontSize,
        AutoFit = autoFit,
        Foreground = Brushes.White,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Border InBox(AutoFitTextBlock block, string imageName)
    {
        AvaloniaTextRendering.EnsureInitialized();
        var box = new Border { Padding = new Thickness(PadX, PadY), Child = block };
        var root = AvaloniaTextRendering.Layout(box, BoxW, BoxH);
        AvaloniaTextRendering.MaybeSavePng(root, imageName);
        return root;
    }

    [TestMethod]
    public Task TextThatFits_KeepsMaxFontSize_AndIsNotTrimmed() => DispatcherTestThread.Run(() =>
    {
        var block = Make(Stanza, 56, autoFit: true);
        InBox(block, "autofit_fits_56");

        Assert.AreEqual(56, block.EffectiveFontSize);
        Assert.IsFalse(block.IsTrimmed);
        Assert.IsTrue(block.DesiredSize.Height <= InnerH);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AutoFit_ShrinksOnly_NeverGrows() => DispatcherTestThread.Run(() =>
    {
        foreach (var max in new double[] { 20, 56, 100, 140 })
        {
            var block = Make(Stanza, max, autoFit: true);
            InBox(block, $"autofit_shrink_{max}");
            Assert.IsTrue(block.EffectiveFontSize <= max, $"max {max}: grew to {block.EffectiveFontSize}");
        }
        return Task.CompletedTask;
    });

    // The reported bug: scaling a block that was wrapped for the unscaled width leaves a blank gap on the right.
    // Re-wrapping at the reduced size must use most of the available width once the text has shrunk.
    [TestMethod]
    public Task AutoFit_WhenShrunk_FillsTheAvailableWidth_NoBlankGapOnTheRight() => DispatcherTestThread.Run(() =>
    {
        foreach (var max in new double[] { 100, 140 })
        {
            var block = Make(LongStanza, max, autoFit: true);
            InBox(block, $"autofit_width_{max}");

            Assert.IsTrue(block.EffectiveFontSize < max, $"max {max}: expected the text to need shrinking");
            Assert.IsFalse(block.IsTrimmed, $"max {max}: should fit by shrinking, not by trimming");
            Assert.IsTrue(block.DesiredSize.Height <= InnerH, $"max {max}: height {block.DesiredSize.Height} > {InnerH}");
            // longest lyric line is ~33 chars; at the fitted size it should span well over half the width
            Assert.IsTrue(block.DesiredSize.Width > InnerW * 0.55,
                $"max {max}: text only {block.DesiredSize.Width:0}px wide of {InnerW}px (size {block.EffectiveFontSize:0.#})");
        }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AutoFit_ChosenSize_IsAboutTheLargestThatFits() => DispatcherTestThread.Run(() =>
    {
        var block = Make(LongStanza, 140, autoFit: true);
        InBox(block, "autofit_largest");

        // a slightly larger font size would overflow
        var bigger = Make(LongStanza, block.EffectiveFontSize + 2, autoFit: false);
        InBox(bigger, "autofit_largest_plus");
        Assert.IsTrue(bigger.IsTrimmed, "shrunk further than needed");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AutoFitOff_OverflowingText_IsCutWithEllipsis_AtMaxFontSize() => DispatcherTestThread.Run(() =>
    {
        var block = Make(LongStanza, 100, autoFit: false);
        InBox(block, "autofit_off_ellipsis_100");

        Assert.AreEqual(100, block.EffectiveFontSize);
        Assert.IsTrue(block.IsTrimmed);
        Assert.IsTrue(block.DisplayedText.EndsWith("…"), $"no trailing ellipsis: '{block.DisplayedText}'");
        Assert.IsTrue(block.DisplayedText.Length < LongStanza.Length);
        Assert.IsTrue(LongStanza.StartsWith(block.DisplayedText.TrimEnd('…')), "displayed text isn't a prefix of the lyrics");
        Assert.IsTrue(block.DesiredSize.Height <= InnerH, $"height {block.DesiredSize.Height} > {InnerH}");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AutoFitOff_TextThatFits_IsNotTrimmed() => DispatcherTestThread.Run(() =>
    {
        var block = Make(Stanza, 56, autoFit: false);
        InBox(block, "autofit_off_fits_56");
        Assert.IsFalse(block.IsTrimmed);
        Assert.AreEqual(Stanza, block.DisplayedText);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AutoFit_TooMuchEvenAtMinFontSize_FallsBackToEllipsis() => DispatcherTestThread.Run(() =>
    {
        var huge = string.Join("\n", Enumerable.Repeat(Stanza, 30));
        var block = Make(huge, 100, autoFit: true);
        block.MinFontSize = 30;
        InBox(block, "autofit_min_ellipsis");

        Assert.AreEqual(30, block.EffectiveFontSize, 0.01);
        Assert.IsTrue(block.IsTrimmed);
        Assert.IsTrue(block.DisplayedText.EndsWith("…"));
        Assert.IsTrue(block.DesiredSize.Height <= InnerH);
        return Task.CompletedTask;
    });
}
