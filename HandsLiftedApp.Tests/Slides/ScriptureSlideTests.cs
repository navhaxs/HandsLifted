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

    [TestMethod]
    public void ScriptureSlide_EqualityIsById()
    {
        var slideA = new ScriptureSlide(null, "3:16") { Text = "first text" };
        var slideB = new ScriptureSlide(null, "3:16") { Text = "different text" };
        var slideC = new ScriptureSlide(null, "3:17");

        Assert.IsTrue(slideA.Equals(slideB));
        Assert.IsFalse(slideA.Equals(slideC));
    }
}
