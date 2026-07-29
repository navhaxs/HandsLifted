using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Tests.Models.Items;

[TestClass]
public class ItemTests
{
    [TestMethod]
    public void SlideTransitionDurationMs_DefaultsToNull()
    {
        var item = new BlankItem();

        Assert.IsNull(item.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void SlideTransitionDurationMs_CanBeSetAndRead()
    {
        var item = new BlankItem();

        item.SlideTransitionDurationMs = 500.0;

        Assert.AreEqual(500.0, item.SlideTransitionDurationMs);
    }
}
