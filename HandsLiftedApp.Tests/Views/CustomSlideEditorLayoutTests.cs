using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HandsLiftedApp.Controls;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Core.Views.Editors.FreeText;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Views;

[TestClass]
public class CustomSlideEditorLayoutTests
{
    private static (CustomSlideEditor Editor, CustomSlide Slide, TextElement Text, ImageElement Image) CreateEditor()
    {
        AvaloniaTextRendering.EnsureInitialized();
        var text = new TextElement { Text = "Hello" };
        var image = new ImageElement();
        var slide = new CustomSlide();
        slide.SlideElements.Add(text);
        slide.SlideElements.Add(image);
        var editor = new CustomSlideEditor { DataContext = slide };
        return (editor, slide, text, image);
    }

    private static T Named<T>(Control scope, string name) where T : Control
    {
        var control = scope.FindControl<T>(name);
        Assert.IsNotNull(control, $"control '{name}' not found");
        return control;
    }

    // ---- Add Image: ask for the file first, then add a fitted element (no empty placeholder) ----

    // the test host has no window, so give the editor the history it would get when attached
    private static CustomSlideEditHistory GiveHistory(CustomSlideEditor editor, CustomSlide slide)
    {
        var history = new CustomSlideEditHistory(slide);
        typeof(CustomSlideEditor).GetField("_activeHistory",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(editor, history);
        return history;
    }

    private static CustomSlideEditor EditorOnEmptySlide(out CustomSlide slide)
    {
        AvaloniaTextRendering.EnsureInitialized();
        slide = new CustomSlide();
        slide.SlideElements.Add(new TextElement { Text = "existing" });
        return new CustomSlideEditor { DataContext = slide, MediaLibraryPathProvider = () => SharedLibrary };
    }

    // A "library" that pictures which need no real file (already inside it, so referenced in place) live in.
    private static readonly string SharedLibrary = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vs-test-shared-library");
    private static string InLib(string name) => System.IO.Path.GetFullPath(System.IO.Path.Combine(SharedLibrary, name));

    // The editor copies the picture on a pool thread and resumes on the test's thread. Depending on whether a
    // control has been created yet that thread's context is Avalonia's (its queue only drains on RunJobs(), so
    // block-and-pump) or the test pump's (blocking would deadlock the resume, so yield instead).
    private static async Task Pumped(Task task)
    {
        var deadline = System.DateTime.UtcNow.AddSeconds(15);
        var avalonia = System.Threading.SynchronizationContext.Current?.GetType().Name.Contains("Avalonia") == true;
        while (!task.IsCompleted && System.DateTime.UtcNow < deadline)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if (avalonia) System.Threading.Thread.Sleep(5);
            else await Task.Delay(5);
        }
        Assert.IsTrue(task.IsCompleted, "the editor action did not finish");
        await task;
    }

