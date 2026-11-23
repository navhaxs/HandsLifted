using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Controls;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Designer;
using HandsLiftedApp.Core.Views.Editors.Song;
using HandsLiftedApp.Core.Views.ItemEditDock;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.UiTests.Support;

namespace HandsLiftedApp.UiTests;

[TestClass]
public class ThemePickerUiTests
{
    private static BaseSlideTheme MakeTheme(string name, SlideThemeType type) =>
        new() { Name = name, Type = type, FontSize = 80 };

    /// <summary>The thumbnails of <paramref name="list"/> that actually show (the type filters hide the other lists' items).</summary>
    private static List<ThemePreviewImage> VisibleThumbnails(ListBox list) =>
        list.GetVisualDescendants().OfType<ThemePreviewImage>().Where(t => t.IsEffectivelyVisible).ToList();

    [TestMethod]
    public async Task Designer_Lists_ShowThumbnailPerTheme()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var vm = Globals.Instance.MainViewModel;

            var general = MakeTheme("General theme", SlideThemeType.General);
            var song = MakeTheme("Song theme", SlideThemeType.SongTheme);
            var scripture = MakeTheme("Scripture theme", SlideThemeType.ScriptureTheme);
            vm.Playlist.Designs.Clear();
            vm.Playlist.Designs.Add(general);
            vm.Playlist.Designs.Add(song);
            vm.Playlist.Designs.Add(scripture);

            // The thumbnail sits above the name and the "default" labels; mark two themes default so the labels show.
            vm.Playlist.DefaultSongThemeId = song.Id;
            vm.Playlist.DefaultScriptureThemeId = scripture.Id;

            var designer = new SlideThemeDesigner { DataContext = vm };
            var window = new Window { Width = 1400, Height = 900, Content = designer };
            window.Show();
            UiTestHelpers.Settle();

            foreach (var (listName, theme) in new[]
                     {
                         ("generalDesignsListBox", general),
                         ("songDesignsListBox", song),
                         ("scriptureDesignsListBox", scripture),
                     })
            {
                var list = designer.Find<ListBox>(l => l.Name == listName)!;
                var thumbnails = VisibleThumbnails(list);

                Assert.AreEqual(1, thumbnails.Count, $"{listName}: expected exactly one visible thumbnail");
                Assert.AreSame(theme, thumbnails[0].SlideTheme, $"{listName}: thumbnail shows the wrong theme");
                Assert.IsNotNull(thumbnails[0].Source, $"{listName}: thumbnail has no bitmap");
                Assert.IsTrue(thumbnails[0].Bounds.Width > 0 && thumbnails[0].Bounds.Height > 0, $"{listName}: thumbnail has no size");
            }

            // The thumbnail must not displace the existing labels: they stay visible below it.
            foreach (var (listName, label) in new[]
                     {
                         ("songDesignsListBox", "default (songs)"),
                         ("scriptureDesignsListBox", "default (scripture)"),
                     })
            {
                var list = designer.Find<ListBox>(l => l.Name == listName)!;
                var labelBlock = list.FindByText<TextBlock>(label);
                var thumbnail = VisibleThumbnails(list)[0];
                Assert.IsNotNull(labelBlock, $"{listName}: '{label}' label is not visible");
                Assert.IsTrue(labelBlock.TranslatePoint(default, list)!.Value.Y >= thumbnail.TranslatePoint(default, list)!.Value.Y + thumbnail.Bounds.Height - 0.5,
                    $"{listName}: '{label}' label is not below the thumbnail");
            }

