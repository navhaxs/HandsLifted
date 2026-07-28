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
