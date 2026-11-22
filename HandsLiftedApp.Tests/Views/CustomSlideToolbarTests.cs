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

    private const string NotInstalledFont = "Zz Not Installed Font 4711";

    /// <summary>What happens in the app when the font box appears: Loaded fills it with the system fonts.</summary>
    private static ComboBox LoadFontBox(CustomSlideToolbar toolbar)
    {
        var box = Named<ComboBox>(toolbar, "FontComboBox");
        box.RaiseEvent(new RoutedEventArgs(Control.LoadedEvent));
        return box;
    }

    [TestMethod]
    public Task FontNotInSystemList_SelectingElement_DoesNotChangeFont() => DispatcherTestThread.Run(() =>
    {
        AvaloniaTextRendering.EnsureInitialized();
        var a = new TextElement { Text = "a", FontFamilyAsText = NotInstalledFont };
        var b = new TextElement { Text = "b", FontFamilyAsText = "Another Missing Font 99" };
        var slide = new CustomSlide();
        slide.SlideElements.Add(a);
        slide.SlideElements.Add(b);

        var stack = new HandsLiftedApp.Core.Undo.UndoStack();
        using var recorder = new HandsLiftedApp.Core.Undo.CustomSlideEditRecorder(stack, slide);

        var toolbar = new CustomSlideToolbar { Slide = slide };
        var box = LoadFontBox(toolbar);

        toolbar.SelectedElement = a;
        toolbar.SelectedElement = b;
        toolbar.SelectedElement = null;
        toolbar.SelectedElement = a;
        LoadFontBox(toolbar); // loaded again (e.g. re-attached) while a is selected

        // and via an element whose font IS in the list (the box then holds a real selection to lose)
        var listed = box.Items.OfType<string>().First(f => f != NotInstalledFont && f != "Another Missing Font 99");
        var c = new TextElement { Text = "c", FontFamilyAsText = listed };
        slide.SlideElements.Add(c);
        stack.Clear();
        toolbar.SelectedElement = c;
        toolbar.SelectedElement = b;
        toolbar.SelectedElement = c;
        toolbar.SelectedElement = a;

        Assert.AreEqual(listed, c.FontFamilyAsText);
        Assert.AreEqual(NotInstalledFont, a.FontFamilyAsText);
        Assert.AreEqual("Another Missing Font 99", b.FontFamilyAsText);
        Assert.IsFalse(stack.CanUndo, "selecting elements must not record an undo step");
        Assert.AreEqual(NotInstalledFont, box.SelectedItem, "the box shows the element's font even when not installed");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task InstalledFontSelectedBeforeLoaded_EndsUpOnceInSortedPosition() => DispatcherTestThread.Run(() =>
    {
        AvaloniaTextRendering.EnsureInitialized();
        var installed = Avalonia.Media.FontManager.Current.SystemFonts.Select(f => f.Name).OrderBy(n => n).ToList();
        Assert.IsTrue(installed.Count > 1, "needs at least two installed fonts");
        var last = installed[^1]; // sorts last, so a stray copy at the top (index 0) is easy to tell apart

        // selected before the box's Loaded: the font is not in the (still empty) list yet, so it is shown as an extra
        var toolbar = CreateToolbar(new TextElement { FontFamilyAsText = last });
        var box = LoadFontBox(toolbar);

        var items = box.Items.OfType<string>().ToList();
        Assert.AreEqual(1, items.Count(i => i == last), "listed exactly once");
        Assert.AreEqual(installed.Count, items.Count, "the whole system list, no extra copy");
        CollectionAssert.AreEqual(installed, items, "in sorted position, not stuck at the top");
        Assert.AreEqual(last, box.SelectedItem);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task PickingListedFont_WritesItToElement() => DispatcherTestThread.Run(() =>
    {
        var text = new TextElement { FontFamilyAsText = NotInstalledFont };
        var toolbar = CreateToolbar(text);
        var box = LoadFontBox(toolbar);

        var listed = box.Items.OfType<string>().First(f => f != NotInstalledFont);
        box.SelectedItem = listed;

        Assert.AreEqual(listed, text.FontFamilyAsText);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ElementFontChangedElsewhere_BoxFollows() => DispatcherTestThread.Run(() =>
    {
        var text = new TextElement { FontFamilyAsText = "Arial" };
        var toolbar = CreateToolbar(text);
        var box = LoadFontBox(toolbar);

        text.FontFamilyAsText = NotInstalledFont; // e.g. undo writing the old font back

        Assert.AreEqual(NotInstalledFont, box.SelectedItem);
        Assert.AreEqual(NotInstalledFont, text.FontFamilyAsText);
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
