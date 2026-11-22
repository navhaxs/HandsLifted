using System.Threading.Tasks;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Views;

[TestClass]
public class SlideElementCommandsTests
{
    [TestMethod]
    public Task Duplicate_CopiesAllTextStylesIncludingItalic() => DispatcherTestThread.Run(() =>
    {
        var slide = new CustomSlide();
        var src = new TextElement { Text = "Hi", X = 100, Y = 100, Width = 300, Height = 80, Italic = true, Underline = true };
        slide.SlideElements.Add(src);

        var copy = SlideElementCommands.Duplicate(slide, src) as TextElement;

        Assert.IsNotNull(copy);
        Assert.AreNotSame(src, copy);
        Assert.AreEqual("Hi", copy.Text);
        Assert.IsTrue(copy.Italic);
        Assert.IsTrue(copy.Underline);
        Assert.AreEqual(120, copy.X);
        Assert.AreEqual(120, copy.Y);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Duplicate_ClampsInsideSlide() => DispatcherTestThread.Run(() =>
    {
        var slide = new CustomSlide();
        var src = new TextElement { X = slide.SlideWidth - 300, Y = slide.SlideHeight - 80, Width = 300, Height = 80 };
        slide.SlideElements.Add(src);

        var copy = SlideElementCommands.Duplicate(slide, src)!;

        Assert.AreEqual(slide.SlideWidth - 300, copy.X);
        Assert.AreEqual(slide.SlideHeight - 80, copy.Y);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Duplicate_PlacesCopyInFrontOfSource() => DispatcherTestThread.Run(() =>
    {
        var slide = new CustomSlide();
        var a = new TextElement { Text = "a" };
        var b = new TextElement { Text = "b" };
        slide.SlideElements.Add(a);
        slide.SlideElements.Add(b);

        var copy = SlideElementCommands.Duplicate(slide, b)!;

        Assert.AreEqual(3, slide.SlideElements.Count);
        Assert.AreSame(a, slide.SlideElements[0]);
        Assert.AreSame(copy, slide.SlideElements[1]);
        Assert.AreSame(b, slide.SlideElements[2]);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Delete_Null_ReturnsFalse() => DispatcherTestThread.Run(() =>
    {
        var slide = new CustomSlide();
        Assert.IsFalse(SlideElementCommands.Delete(slide, null));
        Assert.IsFalse(SlideElementCommands.Delete(slide, new TextElement()));
        var el = new TextElement();
        slide.SlideElements.Add(el);
        Assert.IsTrue(SlideElementCommands.Delete(slide, el));
        Assert.AreEqual(0, slide.SlideElements.Count);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task BringForward_AtFront_ReturnsFalse() => DispatcherTestThread.Run(() =>
    {
        var slide = new CustomSlide();
        var a = new TextElement();
        var b = new TextElement();
        slide.SlideElements.Add(a);
        slide.SlideElements.Add(b);

        Assert.IsFalse(SlideElementCommands.BringForward(slide, a));
        Assert.IsTrue(SlideElementCommands.BringForward(slide, b));
        Assert.AreSame(b, slide.SlideElements[0]);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task SendBackward_SwapsWithNextElement() => DispatcherTestThread.Run(() =>
    {
        var slide = new CustomSlide();
        var a = new TextElement();
        var b = new TextElement();
        slide.SlideElements.Add(a);
        slide.SlideElements.Add(b);

        Assert.IsTrue(SlideElementCommands.SendBackward(slide, a));
        Assert.AreSame(b, slide.SlideElements[0]);
        Assert.AreSame(a, slide.SlideElements[1]);
        Assert.IsFalse(SlideElementCommands.SendBackward(slide, a));
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Nudge_Null_ReturnsFalse() => DispatcherTestThread.Run(() =>
    {
        Assert.IsFalse(SlideElementCommands.Nudge(null, 1, 1));
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Nudge_MovesByDelta() => DispatcherTestThread.Run(() =>
    {
        var el = new TextElement { X = 10, Y = 20 };
        Assert.IsTrue(SlideElementCommands.Nudge(el, -3, 5));
        Assert.AreEqual(7, el.X);
        Assert.AreEqual(25, el.Y);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AddText_IsCentredAndFrontmost() => DispatcherTestThread.Run(() =>
    {
        var slide = new CustomSlide();
        slide.SlideElements.Add(new TextElement { Text = "existing" });

        var el = SlideElementCommands.AddText(slide);

        Assert.AreSame(el, slide.SlideElements[0]);
        Assert.AreEqual("Text", el.Text);
        Assert.AreEqual(slide.SlideWidth / 2 - el.Width / 2, el.X);
        Assert.AreEqual(slide.SlideHeight / 2 - el.Height / 2, el.Y);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AddImage_IsCentredAndFrontmost() => DispatcherTestThread.Run(() =>
    {
        var slide = new CustomSlide();
        slide.SlideElements.Add(new TextElement());

        var el = SlideElementCommands.AddImage(slide);

        Assert.AreSame(el, slide.SlideElements[0]);
        Assert.AreEqual(slide.SlideWidth / 2 - el.Width / 2, el.X);
        return Task.CompletedTask;
    });
}
