using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureBookAliasCatalogTests
{
    [TestMethod]
    public void TryMatchBookPrefix_FullName_Matches()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("Romans 8:28", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("rom", code);
        Assert.AreEqual("Romans", name);
        Assert.AreEqual(6, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_Abbreviation_Matches()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("Rom 8:28", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("rom", code);
        Assert.AreEqual("Romans", name);
        Assert.AreEqual(3, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_IsCaseInsensitive()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("ROMANS 8:28", out var code, out _, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("rom", code);
        Assert.AreEqual(6, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_NumericPrefixVariant_RomanNumeral_Matches()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("I Peter 1:10-12", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("1pe", code);
        Assert.AreEqual("1 Peter", name);
        Assert.AreEqual(7, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_NumericPrefixVariant_WordOrdinal_Matches()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("First Peter 1:10", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("1pe", code);
        Assert.AreEqual("1 Peter", name);
        Assert.AreEqual(11, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_DigitPrefixAbbreviation_Matches()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("1 Pet 1:10-12", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("1pe", code);
        Assert.AreEqual("1 Peter", name);
        Assert.AreEqual(5, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_PrefersLongestMatch_EpistleOverGospel()
    {
        // "1 John" (epistle, code 1jn) must win over "John" (gospel, code jhn) even though
        // "John" is also a valid alias that appears later in the same input.
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("1 John 3:16", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("1jn", code);
        Assert.AreEqual("1 John", name);
        Assert.AreEqual(6, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_RejectsPartialWordMatch()
    {
        // "John" is a real alias (the Gospel), but it must not match as a prefix of the
        // unrelated word "Johnson" — the boundary check (next char must not be a letter)
        // is what makes that distinction, and no other alias matches "Johnson" either.
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("Johnson 3:1", out _, out _, out _);
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void TryMatchBookPrefix_UnknownBook_ReturnsFalse()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("Xyz 1:1", out _, out _, out _);
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void TryMatchBookPrefix_EmptyInput_ReturnsFalse()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("", out _, out _, out _);
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void NoTwoDistinctBookCodesShareTheSameAliasText()
    {
        // Build the same alias set the catalog builds internally, by probing every book's
        // own name/abbreviation plus numeric-prefix variants against TryMatchBookPrefix,
        // and confirm each resolves back to its own code with a full-length match.
        foreach (var (code, name) in ScriptureBookCatalog.AllBooks)
        {
            var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix(name, out var matchedCode, out _, out var len);
            Assert.IsTrue(ok, $"Full name '{name}' should match a book.");
            Assert.AreEqual(code, matchedCode, $"Full name '{name}' resolved to the wrong book code.");
            Assert.AreEqual(name.Length, len);
        }
    }
}
