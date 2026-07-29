using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureTitleFormatterTests
{
    [TestMethod]
    public void Format_SingleVerse_ReturnsBookChapterColonVerse()
    {
        Assert.AreEqual("Romans 8:28", ScriptureTitleFormatter.Format("Romans", 8, 28, 8, 28));
    }

    [TestMethod]
    public void Format_SameChapterRange_ReturnsBookChapterColonVerseDashVerse()
    {
        Assert.AreEqual("1 Peter 1:10-12", ScriptureTitleFormatter.Format("1 Peter", 1, 10, 1, 12));
    }

    [TestMethod]
    public void Format_CrossChapterRange_ReturnsBookChapterColonVerseDashChapterColonVerse()
    {
        Assert.AreEqual("1 Peter 1:20-2:8", ScriptureTitleFormatter.Format("1 Peter", 1, 20, 2, 8));
    }
}
