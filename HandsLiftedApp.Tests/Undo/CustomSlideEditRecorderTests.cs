using System;
using System.Threading.Tasks;
using HandsLiftedApp.Tests.TestSupport;
using System.Linq;
using Avalonia.Media;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Undo;

[TestClass]
public class CustomSlideEditRecorderTests
{
    private static (CustomSlide Slide, UndoStack Stack, CustomSlideEditRecorder Recorder) Open(
        Func<DateTime>? now = null)
    {
        var slide = new CustomSlide();
        slide.SlideElements.Add(new TextElement { Text = "Welcome", X = 100, Y = 200, Width = 800, Height = 150 });
        slide.SlideElements.Add(new ImageElement { FilePath = "logo.png", X = 10, Y = 10 });
        var stack = now == null ? new UndoStack() : new UndoStack(now, TimeSpan.FromSeconds(1.5));
        var recorder = new CustomSlideEditRecorder(stack, slide);
        return (slide, stack, recorder);
    }

    [TestMethod]
    public Task EditingText_IsUndoable_AndNamedSo() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, _) = Open();
        ((TextElement)slide.SlideElements[0]).Text = "Welcome everyone";

        Assert.AreEqual("Undo Edit Text", stack.UndoMenuText);
        stack.Undo();
        Assert.AreEqual("Welcome", ((TextElement)slide.SlideElements[0]).Text);
        stack.Redo();
        Assert.AreEqual("Welcome everyone", ((TextElement)slide.SlideElements[0]).Text);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task DraggingAnElement_MergesIntoOneStep() => DispatcherTestThread.Run(() =>
    {
        var clock = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var (slide, stack, _) = Open(() => clock);
        var el = slide.SlideElements[0];

        for (var i = 1; i <= 20; i++)
        {
            el.X = 100 + i * 5;
            el.Y = 200 + i * 3;
            clock = clock.AddMilliseconds(16);
        }

        Assert.AreEqual("Undo Move Element", stack.UndoMenuText);
        stack.Undo();
        Assert.AreEqual(100, el.X);
        Assert.AreEqual(200, el.Y);
        Assert.IsFalse(stack.CanUndo, "the whole drag is one step");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ResizingFromACorner_ChangesPositionAndSize_ButIsStillOneStep() => DispatcherTestThread.Run(() =>
    {
        var clock = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var (slide, stack, _) = Open(() => clock);
        var el = slide.SlideElements[0]; // X=100 Y=200 W=800 H=150

        // dragging the top-left handle: each pointer move changes X, Y, Width and Height together, and
        // some moves only change one axis
        for (var i = 1; i <= 15; i++)
        {
            el.X = 100 + i * 4;
            el.Width = 800 - i * 4;
            if (i % 3 != 0)
            {
                el.Y = 200 + i * 2;
                el.Height = 150 - i * 2;
            }

            clock = clock.AddMilliseconds(16);
        }

        Assert.AreEqual("Undo Resize Element", stack.UndoMenuText);
        stack.Undo();
        Assert.AreEqual(100, el.X);
        Assert.AreEqual(200, el.Y);
        Assert.AreEqual(800, el.Width);
        Assert.AreEqual(150, el.Height);
        Assert.IsFalse(stack.CanUndo, "the whole resize is one step");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ResizingAnElement_IsUndoable() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, _) = Open();
        slide.SlideElements[0].Width = 400;

        Assert.AreEqual("Undo Resize Element", stack.UndoMenuText);
        stack.Undo();
        Assert.AreEqual(800, slide.SlideElements[0].Width);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task FormattingAndColour_AreUndoable_IncludingFontFamily() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, _) = Open();
        var t = (TextElement)slide.SlideElements[0];
        var originalFont = t.FontFamilyAsText;
        var originalColour = t.ForegroundAvaloniaColour;

        t.FontFamilyAsText = "Verdana"; // the default is Arial
        Assert.AreEqual("Undo Format Text", stack.UndoMenuText);
        t.ForegroundAvaloniaColour = Colors.Red;
        Assert.AreEqual("Undo Change Colour", stack.UndoMenuText);

        stack.Undo();
        Assert.AreEqual(originalColour, t.ForegroundAvaloniaColour);
        stack.Undo();
        Assert.AreEqual(originalFont, t.FontFamilyAsText);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task BackgroundColourAndGraphic_AreUndoable() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, _) = Open();
        var original = slide.BackgroundAvaloniaColour;

        slide.BackgroundAvaloniaColour = Colors.Black;
        slide.BackgroundGraphicFilePath = "bg.png";
        stack.Undo();
        Assert.IsNull(slide.BackgroundGraphicFilePath);
        stack.Undo();
        Assert.AreEqual(original, slide.BackgroundAvaloniaColour);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AddingAnElement_UndoRemovesIt_RedoBringsItBack() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, _) = Open();
        slide.SlideElements.Add(new TextElement { Text = "New box", X = 5 });

        Assert.AreEqual("Undo Add Element", stack.UndoMenuText);
        stack.Undo();
        Assert.AreEqual(2, slide.SlideElements.Count);
        stack.Redo();
        Assert.AreEqual(3, slide.SlideElements.Count);
        Assert.AreEqual("New box", ((TextElement)slide.SlideElements[2]).Text);
        Assert.AreEqual(5, slide.SlideElements[2].X);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task DeletingAnElement_FromTheMiddle_UndoRestoresEveryElementInOrder() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, _) = Open();
        slide.SlideElements.Add(new TextElement { Text = "Third" });
        stack.Clear();

        slide.SlideElements.RemoveAt(1); // the image

        Assert.AreEqual("Undo Delete Element", stack.UndoMenuText);
        stack.Undo();

        Assert.AreEqual(3, slide.SlideElements.Count);
        Assert.AreEqual("Welcome", ((TextElement)slide.SlideElements[0]).Text);
        Assert.IsInstanceOfType<ImageElement>(slide.SlideElements[1]);
        Assert.AreEqual("logo.png", ((ImageElement)slide.SlideElements[1]).FilePath);
        Assert.AreEqual("Third", ((TextElement)slide.SlideElements[2]).Text);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ReorderingElements_UndoRestoresTheOrder() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, _) = Open();
        slide.SlideElements.Move(0, 1);

        Assert.AreEqual("Undo Reorder Elements", stack.UndoMenuText);
        stack.Undo();
        Assert.IsInstanceOfType<TextElement>(slide.SlideElements[0]);
        Assert.IsInstanceOfType<ImageElement>(slide.SlideElements[1]);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ABatch_IsOneStep_WithItsOwnName() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, recorder) = Open();
        var image = (ImageElement)slide.SlideElements[1];

        using (recorder.BeginBatch("Change Image"))
        {
            image.Width = 640;
            image.Height = 480;
            image.X = 5;
            image.FilePath = "photo.jpg";
        }

        Assert.AreEqual("Undo Change Image", stack.UndoMenuText);
        stack.Undo();
        Assert.IsFalse(stack.CanUndo, "picture and size are one step");
        Assert.AreEqual("logo.png", image.FilePath);
        Assert.AreEqual(100, image.Width);
        Assert.AreEqual(100, image.Height);
        Assert.AreEqual(10, image.X);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ChangingAnImage_IsUndoable() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, _) = Open();
        ((ImageElement)slide.SlideElements[1]).FilePath = "other.png";

        Assert.AreEqual("Undo Change Image", stack.UndoMenuText);
        stack.Undo();
        Assert.AreEqual("logo.png", ((ImageElement)slide.SlideElements[1]).FilePath);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task UndoItself_IsNotRecorded_AndLaterEditsStillDiffCorrectly() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, _) = Open();
        var t = (TextElement)slide.SlideElements[0];

        t.Text = "A";
        stack.Undo();
        Assert.IsFalse(stack.CanUndo);

        t = (TextElement)slide.SlideElements[0];
        t.Text = "B";
        stack.Undo();
        Assert.AreEqual("Welcome", ((TextElement)slide.SlideElements[0]).Text);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task WholeCollectionReplaced_IsFollowed() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, _) = Open();
        slide.SlideElements = new(new[] { (SlideElement)new TextElement { Text = "Fresh" } });
        stack.Clear();

        ((TextElement)slide.SlideElements[0]).Text = "Fresh!";
        Assert.IsTrue(stack.CanUndo);
        stack.Undo();
        Assert.AreEqual("Fresh", ((TextElement)slide.SlideElements[0]).Text);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Dispose_StopsRecording() => DispatcherTestThread.Run(() =>
    {
        var (slide, stack, recorder) = Open();
        recorder.Dispose();
        ((TextElement)slide.SlideElements[0]).Text = "After close";
        slide.SlideElements.Add(new TextElement());
        Assert.IsFalse(stack.CanUndo);
        return Task.CompletedTask;
    });
}
