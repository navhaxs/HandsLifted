using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Core.Views.Editors.FreeText;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Views;

// The test host has no window, so there is no adorner layer: the designer still tracks and reports its selection
// (SelectElement / OnUpdateSelectedElement / OnDeselectElement), which is what the keys act on. The adorner itself
// (handles on screen) and real key delivery to the focused canvas are not covered here.
[TestClass]
public class CustomSlideDesignerKeyboardTests
{
    private static (CustomSlideDesigner Designer, CustomSlide Slide, TextElement Text, ImageElement Image) Create()
    {
        AvaloniaTextRendering.EnsureInitialized();
        var text = new TextElement { Text = "Hello", X = 100, Y = 200, Width = 600, Height = 300 };
        var image = new ImageElement { X = 50, Y = 60 };
        var slide = new CustomSlide();
        slide.SlideElements.Add(text);
        slide.SlideElements.Add(image);
        var designer = new CustomSlideDesigner { DataContext = slide };
        AvaloniaTextRendering.Layout(designer, 800, 450);
        return (designer, slide, text, image);
    }

    [TestMethod]
    public Task Delete_RemovesSelected_NoSelection_IsNoOp() => DispatcherTestThread.Run(() =>
    {
        var (designer, slide, text, image) = Create();
        var deselected = 0;
        designer.OnDeselectElement += (_, _) => deselected++;

        Assert.IsFalse(designer.HandleKey(Key.Delete, KeyModifiers.None), "nothing selected");
        Assert.AreEqual(2, slide.SlideElements.Count);

        designer.SelectElement(text);
        Assert.IsTrue(designer.HandleKey(Key.Delete, KeyModifiers.None));
        CollectionAssert.AreEqual(new SlideElement[] { image }, slide.SlideElements.ToList());
        Assert.AreEqual(1, deselected, "the layers selection is cleared");
        Assert.IsFalse(designer.HandleKey(Key.Delete, KeyModifiers.None), "selection is gone");

        designer.SelectElement(image);
        Assert.IsTrue(designer.HandleKey(Key.Back, KeyModifiers.None), "Backspace deletes too");
        Assert.AreEqual(0, slide.SlideElements.Count);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Arrow_Nudges1_ShiftArrow_Nudges10() => DispatcherTestThread.Run(() =>
    {
        var (designer, _, text, _) = Create();
        Assert.IsFalse(designer.HandleKey(Key.Left, KeyModifiers.None), "nothing selected");
        Assert.AreEqual(100, text.X);

        designer.SelectElement(text);
        Assert.IsTrue(designer.HandleKey(Key.Left, KeyModifiers.None));
        Assert.AreEqual(99, text.X);
        Assert.IsTrue(designer.HandleKey(Key.Right, KeyModifiers.None));
        Assert.IsTrue(designer.HandleKey(Key.Right, KeyModifiers.None));
        Assert.AreEqual(101, text.X);
        Assert.IsTrue(designer.HandleKey(Key.Up, KeyModifiers.None));
        Assert.AreEqual(199, text.Y);
        Assert.IsTrue(designer.HandleKey(Key.Down, KeyModifiers.None));
        Assert.AreEqual(200, text.Y);

        Assert.IsTrue(designer.HandleKey(Key.Left, KeyModifiers.Shift));
        Assert.AreEqual(91, text.X);
        Assert.IsTrue(designer.HandleKey(Key.Right, KeyModifiers.Shift));
        Assert.AreEqual(101, text.X);
        Assert.IsTrue(designer.HandleKey(Key.Up, KeyModifiers.Shift));
        Assert.AreEqual(190, text.Y);
        Assert.IsTrue(designer.HandleKey(Key.Down, KeyModifiers.Shift));
        Assert.AreEqual(200, text.Y);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task CtrlD_DuplicatesAndSelectsCopy() => DispatcherTestThread.Run(() =>
    {
        var (designer, slide, text, _) = Create();
        SlideElement? reported = null;
        designer.OnUpdateSelectedElement += (_, e) => reported = e.SelectedElement;

        Assert.IsFalse(designer.HandleKey(Key.D, KeyModifiers.Control), "nothing selected");
        Assert.AreEqual(2, slide.SlideElements.Count);

        designer.SelectElement(text);
        Assert.IsTrue(designer.HandleKey(Key.D, KeyModifiers.Control));

        Assert.AreEqual(3, slide.SlideElements.Count);
        var copy = slide.SlideElements[0];
        Assert.AreNotSame(text, copy);
        Assert.AreSame(copy, reported, "the layers list is told to select the copy, not the original");

        // the next key acts on the copy
        Assert.IsTrue(designer.HandleKey(Key.Right, KeyModifiers.None));
        Assert.AreEqual(121, copy.X);
        Assert.AreEqual(100, text.X);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task CtrlD_ThroughEditor_SelectsCopyInLayersList() => DispatcherTestThread.Run(() =>
    {
        AvaloniaTextRendering.EnsureInitialized();
        var text = new TextElement { Text = "Hello" };
        var slide = new CustomSlide();
        slide.SlideElements.Add(text);
        var editor = new CustomSlideEditor { DataContext = slide };
        var designer = editor.FindControl<CustomSlideDesigner>("VisualEditor")!;
        var layers = editor.FindControl<ListBox>("ListBox")!;
        designer.DataContext = slide;

        layers.SelectedItem = text;
        Assert.IsTrue(designer.HandleKey(Key.D, KeyModifiers.Control));
        Assert.AreSame(slide.SlideElements[0], layers.SelectedItem);
        Assert.AreNotSame(text, layers.SelectedItem);

        Assert.IsTrue(designer.HandleKey(Key.Escape, KeyModifiers.None));
        Assert.IsNull(layers.SelectedItem, "Esc clears the layers selection");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Esc_Deselects() => DispatcherTestThread.Run(() =>
    {
        var (designer, slide, text, _) = Create();
        var deselected = 0;
        designer.OnDeselectElement += (_, _) => deselected++;

        Assert.IsFalse(designer.HandleKey(Key.Escape, KeyModifiers.None), "nothing selected");

        designer.SelectElement(text);
        Assert.IsTrue(designer.HandleKey(Key.Escape, KeyModifiers.None));
        Assert.AreEqual(1, deselected);
        Assert.AreEqual(2, slide.SlideElements.Count, "Esc removes nothing");
        Assert.IsFalse(designer.HandleKey(Key.Right, KeyModifiers.None), "no longer selected");
        Assert.AreEqual(100, text.X);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Keys_AreIgnoredWhileEditing() => DispatcherTestThread.Run(() =>
    {
        var (designer, slide, text, _) = Create();
        designer.SelectElement(text);
        designer.BeginEdit(text);
        Assert.IsTrue(designer.IsEditing);

        foreach (var (key, mods) in new[]
                 {
                     (Key.Delete, KeyModifiers.None), (Key.Back, KeyModifiers.None), (Key.Left, KeyModifiers.None),
                     (Key.Right, KeyModifiers.Shift), (Key.D, KeyModifiers.Control), (Key.Escape, KeyModifiers.None),
                     (Key.Enter, KeyModifiers.None), (Key.F2, KeyModifiers.None),
                 })
            Assert.IsFalse(designer.HandleKey(key, mods), $"{mods}+{key} while editing");

        Assert.AreEqual(2, slide.SlideElements.Count);
        Assert.AreEqual(100, text.X);
        Assert.IsTrue(designer.IsEditing);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task EnterOrF2_BeginsEdit_OnSelectedTextElement() => DispatcherTestThread.Run(() =>
    {
        var (designer, _, text, image) = Create();
        Assert.IsFalse(designer.HandleKey(Key.Enter, KeyModifiers.None), "nothing selected");

        designer.SelectElement(image);
        Assert.IsFalse(designer.HandleKey(Key.Enter, KeyModifiers.None), "images have no text to edit");
        Assert.IsFalse(designer.IsEditing);

        designer.SelectElement(text);
        Assert.IsTrue(designer.HandleKey(Key.Enter, KeyModifiers.None));
        Assert.IsTrue(designer.IsEditing);
        Assert.AreSame(text, designer.EditingElement);
        designer.CommitEdit();

        designer.SelectElement(text);
        Assert.IsTrue(designer.HandleKey(Key.F2, KeyModifiers.None));
        Assert.IsTrue(designer.IsEditing);
        return Task.CompletedTask;
    });

    // ---- Ctrl+V and dropped image files ----

    [TestMethod]
    public Task CtrlV_RaisesPasteRequested_WithOrWithoutASelection() => DispatcherTestThread.Run(() =>
    {
        var (designer, slide, text, _) = Create();
        var pasted = 0;
        designer.PasteRequested += (_, _) => pasted++;

        Assert.IsTrue(designer.HandleKey(Key.V, KeyModifiers.Control), "nothing selected: still a paste");
        Assert.AreEqual(1, pasted);

        designer.SelectElement(text);
        Assert.IsTrue(designer.HandleKey(Key.V, KeyModifiers.Control));
        Assert.AreEqual(2, pasted);

        Assert.IsFalse(designer.HandleKey(Key.V, KeyModifiers.None), "plain V is not a paste");
        Assert.IsFalse(designer.HandleKey(Key.V, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.AreEqual(2, pasted);
        Assert.AreEqual(2, slide.SlideElements.Count, "the designer itself adds nothing");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task CtrlV_WhileEditing_IsLeftToTheTextBox() => DispatcherTestThread.Run(() =>
    {
        var (designer, _, text, _) = Create();
        var pasted = 0;
        designer.PasteRequested += (_, _) => pasted++;
        designer.SelectElement(text);
        designer.BeginEdit(text);

        Assert.IsFalse(designer.HandleKey(Key.V, KeyModifiers.Control));
        Assert.AreEqual(0, pasted);
        return Task.CompletedTask;
    });

    [TestMethod]
    public void ImageFilePaths_KeepsOnlyImagesByExtension_IgnoringCase()
    {
        var paths = CustomSlideDesigner.ImageFilePaths(new[]
        {
            @"C:\a\one.png", @"C:\a\two.JPG", @"C:\a\clip.mp4", null, "", @"C:\a\folder", @"C:\a\three.jpeg",
        });

        CollectionAssert.AreEqual(new[] { @"C:\a\one.png", @"C:\a\two.JPG", @"C:\a\three.jpeg" }, paths.ToList());
        Assert.AreEqual(0, CustomSlideDesigner.ImageFilePaths(null).Count);
    }

    [TestMethod]
    public void DropEffectFor_CopyOnlyWhenAtLeastOneImageFile()
    {
        Assert.AreEqual(DragDropEffects.Copy, CustomSlideDesigner.DropEffectFor(new[] { @"C:\a\clip.mp4", @"C:\a\one.png" }));
        Assert.AreEqual(DragDropEffects.None, CustomSlideDesigner.DropEffectFor(new[] { @"C:\a\clip.mp4", @"C:\a\notes.txt" }));
        Assert.AreEqual(DragDropEffects.None, CustomSlideDesigner.DropEffectFor(Array.Empty<string?>()));
        Assert.AreEqual(DragDropEffects.None, CustomSlideDesigner.DropEffectFor(null));
    }

    [TestMethod]
    public Task DroppedImageFiles_AreReportedWithTheSlidePoint_NonImagesFilteredOut() => DispatcherTestThread.Run(() =>
    {
        var (designer, _, _, _) = Create();
        System.Collections.Generic.IReadOnlyList<string>? reported = null;
        Avalonia.Point at = default;
        designer.ImageFilesDropped += (paths, point) => { reported = paths; at = point; };

        designer.OnFilesDropped(new[] { @"C:\a\notes.txt" }, new Avalonia.Point(5, 5));
        Assert.IsNull(reported, "no image: nothing raised");

        designer.OnFilesDropped(new[] { @"C:\a\one.png", @"C:\a\notes.txt" }, new Avalonia.Point(300, 200));
        Assert.IsNotNull(reported);
        CollectionAssert.AreEqual(new[] { @"C:\a\one.png" }, reported.ToList());
        Assert.AreEqual(new Avalonia.Point(300, 200), at);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Canvas_AcceptsDrops() => DispatcherTestThread.Run(() =>
    {
        var (designer, _, _, _) = Create();
        var scroll = designer.FindControl<ScrollViewer>("MainScrollViewer")!;
        var root = designer.FindControl<Canvas>("Root")!;
        Assert.IsTrue(DragDrop.GetAllowDrop(scroll), "the grey area takes drops");
        Assert.IsTrue(DragDrop.GetAllowDrop(root), "and so does the slide (inherited)");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Nudge_IsOneUndoStepForRepeatedPresses() => DispatcherTestThread.Run(() =>
    {
        var (designer, slide, text, _) = Create();
        var clock = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var stack = new UndoStack(() => clock, TimeSpan.FromSeconds(1.5));
        using var recorder = new CustomSlideEditRecorder(stack, slide);

        designer.SelectElement(text);
        for (var i = 0; i < 8; i++)
        {
            Assert.IsTrue(designer.HandleKey(Key.Right, KeyModifiers.None));
            clock = clock.AddMilliseconds(100);
        }
        Assert.IsTrue(designer.HandleKey(Key.Down, KeyModifiers.Shift));

        Assert.AreEqual(108, text.X);
        Assert.AreEqual(210, text.Y);
        Assert.AreEqual("Undo Move Element", stack.UndoMenuText);
        stack.Undo();
        Assert.AreEqual(100, text.X);
        Assert.AreEqual(200, text.Y);
        Assert.IsFalse(stack.CanUndo, "all the presses were one step");
        return Task.CompletedTask;
    });
}
