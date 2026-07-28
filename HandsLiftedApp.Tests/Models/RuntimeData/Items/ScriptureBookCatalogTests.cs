using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureBookCatalogTests
{
    [TestMethod]
    public void AllBooks_HasExactly66Entries()
    {
        Assert.AreEqual(66, ScriptureBookCatalog.AllBooks.Count);
    }

    [TestMethod]
    public void AllBooks_CodesMatchScriptureUsxDownloaderAllBookCodes_SameOrder()
    {
        var codes = ScriptureBookCatalog.AllBooks.Select(b => b.Code).ToList();
        CollectionAssert.AreEqual(ScriptureUsxDownloader.AllBookCodes.ToList(), codes);
    }

    [TestMethod]
    public void AllBooks_NamesAreAllUnique()
    {
        var names = ScriptureBookCatalog.AllBooks.Select(b => b.Name).ToList();
        Assert.AreEqual(names.Count, names.Distinct().Count());
    }

    [TestMethod]
    public void AllBooks_NamesMatchExpectedCodes_AtKeyPositions()
    {
        Assert.AreEqual("Genesis", ScriptureBookCatalog.AllBooks.Single(b => b.Code == "gen").Name);
        Assert.AreEqual("Malachi", ScriptureBookCatalog.AllBooks.Single(b => b.Code == "mal").Name);
        Assert.AreEqual("Matthew", ScriptureBookCatalog.AllBooks.Single(b => b.Code == "mat").Name);
        Assert.AreEqual("Revelation", ScriptureBookCatalog.AllBooks.Single(b => b.Code == "rev").Name);
        Assert.AreEqual("Song of Solomon", ScriptureBookCatalog.AllBooks.Single(b => b.Code == "sng").Name);
        Assert.AreEqual("Ecclesiastes", ScriptureBookCatalog.AllBooks.Single(b => b.Code == "ecc").Name);
    }
}
