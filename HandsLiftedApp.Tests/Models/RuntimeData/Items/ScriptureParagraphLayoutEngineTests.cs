using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureParagraphLayoutEngineTests
{
    private static BaseSlideTheme MakeTheme(int fontSize = 60) => new BaseSlideTheme
    {
        FontSize = fontSize,
        TextColour = Colors.White,
        BackgroundColour = Colors.Black,
    };

    private static List<ScriptureVerseRef> MakeVerses(params (int chapter, int verse, string text)[] verses) =>
        verses.Select(v => new ScriptureVerseRef(v.chapter, v.verse, v.text)).ToList();

    [TestMethod]
    public void Paginate_ShortPassage_ProducesSinglePage()
    {
        var verses = MakeVerses((1, 1, "In the beginning God created the heaven and the earth."));

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Genesis 1:1", MakeTheme());

        Assert.AreEqual(1, pages.Count);
    }

    [TestMethod]
    public void Paginate_HeaderAppearsOnlyOnFirstPage()
    {
        var longVerseText = string.Join(" ", Enumerable.Repeat("word", 400));
        var verses = MakeVerses((1, 1, longVerseText));

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Genesis 1:1", MakeTheme(fontSize: 80));

        Assert.IsTrue(pages.Count > 1, "expected the long passage to reflow across multiple pages");
        Assert.IsTrue(pages[0].Lines.Any(l => l.IsHeader), "page 0 must contain the header");
        Assert.IsFalse(pages[1].Lines.Any(l => l.IsHeader), "continuation pages must not repeat the header");
    }

    // Scripture shrinks only within ScriptureMaxShrinkRatio, and only when that avoids an extra
    // page. The song-lyric autofit flag/floor (AutofitEnabled, AutofitMinFontSizeRatio) are ignored.
    // 150 "word"s at font 60 needs 2 pages; it fits on 1 page at roughly 48 (about 20% smaller).
    private static List<ScriptureVerseRef> OverflowingPassage() =>
        MakeVerses((1, 1, string.Join(" ", Enumerable.Repeat("word", 150))));

    [TestMethod]
    public void Paginate_DefaultMaxShrinkRatio_IsTenPercent()
    {
        Assert.AreEqual(0.1M, new BaseSlideTheme().ScriptureMaxShrinkRatio);
    }

    [TestMethod]
    public void Paginate_ZeroMaxShrinkRatio_NeverShrinks()
    {
        var theme = MakeTheme(fontSize: 60);
        theme.ScriptureMaxShrinkRatio = 0M;

        var pages = ScriptureParagraphLayoutEngine.Paginate(OverflowingPassage(), "Test 1:1", theme);

        Assert.IsTrue(pages.Count > 1);
        Assert.IsTrue(pages.All(p => p.FontSize == 60f));
    }

    [TestMethod]
    public void Paginate_ShrinkNeededExceedsTolerance_KeepsFullSizeAndSplits()
    {
        var theme = MakeTheme(fontSize: 60);
        theme.ScriptureMaxShrinkRatio = 0.05M; // floor 57, but it needs ~48 to fit one page

        var pages = ScriptureParagraphLayoutEngine.Paginate(OverflowingPassage(), "Test 1:1", theme);

        Assert.IsTrue(pages.Count > 1);
        Assert.IsTrue(pages.All(p => p.FontSize == 60f), "must not shrink at all when it cannot avoid the extra page");
    }

    [TestMethod]
    public void Paginate_ShrinkWithinTolerance_ShrinksJustEnoughToAvoidExtraPage()
    {
        var theme = MakeTheme(fontSize: 60);
        theme.ScriptureMaxShrinkRatio = 0.5M; // floor 30, plenty of room

        var pages = ScriptureParagraphLayoutEngine.Paginate(OverflowingPassage(), "Test 1:1", theme);

        Assert.AreEqual(1, pages.Count);
        Assert.IsTrue(pages[0].FontSize < 60f, "should have shrunk");
        Assert.IsTrue(pages[0].FontSize >= 30f, "must respect the tolerance floor");
    }

    [TestMethod]
    public void Paginate_SongAutofitSettings_AreIgnored()
    {
        var theme = MakeTheme(fontSize: 60);
        theme.ScriptureMaxShrinkRatio = 0M;
        theme.AutofitEnabled = true;
        theme.AutofitMinFontSizeRatio = 0.1M;

        var pages = ScriptureParagraphLayoutEngine.Paginate(OverflowingPassage(), "Test 1:1", theme);

        Assert.IsTrue(pages.Count > 1);
        Assert.IsTrue(pages.All(p => p.FontSize == 60f));
    }

    [TestMethod]
    public void Paginate_LongPassage_ProducesMultiplePages()
    {
        var verses = Enumerable.Range(1, 40)
            .Select(v => new ScriptureVerseRef(1, v, "This is a reasonably long verse of sample text for pagination testing purposes."))
            .ToList();

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Test 1:1-40", MakeTheme(fontSize: 80));

        Assert.IsTrue(pages.Count > 1);
    }

    [TestMethod]
    public void Paginate_VerseNumberNeverOrphanedFromFirstWord()
    {
        var verses = Enumerable.Range(1, 40)
            .Select(v => new ScriptureVerseRef(1, v, "This is a reasonably long verse of sample text for pagination testing purposes."))
            .ToList();

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Test 1:1-40", MakeTheme(fontSize: 80));

        foreach (var page in pages)
        {
            foreach (var line in page.Lines)
            {
                if (line.Runs.Count == 0) continue;
                var lastRun = line.Runs[^1];
                Assert.IsFalse(lastRun.IsSuperscript,
                    "a line must never end with an orphaned superscript marker and no following word");
            }
        }
    }

    [TestMethod]
    public void Paginate_ChapterChangeVerse_ShowsChapterPrefixedMarker()
    {
        var verses = MakeVerses(
            (1, 25, "Follow peace with all men, and holiness."),
            (2, 1, "Wherefore laying aside all malice."));

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Hebrews 12:25-13:1", MakeTheme());

        var allRuns = pages.SelectMany(p => p.Lines).SelectMany(l => l.Runs).ToList();
        Assert.IsTrue(allRuns.Any(r => r.IsSuperscript && r.Text == "25"), "first verse keeps a bare verse number");
        Assert.IsTrue(allRuns.Any(r => r.IsSuperscript && r.Text == "2:1"), "chapter-change verse gets a chapter-prefixed marker");
    }

    [TestMethod]
    public void Paginate_FirstVerseAlsoGetsSuperscriptMarker()
    {
        var verses = MakeVerses((1, 1, "In the beginning God created the heaven and the earth."));

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Genesis 1:1", MakeTheme());

        var allRuns = pages.SelectMany(p => p.Lines).SelectMany(l => l.Runs).ToList();
        Assert.IsTrue(allRuns.Any(r => r.IsSuperscript && r.Text == "1"), "the very first verse must still show its number");
    }

    // The header renders bold per the design spec (measured with a bold typeface so wrapping
    // decisions match what's drawn). ScriptureParagraphLine/Run don't carry font-weight info, so
    // there's no way to assert "is bold" directly from the pagination output -- the meaningful
    // thing to verify here is that Paginate still runs correctly and produces the expected header
    // content now that the header is measured with a different (bold) typeface.
    [TestMethod]
    public void Paginate_HeaderMeasuredWithBoldTypeface_StillProducesExpectedHeaderContent()
    {
        var verses = MakeVerses((1, 1, "In the beginning God created the heaven and the earth."));

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Genesis 1:1", MakeTheme());

        var headerLine = pages[0].Lines.First(l => l.IsHeader);
        var headerText = string.Concat(headerLine.Runs.Select(r => r.Text));
        Assert.AreEqual("Genesis 1:1", headerText);
    }

    [TestMethod]
    public void Paginate_PathologicalSingleTooWideToken_PlacedAloneOnItsOwnLine()
    {
        var absurdlyLongWord = new string('a', 500);
        var verses = MakeVerses((1, 1, absurdlyLongWord));

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Test 1:1", MakeTheme(fontSize: 100));

        Assert.IsTrue(pages.Count >= 1, "must return without hanging or throwing");
        var allText = pages.SelectMany(p => p.Lines).SelectMany(l => l.Runs).Select(r => r.Text);
        Assert.IsTrue(allText.Any(t => t == absurdlyLongWord), "the oversized word must still appear, alone on its line");
    }
}