    private static readonly byte[] TinyPng = System.Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    private sealed class TempFolders : System.IDisposable
    {
        public string Library { get; } = Make();
        public string Outside { get; } = Make();
        private static string Make() => System.IO.Directory.CreateDirectory(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vs-test-" + System.Guid.NewGuid().ToString("N"))).FullName;

        public string OutsideFile(string name)
        {
            var p = System.IO.Path.Combine(Outside, name);
            System.IO.File.WriteAllBytes(p, TinyPng);
            return p;
        }

        public void Dispose()
        {
            try { System.IO.Directory.Delete(Library, true); } catch { }
            try { System.IO.Directory.Delete(Outside, true); } catch { }
        }
    }

    private static readonly System.Func<string, Task<PixelSize?>> Measure800x400 = _ => Task.FromResult<PixelSize?>(new PixelSize(800, 400));

    [TestMethod]
    public Task AddImage_PickedFileOutsideLibrary_IsCopiedIntoMediaImages_AndElementPointsAtTheCopy() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var picked = dirs.OutsideFile("photo.png");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;

        await Pumped(editor.AddImageAsync(() => Task.FromResult<string?>(picked), Measure800x400));

        var image = (ImageElement)slide.SlideElements[0];
        var imagesDir = System.IO.Path.Combine(dirs.Library, "Media", "Images");
        StringAssert.StartsWith(image.FilePath, imagesDir);
        Assert.IsTrue(System.IO.File.Exists(image.FilePath), "the copy exists");
        Assert.IsTrue(System.IO.File.Exists(picked), "the original is untouched");
    });

    [TestMethod]
    public Task AddImage_PickedFileAlreadyInLibrary_IsReferencedInPlace() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var inside = System.IO.Path.Combine(dirs.Library, "mine.png");
        System.IO.File.WriteAllBytes(inside, TinyPng);
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;

        await Pumped(editor.AddImageAsync(() => Task.FromResult<string?>(inside), Measure800x400));

        Assert.AreEqual(inside, ((ImageElement)slide.SlideElements[0]).FilePath);
        Assert.IsFalse(System.IO.Directory.Exists(System.IO.Path.Combine(dirs.Library, "Media")), "nothing copied");
    });

    [TestMethod]
    public Task AddImage_MediaLibraryNotConfigured_AddsNothing_AndShowsTheError() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var picked = dirs.OutsideFile("photo.png");
        var editor = EditorOnEmptySlide(out var slide);
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();
        editor.MediaLibraryPathProvider = () => null;
        var errors = new System.Collections.Generic.List<(string Title, string Message)>();
        editor.ShowMediaLibraryError = (t, m) => errors.Add((t, m));

        await Pumped(editor.AddImageAsync(() => Task.FromResult<string?>(picked), Measure800x400));

        Assert.AreEqual(1, slide.SlideElements.Count);
        Assert.IsFalse(history.Stack.CanUndo);
        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual("Media Library not configured", errors[0].Title);
    });

    [TestMethod]
    public Task ReplaceImage_PickedFileOutsideLibrary_IsCopied_AsOneUndoStep() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var picked = dirs.OutsideFile("new.png");
        var editor = EditorOnEmptySlide(out var slide);
        var history = GiveHistory(editor, slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        var image = new ImageElement { FilePath = InLib("old.png") };
        slide.SlideElements.Add(image);
        history.Stack.Clear();

        await Pumped(editor.ReplaceImageAsync(image, () => Task.FromResult<string?>(picked), Measure800x400));

        StringAssert.StartsWith(image.FilePath, System.IO.Path.Combine(dirs.Library, "Media", "Images"));
        Assert.IsTrue(System.IO.File.Exists(image.FilePath));
        Assert.AreEqual("Undo Change Image", history.Stack.UndoMenuText);
        history.Stack.Undo();
        Assert.AreEqual(InLib("old.png"), image.FilePath);
        Assert.IsFalse(history.Stack.CanUndo, "picture and size were one step");
    });

    [TestMethod]
    public Task ReplaceImage_MediaLibraryNotConfigured_ChangesNothing() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var picked = dirs.OutsideFile("new.png");
        var editor = EditorOnEmptySlide(out var slide);
        var history = GiveHistory(editor, slide);
        editor.MediaLibraryPathProvider = () => null;
        var shown = 0;
        editor.ShowMediaLibraryError = (_, _) => shown++;
        var image = new ImageElement { FilePath = InLib("old.png") };
        slide.SlideElements.Add(image);
        history.Stack.Clear();

        await Pumped(editor.ReplaceImageAsync(image, () => Task.FromResult<string?>(picked), Measure800x400));

        Assert.AreEqual(InLib("old.png"), image.FilePath);
        Assert.IsFalse(history.Stack.CanUndo);
        Assert.AreEqual(1, shown);
    });

    [TestMethod]
    public Task BackgroundGraphic_PickedFile_IsCopiedIntoTheMediaLibrary() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var picked = dirs.OutsideFile("bg.png");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;

        await Pumped(editor.ChangeBackgroundGraphicAsync(() => Task.FromResult<string?>(picked)));

        StringAssert.StartsWith(slide.BackgroundGraphicFilePath, System.IO.Path.Combine(dirs.Library, "Media", "Images"));
        Assert.IsTrue(System.IO.File.Exists(slide.BackgroundGraphicFilePath));
    });

    [TestMethod]
    public Task AddImage_CancellingThePicker_AddsNothing() => DispatcherTestThread.Run(async () =>
    {
        var editor = EditorOnEmptySlide(out var slide);
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();

        await editor.AddImageAsync(() => Task.FromResult<string?>(null), _ => Task.FromResult<PixelSize?>(new PixelSize(10, 10)));

        Assert.AreEqual(1, slide.SlideElements.Count, "no empty image placeholder is left behind");
        Assert.IsFalse(history.Stack.CanUndo, "and no undo step");
    });

    [TestMethod]
    public Task AddImage_FileThatCannotBeRead_AddsNothing() => DispatcherTestThread.Run(async () =>
    {
        var editor = EditorOnEmptySlide(out var slide);

        await editor.AddImageAsync(() => Task.FromResult<string?>(InLib("broken.png")), _ => Task.FromResult<PixelSize?>(null));

        Assert.AreEqual(1, slide.SlideElements.Count);
    });

    [TestMethod]
    public Task AddImage_EditorMovesToAnotherSlideWhilePickerIsOpen_AddsNothingToEither() => DispatcherTestThread.Run(async () =>
    {
        var editor = EditorOnEmptySlide(out var slide);
        var other = new CustomSlide();

        await editor.AddImageAsync(
            () =>
            {
                editor.DataContext = other; // e.g. the media group editor rebuilt the editor for another slide
                return Task.FromResult<string?>(InLib("a.png"));
            },
            _ => Task.FromResult<PixelSize?>(new PixelSize(100, 100)));

        Assert.AreEqual(1, slide.SlideElements.Count);
        Assert.AreEqual(0, other.SlideElements.Count);
    });

    [TestMethod]
    public Task AddImage_PickedFile_AddsAFittedCentredFrontmostSelectedElement() => DispatcherTestThread.Run(async () =>
    {
        var editor = EditorOnEmptySlide(out var slide);
        var layers = Named<ListBox>(editor, "ListBox");

        await editor.AddImageAsync(() => Task.FromResult<string?>(InLib("a.png")), _ => Task.FromResult<PixelSize?>(new PixelSize(800, 400)));

        var image = slide.SlideElements[0] as ImageElement;
        Assert.IsNotNull(image, "the new picture is frontmost");
        Assert.AreEqual(InLib("a.png"), image.FilePath);
        Assert.AreEqual(800, image.Width, "sized to the picture, not the 100x100 starting box");
        Assert.AreEqual(400, image.Height);
        Assert.AreEqual((1920 - 800) / 2, image.X, "centred on the slide");
        Assert.AreEqual((1080 - 400) / 2, image.Y);
        Assert.AreSame(image, layers.SelectedItem, "and selected");
    });

    [TestMethod]
    public Task AddImage_PictureLargerThanTheSlide_IsScaledDownToFit() => DispatcherTestThread.Run(async () =>
    {
        var editor = EditorOnEmptySlide(out var slide);

        await editor.AddImageAsync(() => Task.FromResult<string?>(InLib("big.png")), _ => Task.FromResult<PixelSize?>(new PixelSize(3840, 2160)));

        var image = (ImageElement)slide.SlideElements[0];
        Assert.AreEqual(1920, image.Width);
        Assert.AreEqual(1080, image.Height);
        Assert.AreEqual(0, image.X);
        Assert.AreEqual(0, image.Y);
    });

    [TestMethod]
    public Task AddImage_IsOneUndoStep() => DispatcherTestThread.Run(async () =>
    {
        var editor = EditorOnEmptySlide(out var slide);
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();

        await editor.AddImageAsync(() => Task.FromResult<string?>(InLib("a.png")), _ => Task.FromResult<PixelSize?>(new PixelSize(800, 400)));

        Assert.AreEqual("Undo Add Image", history.Stack.UndoMenuText);
        history.Stack.Undo();
        Assert.AreEqual(1, slide.SlideElements.Count, "one Ctrl+Z removes the picture again");
        Assert.IsFalse(history.Stack.CanUndo, "the file, the size and the position were one step");

        history.Stack.Redo();
        var image = (ImageElement)slide.SlideElements[0];
        Assert.AreEqual(InLib("a.png"), image.FilePath);
        Assert.AreEqual(800, image.Width);
    });

    [TestMethod]
    public Task ToolbarSelectedElement_FollowsLayersSelection() => DispatcherTestThread.Run(() =>
    {
        var (editor, slide, text, image) = CreateEditor();
        var layers = Named<ListBox>(editor, "ListBox");
        var toolbar = Named<CustomSlideToolbar>(editor, "Toolbar");

        Assert.AreSame(slide, toolbar.Slide, "the toolbar edits the editor's slide");
        Assert.IsNull(toolbar.SelectedElement);

        layers.SelectedItem = text;
        Assert.AreSame(text, toolbar.SelectedElement);

        layers.SelectedItem = image;
        Assert.AreSame(image, toolbar.SelectedElement);

        layers.SelectedItem = null;
        Assert.IsNull(toolbar.SelectedElement);

        // a new slide reaches the toolbar too
        var other = new CustomSlide();
        editor.DataContext = other;
        Assert.AreSame(other, toolbar.Slide);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ToolbarDelete_RemovesElementThroughCommands() => DispatcherTestThread.Run(() =>
    {
        var (editor, slide, text, image) = CreateEditor();
        var layers = Named<ListBox>(editor, "ListBox");
        var toolbar = Named<CustomSlideToolbar>(editor, "Toolbar");

        layers.SelectedItem = text;
        Named<Button>(toolbar, "DeleteButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        CollectionAssert.AreEqual(new SlideElement[] { image }, slide.SlideElements.ToList());
        Assert.IsNull(toolbar.SelectedElement);

        // Add Text selects the new element
        Named<Button>(toolbar, "AddTextButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(2, slide.SlideElements.Count);
        var added = slide.SlideElements.OfType<TextElement>().Single();
        Assert.AreSame(added, layers.SelectedItem, "the new text box becomes the selection");
        Assert.AreSame(added, toolbar.SelectedElement);

        // Duplicate selects the copy
        Named<Button>(toolbar, "DuplicateButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(3, slide.SlideElements.Count);
        Assert.IsNotNull(layers.SelectedItem);
        Assert.AreNotSame(added, layers.SelectedItem, "the copy becomes the selection");
        return Task.CompletedTask;
    });

    private static (CustomSlideEditor Editor, CustomSlide Slide, ListBox Layers, CustomSlideToolbar Toolbar, SlideElement[] Elements) CreateEditorWithThree()
    {
        AvaloniaTextRendering.EnsureInitialized();
        var a = new TextElement { Text = "a" };
        var b = new TextElement { Text = "b" };
        var c = new ImageElement();
        var slide = new CustomSlide();
        slide.SlideElements.Add(a);
        slide.SlideElements.Add(b);
        slide.SlideElements.Add(c);
        var editor = new CustomSlideEditor { DataContext = slide };
        return (editor, slide, Named<ListBox>(editor, "ListBox"), Named<CustomSlideToolbar>(editor, "Toolbar"), new SlideElement[] { a, b, c });
    }

    private static void Click(CustomSlideToolbar toolbar, string button)
        => Named<Button>(toolbar, button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void AssertStillSelected(SlideElement expected, ListBox layers, CustomSlideToolbar toolbar, string step)
    {
        Assert.AreSame(expected, layers.SelectedItem, $"{step}: layers selection");
        Assert.AreSame(expected, toolbar.SelectedElement, $"{step}: toolbar selection");
    }

    [TestMethod]
    public Task ToolbarBringForwardAndSendBackward_KeepElementSelected() => DispatcherTestThread.Run(() =>
    {
        var (_, slide, layers, toolbar, els) = CreateEditorWithThree();
        var c = els[2];
        layers.SelectedItem = c;

        Click(toolbar, "BringForwardButton");
        Assert.AreEqual(1, slide.SlideElements.IndexOf(c));
        AssertStillSelected(c, layers, toolbar, "first bring forward");

        Click(toolbar, "BringForwardButton"); // still works: the toolbar didn't fall back to the slide section
        Assert.AreEqual(0, slide.SlideElements.IndexOf(c));
        AssertStillSelected(c, layers, toolbar, "second bring forward");

        Click(toolbar, "SendBackwardButton");
        Assert.AreEqual(1, slide.SlideElements.IndexOf(c));
        AssertStillSelected(c, layers, toolbar, "send backward");
        return Task.CompletedTask;
    });

    private static void InvokeHandler(CustomSlideEditor editor, string handler, SlideElement element)
    {
        var method = typeof(CustomSlideEditor).GetMethod(handler,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(method, handler);
        method.Invoke(editor, new object?[] { new MenuItem { DataContext = element }, new RoutedEventArgs() });
    }

    [TestMethod]
    public Task ContextMenuMoveUpAndDown_KeepElementSelected() => DispatcherTestThread.Run(() =>
    {
        var (editor, slide, layers, toolbar, els) = CreateEditorWithThree();
        var c = els[2];
        layers.SelectedItem = c;

        InvokeHandler(editor, "MoveItemUp_OnClick", c);
        Assert.AreEqual(1, slide.SlideElements.IndexOf(c));
        AssertStillSelected(c, layers, toolbar, "move up");

        InvokeHandler(editor, "MoveItemUp_OnClick", c);
        Assert.AreEqual(0, slide.SlideElements.IndexOf(c));
        AssertStillSelected(c, layers, toolbar, "second move up");

        InvokeHandler(editor, "MoveItemDown_OnClick", c);
        Assert.AreEqual(1, slide.SlideElements.IndexOf(c));
        AssertStillSelected(c, layers, toolbar, "move down");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ZoomPercent_ReflectsZoomBorder_AndZoomIn_Increases() => DispatcherTestThread.Run(() =>
    {
        AvaloniaTextRendering.EnsureInitialized();
        var designer = new CustomSlideDesigner { DataContext = new CustomSlide() };
        AvaloniaTextRendering.Layout(designer, 800, 450);

        var zoomBorder = Named<Avalonia.Controls.PanAndZoom.ZoomBorder>(designer, "ZoomBorder");
        Assert.AreEqual(zoomBorder.ZoomX * 100, designer.ZoomPercent, 0.0001);

        var changed = 0;
        designer.ZoomChanged += (_, _) => changed++;
        var before = designer.ZoomPercent;
        designer.ZoomIn();
        Assert.IsTrue(designer.ZoomPercent > before, $"zoom in: {before} -> {designer.ZoomPercent}");
        Assert.AreEqual(zoomBorder.ZoomX * 100, designer.ZoomPercent, 0.0001);
        Assert.IsTrue(changed > 0, "ZoomChanged was raised");

        var zoomedIn = designer.ZoomPercent;
        designer.ZoomOut();
        Assert.IsTrue(designer.ZoomPercent < zoomedIn);

        // the zoom bar moved out of the designer into the editor's status bar
        Assert.AreEqual(0, designer.GetLogicalDescendants().OfType<Slider>().Count());
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task PropertiesPanel_NoLongerContainsFontOrAlignmentControls() => DispatcherTestThread.Run(() =>
    {
        var (editor, _, text, image) = CreateEditor();
        var properties = Named<ContentControl>(editor, "SelectedElementProperties");
        var template = properties.ContentTemplate;
        Assert.IsNotNull(template);

        // The selection is shown here (set from code, not through a stale element-name binding).
        Named<ListBox>(editor, "ListBox").SelectedItem = text;
        Assert.AreSame(text, properties.Content);

        var textPanel = template.Build(text);
        Assert.IsNotNull(textPanel, "text element template");
        var controls = new[] { textPanel }.Concat(textPanel.GetLogicalDescendants().OfType<Control>()).ToList();

        Assert.IsTrue(controls.OfType<TextBox>().Any(), "the Text box stays");
        Assert.AreEqual(0, controls.OfType<NumericSliderBox>().Count(), "font size and line height moved to the toolbar");
        Assert.AreEqual(0, controls.OfType<ComboBox>().Count(), "font box moved to the toolbar");
        Assert.AreEqual(0, controls.OfType<ToggleButton>().Count(), "bold / alignment toggles moved to the toolbar");
        Assert.AreEqual(0, controls.OfType<ColorPicker>().Count(), "colours moved to the toolbar");
        Assert.AreEqual(0, controls.OfType<UniformGrid>().Count(), "the copy-pasted toggle groups are gone");

        var imagePanel = template.Build(image);
        Assert.IsNotNull(imagePanel, "image element template");
        var imageControls = new[] { imagePanel }.Concat(imagePanel.GetLogicalDescendants().OfType<Control>()).ToList();
        Assert.IsTrue(imageControls.OfType<TextBox>().Any(), "image path box stays");

        // X / Y / Width / Height: one shared panel for any element, following the selection
        var geometry = Named<Control>(editor, "GeometryPanel");
        Assert.AreEqual(4, geometry.GetLogicalDescendants().OfType<NumericSliderBox>().Count());
        Assert.AreSame(text, geometry.DataContext);
        Assert.IsTrue(geometry.IsVisible);
        Named<ListBox>(editor, "ListBox").SelectedItem = image;
        Assert.AreSame(image, geometry.DataContext);
        Named<ListBox>(editor, "ListBox").SelectedItem = null;
        Assert.IsFalse(geometry.IsVisible);
        Assert.IsNull(properties.Content);

        // the whole properties panel holds no font / alignment controls (only the slide background colour picker)
        var panel = Named<Control>(editor, "PropertiesPanel");
        var all = panel.GetLogicalDescendants().OfType<Control>().ToList();
        Assert.AreEqual(0, all.OfType<ComboBox>().Count());
        Assert.AreEqual(0, all.OfType<ToggleButton>().Count());
        Assert.AreEqual(1, all.OfType<ColorPicker>().Count(), "only the slide background colour");
        return Task.CompletedTask;
    });
}
