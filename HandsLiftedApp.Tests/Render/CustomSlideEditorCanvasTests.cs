using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CustomSlideModel = HandsLiftedApp.Data.Data.Models.Slides.CustomSlide;

namespace HandsLiftedApp.Tests.Render;

[TestClass]
public class CustomSlideEditorCanvasTests
{
    private static (CustomSlideModel Slide, TextElement Text, CustomSlideEditorCanvas Canvas) Setup()
    {
        var text = new TextElement { X = 10, Y = 10, Width = 200, Height = 100, Text = "Hello" };
        var slide = new CustomSlideModel();
        slide.SlideElements.Add(text);
        var canvas = new CustomSlideEditorCanvas { Slide = slide };
        Dispatcher.UIThread.RunJobs();
        return (slide, text, canvas);
    }

    private static void AssertIncrements(CustomSlideEditorCanvas canvas, string what, System.Action change)
    {
        int before = canvas.InvalidationCount;
        change();
        Dispatcher.UIThread.RunJobs();
        Assert.IsTrue(canvas.InvalidationCount > before, $"canvas did not invalidate after {what} changed");
    }

    [TestMethod]
    public Task Canvas_InvalidatesWhenElementTextChanges() => DispatcherTestThread.Run(() =>
    {
        var (slide, text, canvas) = Setup();

        AssertIncrements(canvas, "Text", () => text.Text = "World");
        AssertIncrements(canvas, "X", () => text.X = 42);
        AssertIncrements(canvas, "Italic", () => text.Italic = true);

        var added = new TextElement { Text = "New" };
        AssertIncrements(canvas, "SlideElements.Add", () => slide.SlideElements.Add(added));
        // an element added after the canvas started listening is followed too
        AssertIncrements(canvas, "added element's Text", () => added.Text = "Changed");

        AssertIncrements(canvas, "BackgroundAvaloniaColour", () => slide.BackgroundAvaloniaColour = Colors.Red);
        AssertIncrements(canvas, "BackgroundGraphicFilePath", () => slide.BackgroundGraphicFilePath = "C:/nope.png");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Canvas_StopsListening_AfterDetach() => DispatcherTestThread.Run(() =>
    {
        var (slide, text, canvas) = Setup();
        var removed = new TextElement { Text = "gone" };
        slide.SlideElements.Add(removed);
        slide.SlideElements.Remove(removed);
        Dispatcher.UIThread.RunJobs();

        // a removed element is no longer followed
        int afterRemove = canvas.InvalidationCount;
        removed.Text = "still gone";
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(afterRemove, canvas.InvalidationCount, "canvas still follows a removed element");

        canvas.Slide = null;
        Dispatcher.UIThread.RunJobs();
        int before = canvas.InvalidationCount;

        text.Text = "World";
        text.X = 42;
        text.Italic = true;
        slide.SlideElements.Add(new TextElement());
        slide.BackgroundAvaloniaColour = Colors.Red;
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual(before, canvas.InvalidationCount, "canvas still listens to a slide it no longer shows");
        return Task.CompletedTask;
    });
}
