using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Tests.Slides;

[TestClass]
public class ScriptureSlideTests
{
    [TestMethod]
    public void ScriptureSlide_ExposesTextLabelAndParent()
    {
        var item = new ScriptureItem { Book = "JHN" };
        var slide = new ScriptureSlide(item, "3:16") { Text = "For God so loved the world...", Label = "John 3:16" };

        Assert.AreEqual("3:16", slide.Id);
        Assert.AreEqual("For God so loved the world...", slide.Text);
        Assert.AreEqual("John 3:16", slide.Label);
        Assert.AreEqual("For God so loved the world...", slide.SlideText);
        Assert.AreEqual("John 3:16", slide.SlideLabel);
        Assert.AreSame(item, slide.ParentScriptureItem);
    }

    // Two different scripture items both number their own pages "page0", "page1", ...
    // independently, so slides from different readings can share an Id -- they must not compare
    // equal just because of that (see ScriptureSlide's removed Equals override).
    [TestMethod]
    public void Equals_TwoDifferentSlidesWithSameId_AreNotEqual()
    {
        var slideA = new ScriptureSlide(null, "page0");
        var slideB = new ScriptureSlide(null, "page0");

        Assert.AreNotEqual(slideA, slideB);
    }
}
