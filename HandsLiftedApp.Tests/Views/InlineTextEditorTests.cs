using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Core.Views.Editors.FreeText;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Views;

[TestClass]
public class InlineTextEditorTests
{
    private static (CustomSlideDesigner Designer, CustomSlide Slide, TextElement Text, InlineTextEditor Overlay) CreateDesigner()
    {
        AvaloniaTextRendering.EnsureInitialized();
        var text = new TextElement { Text = "Hello", X = 100, Y = 200, Width = 600, Height = 300 };
        var slide = new CustomSlide();
        slide.SlideElements.Add(text);
        var designer = new CustomSlideDesigner { DataContext = slide };
        AvaloniaTextRendering.Layout(designer, 800, 450);
        var overlay = designer.FindControl<InlineTextEditor>("InlineEditor");
        Assert.IsNotNull(overlay, "the designer hosts the inline text editor");
        return (designer, slide, text, overlay);
    }

    private static SolidColorBrush Solid(IBrush? brush)
    {
        Assert.IsInstanceOfType<SolidColorBrush>(brush);
        return (SolidColorBrush)brush;
    }

    [TestMethod]
    public Task ApplyStyle_MapsEveryTextProperty() => DispatcherTestThread.Run(() =>
    {
        AvaloniaTextRendering.EnsureInitialized();
        var el = new TextElement
        {
            FontFamilyAsText = "Arial",
            FontSize = 72,
            IsBold = true,
            Italic = true,
            Underline = true,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            LineHeightEm = 1.5M,
            ForegroundAvaloniaColour = Colors.Red,
            BackgroundAvaloniaColour = Color.FromRgb(0x10, 0x20, 0x30),
            Width = 640,
            Height = 200,
        };
        var box = new TextBox();

        InlineTextEditor.ApplyStyle(box, el);

        Assert.AreEqual("Arial", box.FontFamily.Name);
        Assert.AreEqual(72, box.FontSize);
        Assert.AreEqual(FontWeight.Bold, box.FontWeight);
        Assert.AreEqual(FontStyle.Italic, box.FontStyle);
        Assert.AreEqual(Colors.Red, Solid(box.Foreground).Color);
        Assert.AreEqual(Color.FromRgb(0x10, 0x20, 0x30), Solid(box.Background).Color);
        Assert.AreEqual(TextAlignment.Right, box.TextAlignment);
        Assert.AreEqual(VerticalAlignment.Bottom, box.VerticalContentAlignment);
        Assert.AreEqual(108, box.LineHeight, "line height = (int)(font size x em), as the Skia layout uses");
        // zero padding, except the half-leading correction at a tall line height (see the metric test below)
        Assert.AreEqual(0, box.Padding.Left);
        Assert.AreEqual(0, box.Padding.Right);
        Assert.IsTrue(box.Padding.Top <= 0 && box.Padding.Bottom == -box.Padding.Top, $"padding {box.Padding}");
        Assert.AreEqual(new Thickness(0), box.BorderThickness);
        Assert.AreEqual(TextWrapping.Wrap, box.TextWrapping);
        Assert.IsTrue(box.AcceptsReturn);
        Assert.AreEqual(640, box.Width);
        Assert.AreEqual(200, box.Height);

        // and back: plain text, the other alignments
        el.IsBold = false;
        el.Italic = false;
        el.Underline = false;
        el.TextAlignment = TextAlignment.Left;
        el.VerticalAlignment = VerticalAlignment.Top;
        el.LineHeightEm = 1.0M;
        InlineTextEditor.ApplyStyle(box, el);
        Assert.AreEqual(new Thickness(0), box.Padding, "default line height: no padding at all");
        Assert.AreEqual(FontWeight.Normal, box.FontWeight);
        Assert.AreEqual(FontStyle.Normal, box.FontStyle);
        Assert.AreEqual(TextAlignment.Left, box.TextAlignment);
        Assert.AreEqual(VerticalAlignment.Top, box.VerticalContentAlignment);

        el.VerticalAlignment = VerticalAlignment.Center;
        InlineTextEditor.ApplyStyle(box, el);
        Assert.AreEqual(VerticalAlignment.Center, box.VerticalContentAlignment);

        // the Skia layout puts Stretch text at the top
        el.VerticalAlignment = VerticalAlignment.Stretch;
        InlineTextEditor.ApplyStyle(box, el);
        Assert.AreEqual(VerticalAlignment.Top, box.VerticalContentAlignment);
        return Task.CompletedTask;
    });