            window.Screenshot("theme-designer-lists");
            window.Close();
            main.Close();
        });
    }

    // Must match the tile width / selection-box thumbnail width in Views/ThemePickerStyles.axaml.
    private const double TileWidth = 150;
    private const double SelectionThumbnailWidth = 48;

    /// <summary>The dropdown's item containers that are laid out (the type filters hide the others, which still exist).</summary>
    private static List<ComboBoxItem> VisibleItems(ComboBox combo) =>
        combo.ItemsPanelRoot!.Children.OfType<ComboBoxItem>().Where(i => i.IsVisible).ToList();

    private static ThemePreviewImage Thumbnail(ComboBoxItem item) =>
        item.GetVisualDescendants().OfType<ThemePreviewImage>().Single();

    /// <summary>Opens the "Edit" menu flyout of an <see cref="ItemEditDockRoot"/>, then its "Theme" sub-flyout, and returns the theme ComboBox.</summary>
    private static ComboBox OpenItemDockThemeCombo(ItemEditDockRoot dock)
    {
        var editButton = dock.Find<Button>(b => b.Name == "EditButton")!;
        editButton.Flyout!.ShowAt(editButton);
        UiTestHelpers.Settle();

        var menu = ((Flyout)editButton.Flyout).Content as Control ?? throw new AssertFailedException("Edit flyout has no content");
        var themeRow = menu.FindByText<Button>("Theme") ?? throw new AssertFailedException("no Theme row in the Edit menu");
        themeRow.Flyout!.ShowAt(themeRow);
        UiTestHelpers.Settle();

        var themePanel = ((Flyout)themeRow.Flyout).Content as Control ?? throw new AssertFailedException("Theme flyout has no content");
        var combo = themePanel.Find<ComboBox>() ?? throw new AssertFailedException("no theme ComboBox in the Theme flyout");
        combo.IsDropDownOpen = true;
        UiTestHelpers.Settle();
        return combo;
    }

    [TestMethod]
    public async Task SongEditor_ThemeDropdown_ShowsTiles()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;

            var themes = new[]
            {
                MakeTheme("Plain", SlideThemeType.General),
                MakeTheme("Song one", SlideThemeType.SongTheme),
                MakeTheme("Song two", SlideThemeType.SongTheme),
            };
            var scriptureOnly = MakeTheme("Scripture only", SlideThemeType.ScriptureTheme);
            playlist.Designs.Clear();
            foreach (var t in themes) playlist.Designs.Add(t);
            playlist.Designs.Add(scriptureOnly);

            var editor = new SongEditorViewModel(SongItemInstance.NewDraft(playlist), playlist);
            var control = new SongEditorControl { DataContext = editor };
            var window = new Window { Width = 520, Height = 700, Content = control };
            window.Show();
            control.Find<TabControl>(t => t.Name == "TabControl")!.SelectedIndex = 2; // Design tab
            UiTestHelpers.Settle();

            var combo = control.Find<ComboBox>(c => c.Classes.Contains("themePicker"))
                        ?? throw new AssertFailedException("the song editor's theme ComboBox does not have the themePicker class");

            // Closed box with a selection: a small thumbnail plus the name, and the combo stays a normal height.
            editor.SelectedSlideTheme = themes[1];
            UiTestHelpers.Settle();
            var boxThumbnail = combo.GetVisualDescendants().OfType<ThemePreviewImage>().Single(t => t.IsEffectivelyVisible);
            Assert.AreSame(themes[1], boxThumbnail.SlideTheme);
            Assert.IsNotNull(boxThumbnail.Source, "closed box: no bitmap");
            Assert.AreEqual(SelectionThumbnailWidth, boxThumbnail.Bounds.Width, 0.5, "closed box thumbnail width");
            Assert.IsNotNull(combo.FindByText<TextBlock>("Song one"), "closed box: the name is not shown next to the thumbnail");
            Assert.IsTrue(combo.Bounds.Height < 60, $"closed box blew up the combo height: {combo.Bounds.Height}");
            window.Screenshot("theme-dropdown-song-editor-closed");

            combo.IsDropDownOpen = true;
            UiTestHelpers.Settle();

            var items = VisibleItems(combo);
            Assert.AreEqual(3, items.Count, "the scripture-only theme must stay filtered out of the song list");
            foreach (var (item, theme) in items.Zip(themes))
            {
                var thumb = Thumbnail(item);
                Assert.AreSame(theme, thumb.SlideTheme);
                Assert.IsNotNull(thumb.Source, $"{theme.Name}: tile has no bitmap");
                Assert.IsTrue(thumb.Bounds.Width > 0 && thumb.Bounds.Height > 0, $"{theme.Name}: tile thumbnail has no size");
                Assert.AreEqual(TileWidth, thumb.Parent is Control tile ? tile.Bounds.Width : 0, 0.5, $"{theme.Name}: tile width");
                Assert.IsNotNull(item.FindByText<TextBlock>(theme.Name), $"{theme.Name}: name is not shown on its tile");
            }

            // Two columns, wrapping, with no gap left by the hidden scripture theme.
            var positions = items.Select(i => i.TranslatePoint(default, combo.ItemsPanelRoot!)!.Value).ToList();
            Assert.AreEqual(positions[0].Y, positions[1].Y, 0.5, "tiles 1 and 2 share a row");
            Assert.IsTrue(positions[1].X > positions[0].X, "tile 2 is to the right of tile 1");
            Assert.AreEqual(positions[0].X, positions[2].X, 0.5, "tile 3 starts the next row");
            Assert.IsTrue(positions[2].Y > positions[0].Y, "tile 3 is on a second row");

            window.Screenshot("theme-dropdown-song-editor");

            // A real click on a tile inside the popup selects it.
            items[2].ClickInPopup();
            Assert.AreSame(themes[2], editor.SelectedSlideTheme, "clicking a tile did not select its theme");
            Assert.IsFalse(combo.IsDropDownOpen, "dropdown should close after picking");

            window.Close();
            main.Close();
        });
    }

    [TestMethod]
    public async Task ItemDock_SongThemeFlyout_ClickingATileSetsTheItemsTheme()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;
            var a = MakeTheme("Song A", SlideThemeType.SongTheme);
            var b = MakeTheme("Song B", SlideThemeType.SongTheme);
            playlist.Designs.Clear();
            playlist.Designs.Add(a);
            playlist.Designs.Add(b);

            var song = SongItemInstance.NewDraft(playlist);
            var dock = new ItemEditDockRoot { DataContext = song };
            var window = new Window { Width = 600, Height = 400, Content = dock };
            window.Show();
            UiTestHelpers.Settle();

            var combo = OpenItemDockThemeCombo(dock);
            Assert.IsTrue(combo.Classes.Contains("themePicker"), "item dock song combo lacks the themePicker class");
            var items = VisibleItems(combo);
            Assert.AreEqual(2, items.Count);
            Assert.IsNotNull(Thumbnail(items[1]).Source, "flyout tile has no bitmap");

            window.Screenshot("theme-dropdown-item-dock-song");
            items[1].ClickInPopup();
            Assert.AreSame(b, song.ResolvedDesignTheme, "clicking a tile in the flyout did not set the item's theme");
            Assert.AreEqual(b.Id, song.Design);

            window.Close();
            main.Close();
        });
    }

    [TestMethod]
    public async Task ItemDock_BlankOption_IsTextOnly()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;
            var longName = new string('W', 200);
            var scripture = MakeTheme(longName, SlideThemeType.ScriptureTheme);
            var songOnly = MakeTheme("Song only", SlideThemeType.SongTheme);
            playlist.Designs.Clear();
            playlist.Designs.Add(scripture);
            playlist.Designs.Add(songOnly);

            var item = new ScriptureItemInstance(playlist) { Title = "John 3" };
            var dock = new ItemEditDockRoot { DataContext = item };
            var window = new Window { Width = 600, Height = 400, Content = dock };
            window.Show();
            UiTestHelpers.Settle();

            var combo = OpenItemDockThemeCombo(dock);
            Assert.IsTrue(combo.Classes.Contains("themePicker"), "item dock scripture combo lacks the themePicker class");

            var items = VisibleItems(combo);
            Assert.AreEqual(2, items.Count, "blank option + the one scripture theme (the song-only theme is filtered out)");

            // The blank (null) option: a text-only tile. (A ContentPresenter never templates a null item, so there is no
            // preview control at all; the theme picker style supplies the "(None)" label instead.)
            var blank = items[0];
            Assert.IsNull(blank.DataContext);
            Assert.AreEqual(0, blank.GetVisualDescendants().OfType<ThemePreviewImage>().Count(t => t.Bounds.Width > 0), "blank tile must not show a preview");
            Assert.IsNotNull(blank.FindByText<TextBlock>("(None)"), "blank tile shows no text, so it looks like an empty gap");
            Assert.IsTrue(blank.Bounds.Width >= TileWidth && blank.Bounds.Height > 10, $"blank tile collapsed: {blank.Bounds}");
            Assert.AreEqual(items[1].Bounds.Width, blank.Bounds.Width, 0.5, "blank tile is not the same width as the theme tiles");

            // A 200-character name is trimmed to the tile, not allowed to widen it.
            var tile = Thumbnail(items[1]).Parent as Control ?? throw new AssertFailedException("tile root missing");
            Assert.AreEqual(TileWidth, tile.Bounds.Width, 0.5, "long name widened the tile");
            var nameBlock = items[1].GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == longName);
            Assert.AreEqual(TextTrimming.CharacterEllipsis, nameBlock.TextTrimming);
            Assert.IsTrue(nameBlock.Bounds.Width <= TileWidth + 0.5, $"name is {nameBlock.Bounds.Width} wide inside a {TileWidth} tile");

            window.Screenshot("theme-dropdown-item-dock-scripture");

            // Picking a tile, then the blank option, reaches the item through the flyout's two-way binding.
            items[1].ClickInPopup();
            Assert.AreEqual(scripture.Id, item.Design, "clicking the theme tile did not set the scripture item's theme");

            combo.IsDropDownOpen = true;
            UiTestHelpers.Settle();
            VisibleItems(combo)[0].ClickInPopup();
            Assert.AreEqual(Guid.Empty, item.Design, "clicking the blank tile did not clear the scripture item's theme");

            window.Close();
            main.Close();
        });
    }
}
