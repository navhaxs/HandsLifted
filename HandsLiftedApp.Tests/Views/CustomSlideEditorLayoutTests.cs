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
        return new CustomSlideEditor { DataContext = slide };
    }

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

        await editor.AddImageAsync(() => Task.FromResult<string?>("C:/pics/broken.png"), _ => Task.FromResult<PixelSize?>(null));

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
                return Task.FromResult<string?>("C:/pics/a.png");
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

        await editor.AddImageAsync(() => Task.FromResult<string?>("C:/pics/a.png"), _ => Task.FromResult<PixelSize?>(new PixelSize(800, 400)));

        var image = slide.SlideElements[0] as ImageElement;
        Assert.IsNotNull(image, "the new picture is frontmost");
        Assert.AreEqual("C:/pics/a.png", image.FilePath);
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

        await editor.AddImageAsync(() => Task.FromResult<string?>("C:/pics/big.png"), _ => Task.FromResult<PixelSize?>(new PixelSize(3840, 2160)));

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

        await editor.AddImageAsync(() => Task.FromResult<string?>("C:/pics/a.png"), _ => Task.FromResult<PixelSize?>(new PixelSize(800, 400)));

        Assert.AreEqual("Undo Add Image", history.Stack.UndoMenuText);
        history.Stack.Undo();
        Assert.AreEqual(1, slide.SlideElements.Count, "one Ctrl+Z removes the picture again");
        Assert.IsFalse(history.Stack.CanUndo, "the file, the size and the position were one step");

        history.Stack.Redo();
        var image = (ImageElement)slide.SlideElements[0];
        Assert.AreEqual("C:/pics/a.png", image.FilePath);
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
