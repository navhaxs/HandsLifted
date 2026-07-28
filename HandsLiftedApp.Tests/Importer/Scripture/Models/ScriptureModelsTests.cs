using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.Scripture.Models;

namespace HandsLiftedApp.Tests.Importer.Scripture.Models;

[TestClass]
public class ScriptureModelsTests
{
    [TestMethod]
    public void ScriptureBook_ExposesConstructorArguments()
    {
        var footnote = new ScriptureFootnote("1", "Cited in 2 Corinthians 4:6");
        var verse = new ScriptureVerseSegment(1, 3, "And God said, “Let there be light,”", new[] { footnote });
        var paragraph = new ScriptureParagraph(1, IsVerseContinuation: false, IsPoetry: false, PoetryIndentLevel: 0, new[] { verse });
        var book = new ScriptureBook("GEN", "Genesis", new[] { paragraph });

        Assert.AreEqual("GEN", book.Code);
        Assert.AreEqual("Genesis", book.Title);
        Assert.AreEqual(1, book.Paragraphs.Count);
        Assert.AreEqual(1, book.Paragraphs[0].StartChapter);
        Assert.IsFalse(book.Paragraphs[0].IsVerseContinuation);
        Assert.IsFalse(book.Paragraphs[0].IsPoetry);
        Assert.AreEqual(1, book.Paragraphs[0].Verses.Count);
        Assert.AreEqual(1, book.Paragraphs[0].Verses[0].Chapter);
        Assert.AreEqual(3, book.Paragraphs[0].Verses[0].VerseNumber);
        Assert.AreEqual("And God said, “Let there be light,”", book.Paragraphs[0].Verses[0].Text);
        Assert.AreEqual(1, book.Paragraphs[0].Verses[0].Footnotes.Count);
        Assert.AreEqual("Cited in 2 Corinthians 4:6", book.Paragraphs[0].Verses[0].Footnotes[0].Text);
    }

    [TestMethod]
    public void ScriptureParagraph_TracksPoetryIndentLevel()
    {
        var verse = new ScriptureVerseSegment(1, 27, "So God created man in His own image;", System.Array.Empty<ScriptureFootnote>());
        var paragraph = new ScriptureParagraph(1, IsVerseContinuation: false, IsPoetry: true, PoetryIndentLevel: 2, new[] { verse });

        Assert.IsTrue(paragraph.IsPoetry);
        Assert.AreEqual(2, paragraph.PoetryIndentLevel);
    }
}

