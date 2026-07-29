using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureReferenceParserTests
{
    [TestMethod]
    public void TryParse_WholeChapter_Succeeds()
    {
        var ok = ScriptureReferenceParser.TryParse("John 3", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("jhn", result.BookCode);
        Assert.AreEqual(3, result.StartChapter);
        Assert.IsNull(result.StartVerse);
        Assert.AreEqual(3, result.EndChapter);
        Assert.IsNull(result.EndVerse);
    }

    [TestMethod]
    public void TryParse_SingleVerse_Succeeds()
    {
        var ok = ScriptureReferenceParser.TryParse("Rom 8:28", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("rom", result.BookCode);
        Assert.AreEqual(8, result.StartChapter);
        Assert.AreEqual(28, result.StartVerse);
        Assert.AreEqual(8, result.EndChapter);
        Assert.AreEqual(28, result.EndVerse);
    }

    [TestMethod]
    public void TryParse_SameChapterRange_Succeeds()
    {
        var ok = ScriptureReferenceParser.TryParse("1 Peter 1:10-12", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("1pe", result.BookCode);
        Assert.AreEqual(1, result.StartChapter);
        Assert.AreEqual(10, result.StartVerse);
        Assert.AreEqual(1, result.EndChapter);
        Assert.AreEqual(12, result.EndVerse);
    }

    [TestMethod]
    public void TryParse_CrossChapterRange_Succeeds()
    {
        var ok = ScriptureReferenceParser.TryParse("1 Peter 1:20-2:8", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("1pe", result.BookCode);
        Assert.AreEqual(1, result.StartChapter);
        Assert.AreEqual(20, result.StartVerse);
        Assert.AreEqual(2, result.EndChapter);
        Assert.AreEqual(8, result.EndVerse);
    }

    [TestMethod]
    public void TryParse_IsWhitespaceTolerant()
    {
        var ok = ScriptureReferenceParser.TryParse("  Romans   8 : 28  ", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("rom", result.BookCode);
        Assert.AreEqual(8, result.StartChapter);
        Assert.AreEqual(28, result.StartVerse);
    }

    [TestMethod]
    public void TryParse_NumericPrefixVariant_Succeeds()
    {
        var ok = ScriptureReferenceParser.TryParse("First Peter 1:10-12", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("1pe", result.BookCode);
        Assert.AreEqual(1, result.StartChapter);
        Assert.AreEqual(10, result.StartVerse);
        Assert.AreEqual(12, result.EndVerse);
    }

    [TestMethod]
    public void TryParse_UnknownBook_FailsWithBookNameInError()
    {
        var ok = ScriptureReferenceParser.TryParse("Xyz 1:1", out _, out var error);
        Assert.IsFalse(ok);
        StringAssert.Contains(error, "Xyz");
    }

    [TestMethod]
    public void TryParse_NoChapterAtAll_Fails()
    {
        var ok = ScriptureReferenceParser.TryParse("John", out _, out var error);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParse_GarbageAfterBook_Fails()
    {
        var ok = ScriptureReferenceParser.TryParse("John abc", out _, out var error);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParse_EmptyInput_Fails()
    {
        var ok = ScriptureReferenceParser.TryParse("", out _, out var error);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParse_WhitespaceOnlyInput_Fails()
    {
        var ok = ScriptureReferenceParser.TryParse("   ", out _, out var error);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParse_ChapterNumberOverflowsInt_FailsWithoutThrowing()
    {
        var ok = ScriptureReferenceParser.TryParse("John 99999999999999", out _, out var error);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }
}
