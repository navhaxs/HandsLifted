using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Views;

[TestClass]
public class CustomSlideToolbarTests
{
    private static CustomSlideToolbar CreateToolbar(SlideElement? selected = null)
    {
        AvaloniaTextRendering.EnsureInitialized();
        var slide = new CustomSlide();
        if (selected != null) slide.SlideElements.Add(selected);
        return new CustomSlideToolbar { Slide = slide, SelectedElement = selected };
    }

    private static T Named<T>(CustomSlideToolbar toolbar, string name) where T : Control
    {
        var control = toolbar.FindControl<T>(name);
        Assert.IsNotNull(control, $"control '{name}' not found");
        return control;
    }

    /// <summary>True when the control and every ancestor up to the toolbar are visible.</summary>
    private static bool IsShown(CustomSlideToolbar toolbar, string name)
    {
        Control? c = Named<Control>(toolbar, name);
        while (c != null && c != toolbar)
        {
            if (!c.IsVisible) return false;
            c = c.Parent as Control;
        }
        return true;
    }

    [TestMethod]
    public Task TextSelected_ShowsTextSection_HidesImageSection() => DispatcherTestThread.Run(() =>
    {
        var text = new TextElement { Text = "Hi" };
        var toolbar = CreateToolbar(text);

        Assert.IsTrue(IsShown(toolbar, "TextSection"));
        Assert.IsFalse(IsShown(toolbar, "ImageSection"));
        Assert.IsFalse(IsShown(toolbar, "SlideSection"));
        Assert.IsTrue(IsShown(toolbar, "SelectionActions"));
        Assert.IsTrue(IsShown(toolbar, "BoldToggle"));
        Assert.AreSame(text, Named<Control>(toolbar, "TextSection").DataContext);

        // switching to nothing hides it again
        toolbar.SelectedElement = null;
        Assert.IsFalse(IsShown(toolbar, "TextSection"));
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task NothingSelected_ShowsSlideSection() => DispatcherTestThread.Run(() =>
    {
        var toolbar = CreateToolbar();

        Assert.IsTrue(IsShown(toolbar, "SlideSection"));
        Assert.IsFalse(IsShown(toolbar, "TextSection"));
        Assert.IsFalse(IsShown(toolbar, "ImageSection"));
        Assert.IsFalse(IsShown(toolbar, "SelectionActions"));
        Assert.AreSame(toolbar.Slide, Named<Control>(toolbar, "SlideSection").DataContext);

        // always-present actions
        Assert.IsTrue(IsShown(toolbar, "UndoButton"));
        Assert.IsTrue(IsShown(toolbar, "RedoButton"));
        Assert.IsTrue(IsShown(toolbar, "AddTextButton"));
        Assert.IsTrue(IsShown(toolbar, "AddImageButton"));
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ImageSelected_ShowsReplaceAndFit() => DispatcherTestThread.Run(() =>
    {
        var toolbar = CreateToolbar(new ImageElement());

        Assert.IsTrue(IsShown(toolbar, "ReplaceImageButton"));
        Assert.IsTrue(IsShown(toolbar, "FitImageButton"));
        Assert.IsTrue(IsShown(toolbar, "SelectionActions"));
        Assert.IsFalse(IsShown(toolbar, "TextSection"));
        Assert.IsFalse(IsShown(toolbar, "SlideSection"));
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AllButtons_AreNotFocusable() => DispatcherTestThread.Run(() =>
    {
        var toolbar = CreateToolbar(new TextElement());

        // No window here, so apply styles and templates by hand (the shared toggle style re-templates the
        // toggles; any button a template creates must be caught too).
        foreach (var styled in new[] { toolbar }.Concat(toolbar.GetLogicalDescendants().OfType<Control>()).ToList())
        {
            styled.ApplyStyling();
            if (styled is TemplatedControl tc) tc.ApplyTemplate();
        }

        var buttons = new HashSet<Button>();
        foreach (var b in toolbar.GetLogicalDescendants().OfType<Button>()) buttons.Add(b);
        foreach (var b in toolbar.GetVisualDescendants().OfType<Button>()) buttons.Add(b);
        foreach (var c in toolbar.GetLogicalDescendants().OfType<Control>().ToList())
        foreach (var b in c.GetVisualDescendants().OfType<Button>()) buttons.Add(b);

        // Button covers ToggleButton and RadioButton (both derive from it)
        Assert.IsTrue(buttons.OfType<ToggleButton>().Count() >= 10, $"expected the toggles, found {buttons.OfType<ToggleButton>().Count()}");
        Assert.IsTrue(buttons.Count >= 20, $"expected at least 20 buttons, found {buttons.Count}");
        var focusable = buttons.Where(b => b.Focusable).Select(b => $"{b.GetType().Name} '{b.Name}' ({ToolTip.GetTip(b)})").ToList();
        Assert.AreEqual(0, focusable.Count, "focusable buttons: " + string.Join(", ", focusable));
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task BoldToggle_WritesFontWeight() => DispatcherTestThread.Run(() =>
    {
        var text = new TextElement();
        var toolbar = CreateToolbar(text);
        var bold = Named<ToggleButton>(toolbar, "BoldToggle");

        Assert.AreNotEqual(true, bold.IsChecked);
        bold.IsChecked = true;
        Assert.AreEqual(FontWeight.Bold, text.FontWeight);

        bold.IsChecked = false;
        Assert.AreEqual(FontWeight.Normal, text.FontWeight);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task DeleteButton_RaisesDeleteRequested() => DispatcherTestThread.Run(() =>
    {
        var toolbar = CreateToolbar(new TextElement());
        var raised = 0;
        toolbar.DeleteRequested += (_, _) => raised++;

        Named<Button>(toolbar, "DeleteButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.AreEqual(1, raised);
        return Task.CompletedTask;
    });
}
