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
using static HandsLiftedApp.Tests.TestSupport.AsyncPump;

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

    // Every async editor action is awaited through AsyncPump.Pumped (TestSupport): the editor copies pictures on a
    // pool thread and resumes on the test's thread, which needs pumping.

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

        await Pumped(editor.AddImageAsync(() => Task.FromResult<string?>(null), _ => Task.FromResult<PixelSize?>(new PixelSize(10, 10))));

        Assert.AreEqual(1, slide.SlideElements.Count, "no empty image placeholder is left behind");
        Assert.IsFalse(history.Stack.CanUndo, "and no undo step");
    });

    [TestMethod]
    public Task AddImage_FileThatCannotBeRead_AddsNothing() => DispatcherTestThread.Run(async () =>
    {
        var editor = EditorOnEmptySlide(out var slide);

        await Pumped(editor.AddImageAsync(() => Task.FromResult<string?>(InLib("broken.png")), _ => Task.FromResult<PixelSize?>(null)));

        Assert.AreEqual(1, slide.SlideElements.Count);
    });

    [TestMethod]
    public Task AddImage_EditorMovesToAnotherSlideWhilePickerIsOpen_AddsNothingToEither() => DispatcherTestThread.Run(async () =>
    {
        var editor = EditorOnEmptySlide(out var slide);
        var other = new CustomSlide();

        await Pumped(editor.AddImageAsync(
            () =>
            {
                editor.DataContext = other; // e.g. the media group editor rebuilt the editor for another slide
                return Task.FromResult<string?>(InLib("a.png"));
            },
            _ => Task.FromResult<PixelSize?>(new PixelSize(100, 100))));

        Assert.AreEqual(1, slide.SlideElements.Count);
        Assert.AreEqual(0, other.SlideElements.Count);
    });

    [TestMethod]
    public Task AddImage_PickedFile_AddsAFittedCentredFrontmostSelectedElement() => DispatcherTestThread.Run(async () =>
    {
        var editor = EditorOnEmptySlide(out var slide);
        var layers = Named<ListBox>(editor, "ListBox");

        await Pumped(editor.AddImageAsync(() => Task.FromResult<string?>(InLib("a.png")), _ => Task.FromResult<PixelSize?>(new PixelSize(800, 400))));

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

        await Pumped(editor.AddImageAsync(() => Task.FromResult<string?>(InLib("big.png")), _ => Task.FromResult<PixelSize?>(new PixelSize(3840, 2160))));

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

        await Pumped(editor.AddImageAsync(() => Task.FromResult<string?>(InLib("a.png")), _ => Task.FromResult<PixelSize?>(new PixelSize(800, 400))));

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

    // ---- dropped / pasted image files: Media/Pasted, one undo step, fanned out from the drop point ----

    private static string PastedDir(TempFolders dirs) => System.IO.Path.Combine(dirs.Library, "Media", "Pasted");

    [TestMethod]
    public Task DroppedFiles_TwoImages_AreAddedAsOneStep_FannedOutFromThePoint_LastSelected() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var a = dirs.OutsideFile("a.png");
        var b = dirs.OutsideFile("b.png");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();
        var layers = Named<ListBox>(editor, "ListBox");

        await Pumped(editor.AddImagesFromFilesAsync(new[] { a, b }, new Point(600, 400), Measure800x400));

        Assert.AreEqual(3, slide.SlideElements.Count);
        var second = (ImageElement)slide.SlideElements[0]; // frontmost = added last
        var first = (ImageElement)slide.SlideElements[1];
        StringAssert.StartsWith(first.FilePath, PastedDir(dirs));
        StringAssert.StartsWith(second.FilePath, PastedDir(dirs));
        Assert.AreEqual("a.png", System.IO.Path.GetFileName(first.FilePath));
        Assert.AreEqual("b.png", System.IO.Path.GetFileName(second.FilePath));
        Assert.IsTrue(System.IO.File.Exists(first.FilePath) && System.IO.File.Exists(second.FilePath));
        Assert.IsTrue(System.IO.File.Exists(a), "originals untouched");

        Assert.AreEqual(800, first.Width);
        Assert.AreEqual(400, first.Height);
        Assert.AreEqual(600 - 400, first.X, "first centred on the drop point");
        Assert.AreEqual(400 - 200, first.Y);
        Assert.AreEqual(first.X + 40, second.X, "each further one 40 px right");
        Assert.AreEqual(first.Y + 40, second.Y, "and 40 px down");
        Assert.AreSame(second, layers.SelectedItem, "the last added is selected");

        Assert.AreEqual("Undo Add Images", history.Stack.UndoMenuText);
        history.Stack.Undo();
        Assert.AreEqual(1, slide.SlideElements.Count, "one Ctrl+Z removes both");
        Assert.IsFalse(history.Stack.CanUndo);
    });

    [TestMethod]
    public Task DroppedFiles_OneImage_IsAnAddImageStep_CentredOnTheSlideWithoutAPoint() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var a = dirs.OutsideFile("a.png");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();

        await Pumped(editor.AddImagesFromFilesAsync(new[] { a }, null, Measure800x400));

        var image = (ImageElement)slide.SlideElements[0];
        Assert.AreEqual((1920 - 800) / 2, image.X);
        Assert.AreEqual((1080 - 400) / 2, image.Y);
        Assert.AreEqual("Undo Add Image", history.Stack.UndoMenuText);
    });

    [TestMethod]
    public Task DroppedFiles_NonImagesAreFilteredOut() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var a = dirs.OutsideFile("a.PNG");
        var video = System.IO.Path.Combine(dirs.Outside, "clip.mp4");
        System.IO.File.WriteAllText(video, "x");
        var notes = System.IO.Path.Combine(dirs.Outside, "notes.txt");
        System.IO.File.WriteAllText(notes, "x");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        var measured = new System.Collections.Generic.List<string>();

        await Pumped(editor.AddImagesFromFilesAsync(new[] { video, a, notes }, null, p =>
        {
            measured.Add(p);
            return Task.FromResult<PixelSize?>(new PixelSize(100, 100));
        }));

        Assert.AreEqual(2, slide.SlideElements.Count, "only the picture was added");
        Assert.AreEqual(1, measured.Count);
        Assert.IsFalse(System.IO.Directory.Exists(System.IO.Path.Combine(dirs.Library, "Media", "Video")), "the video was not copied");
    });

    [TestMethod]
    public Task DroppedFiles_UnreadableOneIsSkipped_OthersAreAdded() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var good = dirs.OutsideFile("good.png");
        var broken = dirs.OutsideFile("broken.png");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();

        await Pumped(editor.AddImagesFromFilesAsync(new[] { broken, good }, new Point(500, 500),
            p => Task.FromResult<PixelSize?>(p.Contains("broken") ? null : new PixelSize(200, 100))));

        Assert.AreEqual(2, slide.SlideElements.Count);
        var image = (ImageElement)slide.SlideElements[0];
        Assert.AreEqual("good.png", System.IO.Path.GetFileName(image.FilePath));
        Assert.AreEqual(500 - 100, image.X, "the first image that made it is centred on the point");
        Assert.AreEqual("Undo Add Image", history.Stack.UndoMenuText);
    });

    [TestMethod]
    public Task DroppedFiles_NothingQualifies_AddsNothing_AndNoUndoStep() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var notes = System.IO.Path.Combine(dirs.Outside, "notes.txt");
        System.IO.File.WriteAllText(notes, "x");
        var broken = dirs.OutsideFile("broken.png");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();

        await Pumped(editor.AddImagesFromFilesAsync(System.Array.Empty<string>(), null, Measure800x400));
        await Pumped(editor.AddImagesFromFilesAsync(new[] { notes }, null, Measure800x400));
        await Pumped(editor.AddImagesFromFilesAsync(new[] { broken }, null, _ => Task.FromResult<PixelSize?>(null)));

        Assert.AreEqual(1, slide.SlideElements.Count);
        Assert.IsFalse(history.Stack.CanUndo);
    });

    [TestMethod]
    public Task DroppedFiles_MediaLibraryNotConfigured_AddsNothing_AndShowsTheErrorOnce() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var files = new[] { dirs.OutsideFile("a.png"), dirs.OutsideFile("b.png"), dirs.OutsideFile("c.png") };
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => null;
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();
        var errors = new System.Collections.Generic.List<string>();
        editor.ShowMediaLibraryError = (t, _) => errors.Add(t);

        await Pumped(editor.AddImagesFromFilesAsync(files, null, Measure800x400));

        Assert.AreEqual(1, slide.SlideElements.Count);
        Assert.IsFalse(history.Stack.CanUndo);
        CollectionAssert.AreEqual(new[] { "Media Library not configured" }, errors, "one message, not one per file");
    });

    [TestMethod]
    public Task DroppedFiles_SlideSwitchedWhileCopying_AddsNothingToEither() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var a = dirs.OutsideFile("a.png");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        var other = new CustomSlide();

        await Pumped(editor.AddImagesFromFilesAsync(new[] { a }, null, _ =>
        {
            editor.DataContext = other; // e.g. the media group editor rebuilt the editor for another slide
            return Task.FromResult<PixelSize?>(new PixelSize(100, 100));
        }));

        Assert.AreEqual(1, slide.SlideElements.Count);
        Assert.AreEqual(0, other.SlideElements.Count);
    });

    [TestMethod]
    public Task DroppedFiles_NearTheSlideEdge_StayInsideTheSlide() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;

        await Pumped(editor.AddImagesFromFilesAsync(new[] { dirs.OutsideFile("a.png"), dirs.OutsideFile("b.png") },
            new Point(1910, 1075), Measure800x400));

        foreach (var image in slide.SlideElements.OfType<ImageElement>())
        {
            Assert.IsTrue(image.X >= 0 && image.X + image.Width <= 1920, $"x {image.X}");
            Assert.IsTrue(image.Y >= 0 && image.Y + image.Height <= 1080, $"y {image.Y}");
        }
        Assert.AreEqual(2, slide.SlideElements.OfType<ImageElement>().Count());
    });

    [TestMethod]
    public Task Paste_ClipboardWithImageFiles_AddsThemIntoMediaPasted() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var a = dirs.OutsideFile("copied.png");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        editor.ReadClipboardFilePaths = () => Task.FromResult<System.Collections.Generic.IReadOnlyList<string>?>(new[] { a });
        editor.MeasureImage = _ => Task.FromResult<PixelSize?>(new PixelSize(800, 400));

        await Pumped(editor.PasteAsync());

        var image = (ImageElement)slide.SlideElements[0];
        StringAssert.StartsWith(image.FilePath, PastedDir(dirs));
        Assert.AreEqual((1920 - 800) / 2, image.X, "a paste lands in the middle of the slide");
    });

    [TestMethod]
    public Task Paste_ClipboardWithoutImageFiles_DoesNothing() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var notes = System.IO.Path.Combine(dirs.Outside, "notes.txt");
        System.IO.File.WriteAllText(notes, "x");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();
        editor.MeasureImage = _ => Task.FromResult<PixelSize?>(new PixelSize(800, 400));

        editor.ReadClipboardFilePaths = () => Task.FromResult<System.Collections.Generic.IReadOnlyList<string>?>(null);
        await Pumped(editor.PasteAsync());
        editor.ReadClipboardFilePaths = () => Task.FromResult<System.Collections.Generic.IReadOnlyList<string>?>(System.Array.Empty<string>());
        await Pumped(editor.PasteAsync());
        editor.ReadClipboardFilePaths = () => Task.FromResult<System.Collections.Generic.IReadOnlyList<string>?>(new[] { notes });
        await Pumped(editor.PasteAsync());

        Assert.AreEqual(1, slide.SlideElements.Count);
        Assert.IsFalse(history.Stack.CanUndo);
    });

    // ---- Ctrl+V of a raw picture (screenshot / "Copy image"): saved as a PNG in Media/Pasted ----
    // Not covered headlessly: the real clipboard read (ReadClipboardPictureFromSystem: TryGetBitmapAsync + PNG encode).

    private static readonly byte[] PictureBytes = { 0x89, 0x50, 0x4E, 0x47, 9, 8, 7, 6 };

    private static System.Func<Task<ClipboardPicture?>> Picture(byte[]? bytes = null) =>
        () => Task.FromResult<ClipboardPicture?>(new ClipboardPicture(bytes ?? PictureBytes, new PixelSize(800, 400)));

    [TestMethod]
    public Task Paste_ClipboardPicture_AddsOneFittedCentredSelectedElement_SavedUnderMediaPasted() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        editor.ReadClipboardFilePaths = () => Task.FromResult<System.Collections.Generic.IReadOnlyList<string>?>(null);
        editor.ReadClipboardPicture = Picture();
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();
        var layers = Named<ListBox>(editor, "ListBox");

        await Pumped(editor.PasteAsync());

        Assert.AreEqual(2, slide.SlideElements.Count);
        var image = (ImageElement)slide.SlideElements[0];
        StringAssert.StartsWith(image.FilePath, PastedDir(dirs));
        StringAssert.Matches(System.IO.Path.GetFileName(image.FilePath), new System.Text.RegularExpressions.Regex(@"^Pasted \d{4}-\d\d-\d\d \d\d-\d\d-\d\d\.png$"));
        CollectionAssert.AreEqual(PictureBytes, System.IO.File.ReadAllBytes(image.FilePath));
        Assert.AreEqual(800, image.Width);
        Assert.AreEqual(400, image.Height);
        Assert.AreEqual((1920 - 800) / 2, image.X);
        Assert.AreEqual((1080 - 400) / 2, image.Y);
        Assert.AreSame(image, layers.SelectedItem);

        Assert.AreEqual("Undo Add Image", history.Stack.UndoMenuText);
        history.Stack.Undo();
        Assert.AreEqual(1, slide.SlideElements.Count);
        Assert.IsFalse(history.Stack.CanUndo, "one step");
        Assert.IsTrue(System.IO.File.Exists(image.FilePath), "undo removes the element, the file stays");
    });

    [TestMethod]
    public Task Paste_FilesAndPicture_FilesWin_PictureIsNotRead() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var a = dirs.OutsideFile("copied.png");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        editor.MeasureImage = _ => Task.FromResult<PixelSize?>(new PixelSize(800, 400));
        editor.ReadClipboardFilePaths = () => Task.FromResult<System.Collections.Generic.IReadOnlyList<string>?>(new[] { a });
        editor.ReadClipboardPicture = () => throw new System.InvalidOperationException("picture must not be read");

        await Pumped(editor.PasteAsync());

        Assert.AreEqual("copied.png", System.IO.Path.GetFileName(((ImageElement)slide.SlideElements[0]).FilePath));
    });

    [TestMethod]
    public Task Paste_NeitherFilesNorPicture_DoesNothing() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        editor.ReadClipboardFilePaths = () => Task.FromResult<System.Collections.Generic.IReadOnlyList<string>?>(null);
        editor.ReadClipboardPicture = () => Task.FromResult<ClipboardPicture?>(null);
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();
        var errors = new System.Collections.Generic.List<string>();
        editor.ShowMediaLibraryError = (t, _) => errors.Add(t);

        await Pumped(editor.PasteAsync());

        Assert.AreEqual(1, slide.SlideElements.Count);
        Assert.IsFalse(history.Stack.CanUndo);
        Assert.AreEqual(0, errors.Count);
        Assert.IsFalse(System.IO.Directory.Exists(PastedDir(dirs)));
    });

    [TestMethod]
    public Task Paste_PictureWithoutMediaLibrary_AddsNothing_WritesNothing_ShowsErrorOnce() => DispatcherTestThread.Run(async () =>
    {
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => null;
        editor.ReadClipboardFilePaths = () => Task.FromResult<System.Collections.Generic.IReadOnlyList<string>?>(null);
        editor.ReadClipboardPicture = Picture();
        var history = GiveHistory(editor, slide);
        history.Stack.Clear();
        var errors = new System.Collections.Generic.List<string>();
        editor.ShowMediaLibraryError = (t, _) => errors.Add(t);

        await Pumped(editor.PasteAsync());

        Assert.AreEqual(1, slide.SlideElements.Count);
        Assert.IsFalse(history.Stack.CanUndo);
        CollectionAssert.AreEqual(new[] { "Media Library not configured" }, errors);
    });

    [TestMethod]
    public Task CtrlVOnTheCanvas_And_ADrop_ReachTheEditor() => DispatcherTestThread.Run(async () =>
    {
        using var dirs = new TempFolders();
        var copied = dirs.OutsideFile("copied.png");
        var dropped = dirs.OutsideFile("dropped.png");
        var editor = EditorOnEmptySlide(out var slide);
        editor.MediaLibraryPathProvider = () => dirs.Library;
        editor.MeasureImage = _ => Task.FromResult<PixelSize?>(new PixelSize(200, 100));
        editor.ReadClipboardFilePaths = () => Task.FromResult<System.Collections.Generic.IReadOnlyList<string>?>(new[] { copied });
        var designer = Named<CustomSlideDesigner>(editor, "VisualEditor");
        designer.DataContext = slide; // as in CtrlD_ThroughEditor: no window, so set it like the binding would

        Assert.IsTrue(designer.HandleKey(Avalonia.Input.Key.V, Avalonia.Input.KeyModifiers.Control));
        await Pumped(editor.PendingImageAction);
        Assert.AreEqual("copied.png", System.IO.Path.GetFileName(((ImageElement)slide.SlideElements[0]).FilePath));

        designer.OnFilesDropped(new[] { dropped }, new Point(300, 300));
        await Pumped(editor.PendingImageAction);
        var image = (ImageElement)slide.SlideElements[0];
        Assert.AreEqual("dropped.png", System.IO.Path.GetFileName(image.FilePath));
        Assert.AreEqual(300 - 100, image.X, "centred on the drop point");
        Assert.AreEqual(300 - 50, image.Y);
    });
}
