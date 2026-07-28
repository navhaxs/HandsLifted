using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Slides;

[TestClass]
public class ScriptureSlideInstanceTests
{
    [TestMethod]
    public void Constructor_SetsTextAndLabelWhenProvided()
    {
        var slide = new ScriptureSlideInstance(null, "1:1", text: "In the beginning...", label: "Genesis 1:1");

        Assert.AreEqual("1:1", slide.Id);
        Assert.AreEqual("In the beginning...", slide.Text);
        Assert.AreEqual("Genesis 1:1", slide.Label);
    }

    [TestMethod]
    public void Constructor_LeavesDefaultsWhenTextAndLabelOmitted()
    {
        var slide = new ScriptureSlideInstance(null, "1:1");

        Assert.AreEqual("", slide.Text);
        Assert.AreEqual("", slide.Label);
    }

    [TestMethod]
    public void CachedAndThumbnail_DefaultToNull()
    {
        var slide = new ScriptureSlideInstance(null, "1:1");

        Assert.IsNull(slide.Cached);
        Assert.IsNull(slide.Thumbnail);
        Assert.IsNull(slide.SlideTimerConfig);
        Assert.IsNull(slide.SlideThumbnailBadge);
    }

    [TestMethod]
    public void ParentScriptureItem_IsPassedThroughToBase()
    {
        var item = new ScriptureItem { Book = "JHN" };
        var slide = new ScriptureSlideInstance(item, "3:16");

        Assert.AreSame(item, slide.ParentScriptureItem);
    }

    [TestMethod]
    public void Theme_DefaultsToNonNullAfterConstruction()
    {
        var slide = new ScriptureSlideInstance(null, "1:1");

        Assert.IsNotNull(slide.Theme);
    }

    [TestMethod]
    public void Theme_IsSettable()
    {
        var slide = new ScriptureSlideInstance(null, "1:1");
        var customTheme = new HandsLiftedApp.Data.SlideTheme.BaseSlideTheme { FontSize = 42 };

        slide.Theme = customTheme;

        Assert.AreSame(customTheme, slide.Theme);
    }

    [TestMethod]
    public void ScriptureSlideInstance_ImplementsIRenderable()
    {
        var slide = new ScriptureSlideInstance(null, "1:1");

        Assert.IsInstanceOfType(slide, typeof(HandsLiftedApp.Core.Services.IRenderable));
    }
}
