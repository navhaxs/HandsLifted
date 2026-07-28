using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Tests.Models.Items;

[TestClass]
public class ScriptureItemTests
{
    [TestMethod]
    public void ScriptureItem_DefaultsToChapterOneVerseOne()
    {
        var item = new ScriptureItem();

        Assert.AreEqual("", item.Translation);
        Assert.AreEqual("", item.Book);
        Assert.AreEqual(1, item.StartChapter);
        Assert.AreEqual(1, item.StartVerse);
        Assert.AreEqual(1, item.EndChapter);
        Assert.AreEqual(1, item.EndVerse);
    }

    [TestMethod]
    public void ScriptureItem_PropertiesAreSettable()
    {
        var item = new ScriptureItem
        {
            Translation = "eng_bsb",
            Book = "JHN",
            StartChapter = 3,
            StartVerse = 16,
            EndChapter = 3,
            EndVerse = 21
        };

        Assert.AreEqual("eng_bsb", item.Translation);
        Assert.AreEqual("JHN", item.Book);
        Assert.AreEqual(3, item.StartChapter);
        Assert.AreEqual(16, item.StartVerse);
        Assert.AreEqual(3, item.EndChapter);
        Assert.AreEqual(21, item.EndVerse);
    }
}