    // Metric-level check that the box's text lands where the Skia layout draws it, for left/centre/right x
    // top/centre/bottom, at the default line height and at a taller one. It lays the text out with the box's own
    // properties and applies the box's padding and vertical content alignment the way the TextBox template does
    // (text area = box minus padding, text block aligned inside it). It does not render the real themed template.
    [TestMethod]
    public Task BoxText_LinesUpWithSkiaLayout_ForEveryAlignment() => DispatcherTestThread.Run(() =>
    {
        AvaloniaTextRendering.EnsureInitialized();
        foreach (var em in new[] { 1.0M, 1.5M })
        foreach (var h in new[] { TextAlignment.Left, TextAlignment.Center, TextAlignment.Right })
        foreach (var v in new[] { VerticalAlignment.Top, VerticalAlignment.Center, VerticalAlignment.Bottom })
        {
            var el = new TextElement
            {
                Text = "Hello world one two three", FontFamilyAsText = "Arial", FontSize = 90, LineHeightEm = em,
                X = 0, Y = 0, Width = 600, Height = 500, TextAlignment = h, VerticalAlignment = v,
            };
            // The Skia layout reads TextElement.LineHeight, which follows FontSize/LineHeightEm asynchronously.
            var expectedLineHeight = (int)(el.FontSize * el.LineHeightEm);
            var deadline = System.DateTime.UtcNow.AddSeconds(5);
            while (el.LineHeight != expectedLineHeight && System.DateTime.UtcNow < deadline)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                System.Threading.Thread.Sleep(1);
            }
            Assert.AreEqual(expectedLineHeight, el.LineHeight, "derived line height settled");

            var box = new TextBox();
            InlineTextEditor.ApplyStyle(box, el);
            var label = $"em {em}, {h} x {v}";
            Assert.AreEqual(0, box.Padding.Left, label);
            Assert.AreEqual(0, box.Padding.Right, label);
            Assert.AreEqual(0, box.Padding.Top + box.Padding.Bottom, 1e-9, $"{label}: padding keeps the box height");

            using var font = HandsLiftedApp.Core.Render.Skia.Builders.CustomSlideTextLayout.CreateFont(el);
            var skia = HandsLiftedApp.Core.Render.Skia.Builders.CustomSlideTextLayout.Layout(el, font);

            using var layout = new Avalonia.Media.TextFormatting.TextLayout(el.Text,
                new Typeface(box.FontFamily, box.FontStyle, box.FontWeight), box.FontSize, null, box.TextAlignment,
                box.TextWrapping, maxWidth: box.Width, lineHeight: box.LineHeight);
            Assert.AreEqual(skia.Count, layout.TextLines.Count, $"{label}: same line breaks");

            var areaHeight = box.Height - box.Padding.Top - box.Padding.Bottom;
            var y = box.Padding.Top + box.VerticalContentAlignment switch
            {
                VerticalAlignment.Center => (areaHeight - layout.Height) / 2,
                VerticalAlignment.Bottom => areaHeight - layout.Height,
                _ => 0,
            };
            for (var i = 0; i < skia.Count; i++)
            {
                var line = layout.TextLines[i];
                Assert.AreEqual(skia[i].Baseline, y + line.Baseline, 1.0, $"{label}: line {i} baseline");
                var softWrapped = i < skia.Count - 1;
                if (h == TextAlignment.Right && softWrapped)
                {
                    // Known, accepted drift: Avalonia right-aligns a soft-wrapped line including the space it broke
                    // at, so while typing such a line sits one space width left of where Skia draws it.
                    var space = font.MeasureText(" ");
                    Assert.AreEqual(skia[i].X - space, line.Start, 1.0, $"{label}: line {i} x (one space left)");
                }
                else
                {
                    Assert.AreEqual(skia[i].X, line.Start, 1.0, $"{label}: line {i} x");
                }
                y += line.Height;
            }
        }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Commit_WithUnchangedText_AddsNoUndoStep() => DispatcherTestThread.Run(() =>
    {
        var (designer, slide, text, overlay) = CreateDesigner();
        var stack = new UndoStack();
        using var recorder = new CustomSlideEditRecorder(stack, slide);

        designer.BeginEdit(text);
        Assert.IsTrue(designer.IsEditing);
        Assert.AreEqual("Hello", overlay.Text);

        designer.CommitEdit();

        Assert.IsFalse(designer.IsEditing);
        Assert.AreEqual("Hello", text.Text);
        Assert.IsFalse(stack.CanUndo, "nothing changed, so nothing to undo");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Commit_WithNewText_IsOneEditTextStep() => DispatcherTestThread.Run(() =>
    {
        var (designer, slide, text, overlay) = CreateDesigner();
        var stack = new UndoStack();
        using var recorder = new CustomSlideEditRecorder(stack, slide);

        designer.BeginEdit(text);
        // typing goes to the box only
        overlay.Text = "Hello w";
        overlay.Text = "Hello world";
        Assert.AreEqual("Hello", text.Text, "the model is untouched until commit");
        Assert.IsFalse(stack.CanUndo);

        designer.CommitEdit();

        Assert.AreEqual("Hello world", text.Text);
        Assert.AreEqual("Undo Edit Text", stack.UndoMenuText);
        stack.Undo();
        Assert.AreEqual("Hello", text.Text);
        Assert.IsFalse(stack.CanUndo, "the whole edit was one step");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Begin_HidesCanvasText_CommitRestoresIt() => DispatcherTestThread.Run(() =>
    {
        var (designer, _, text, overlay) = CreateDesigner();
        Assert.IsNull(designer.HiddenTextElement);
        Assert.IsFalse(overlay.IsVisible);

        designer.BeginEdit(text);

        Assert.AreSame(text, designer.HiddenTextElement, "the canvas does not draw the text under the box");
        Assert.IsTrue(overlay.IsVisible);
        Assert.IsTrue(overlay.IsEditing);
        Assert.AreEqual(100, Canvas.GetLeft(overlay));
        Assert.AreEqual(200, Canvas.GetTop(overlay));
        Assert.AreEqual(600, overlay.Width);
        Assert.AreEqual(300, overlay.Height);

        // the box sits above the hit-test proxies, inside the zoomed 1920x1080 slide
        var root = designer.FindControl<Canvas>("Root")!;
        var layer = (Control)overlay.Parent!;
        Assert.AreSame(root.Parent, layer.Parent, "same zoomed grid as the proxies");
        var grid = (Grid)root.Parent!;
        Assert.IsTrue(grid.Children.IndexOf(layer) > grid.Children.IndexOf(root), "above the proxies");

        designer.CommitEdit();

        Assert.IsNull(designer.HiddenTextElement);
        Assert.IsFalse(overlay.IsVisible);
        Assert.IsFalse(overlay.IsEditing);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task FormatChangeDuringEdit_ReappliesStyle() => DispatcherTestThread.Run(() =>
    {
        var (designer, _, text, overlay) = CreateDesigner();
        designer.BeginEdit(text);

        text.FontSize = 120;
        Assert.AreEqual(120, overlay.FontSize);
        text.Italic = true;
        Assert.AreEqual(FontStyle.Italic, overlay.FontStyle);
        text.ForegroundAvaloniaColour = Colors.Yellow;
        Assert.AreEqual(Colors.Yellow, Solid(overlay.Foreground).Color);
        text.Width = 500;
        Assert.AreEqual(500, overlay.Width);
        text.X = 33;
        Assert.AreEqual(33, Canvas.GetLeft(overlay));

        designer.CommitEdit();

        // no longer following the element
        text.FontSize = 40;
        Assert.AreEqual(120, overlay.FontSize);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task EscapeKey_Commits() => DispatcherTestThread.Run(() =>
    {
        var (designer, _, text, overlay) = CreateDesigner();
        designer.BeginEdit(text);
        overlay.Text = "Changed";

        var esc = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
        overlay.RaiseEvent(esc);

        Assert.IsTrue(esc.Handled, "Esc stops at the box (it must not also deselect)");
        Assert.IsFalse(designer.IsEditing);
        Assert.AreEqual("Changed", text.Text);
        Assert.IsNull(designer.HiddenTextElement);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Committed_ReportsElementAndText() => DispatcherTestThread.Run(() =>
    {
        AvaloniaTextRendering.EnsureInitialized();
        var el = new TextElement { Text = "a" };
        var box = new InlineTextEditor();
        TextElement? committedElement = null;
        string? committedText = null;
        box.Committed += (e, t) => { committedElement = e; committedText = t; };

        box.Begin(el, selectAll: true);
        Assert.IsTrue(box.IsEditing);
        Assert.AreEqual(0, box.SelectionStart);
        Assert.AreEqual(1, box.SelectionEnd);
        box.Text = "ab";
        box.Commit();

        Assert.AreSame(el, committedElement);
        Assert.AreEqual("ab", committedText);
        Assert.AreEqual("a", el.Text, "the box itself never writes the model");
        Assert.IsFalse(box.IsEditing);

        // committing again does nothing
        committedElement = null;
        box.Commit();
        Assert.IsNull(committedElement);
        return Task.CompletedTask;
    });

    // The test host has no TopLevel, so nothing can really be attached or detached; call the control's own
    // OnDetachedFromVisualTree override the way Avalonia does when the editor's window closes or the designer is
    // removed (the box always leaves the tree with the designer that hosts it).
    private static void SimulateDetachedFromVisualTree(Visual control)
    {
        const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        // Its constructor insists on a real presentation source (window), which this host cannot make; build the
        // args without it, with just the attachment point.
        var args = (VisualTreeAttachmentEventArgs)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(VisualTreeAttachmentEventArgs));
        foreach (var field in typeof(VisualTreeAttachmentEventArgs).GetFields(any).Where(f => f.FieldType == typeof(Visual)))
            field.SetValue(args, control);
        var method = control.GetType().GetMethod("OnDetachedFromVisualTree", any);
        Assert.IsNotNull(method);
        method.Invoke(control, new object[] { args });
    }

    [TestMethod]
    public Task LeavingVisualTreeWhileEditing_CommitsTypedTextAndStopsFollowingElement() => DispatcherTestThread.Run(() =>
    {
        var (designer, slide, text, overlay) = CreateDesigner();
        var stack = new UndoStack();
        using var recorder = new CustomSlideEditRecorder(stack, slide);
        designer.BeginEdit(text);
        overlay.Text = "typed before closing";

        SimulateDetachedFromVisualTree(overlay);

        Assert.IsFalse(designer.IsEditing);
        Assert.AreEqual("typed before closing", text.Text, "nothing typed is lost when the window closes");
        Assert.AreEqual("Undo Edit Text", stack.UndoMenuText);
        Assert.IsNull(designer.HiddenTextElement);

        // the element no longer holds on to the box (and through it the closed window)
        var fontSize = overlay.FontSize;
        text.FontSize = 33;
        Assert.AreEqual(fontSize, overlay.FontSize, "unsubscribed from the element");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Commit_WithEmptyText_KeepsEmptyElement_OneStep() => DispatcherTestThread.Run(() =>
    {
        var (designer, slide, text, overlay) = CreateDesigner();
        var stack = new UndoStack();
        using var recorder = new CustomSlideEditRecorder(stack, slide);
        designer.BeginEdit(text);
        overlay.Text = "";

        designer.CommitEdit();

        CollectionAssert.Contains(slide.SlideElements, text, "an emptied text box stays on the slide");
        Assert.AreEqual("", text.Text);
        Assert.AreEqual("Undo Edit Text", stack.UndoMenuText);
        stack.Undo();
        Assert.AreEqual("Hello", text.Text);
        Assert.IsFalse(stack.CanUndo, "exactly one step");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ElementRemovedWhileEditing_EndsEditWithoutWriting() => DispatcherTestThread.Run(() =>
    {
        var (designer, slide, text, overlay) = CreateDesigner();
        designer.BeginEdit(text);
        overlay.Text = "typed";

        slide.SlideElements.Remove(text);

        Assert.IsFalse(designer.IsEditing);
        Assert.IsFalse(overlay.IsVisible);
        Assert.IsNull(designer.HiddenTextElement);
        Assert.AreEqual("Hello", text.Text, "a removed element is not written to");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AddedTextElement_StartsEditingWithAllSelected() => DispatcherTestThread.Run(() =>
    {
        AvaloniaTextRendering.EnsureInitialized();
        var slide = new CustomSlide();
        var editor = new CustomSlideEditor { DataContext = slide };
        var toolbar = editor.FindControl<CustomSlideToolbar>("Toolbar")!;
        var designer = editor.FindControl<CustomSlideDesigner>("VisualEditor")!;
        var overlay = designer.FindControl<InlineTextEditor>("InlineEditor")!;
        // In the app the designer gets the slide through DataContext="{Binding}" inside a TabItem; the test host has
        // no theme, so the TabControl never presents its content and that binding never resolves. Give it directly.
        designer.DataContext = slide;

        toolbar.FindControl<Button>("AddTextButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var added = slide.SlideElements.OfType<TextElement>().Single();
        Assert.IsTrue(designer.IsEditing, "a new text box opens for typing");
        Assert.AreSame(added, designer.HiddenTextElement);
        Assert.AreEqual(added.Text, overlay.Text);
        Assert.IsFalse(string.IsNullOrEmpty(added.Text));
        Assert.AreEqual(0, overlay.SelectionStart, "placeholder text is selected, so typing replaces it");
        Assert.AreEqual(added.Text.Length, overlay.SelectionEnd);
        return Task.CompletedTask;
    });
}
