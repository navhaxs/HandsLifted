using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Controls;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Undo;
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

    /// <summary>The thumbnail showing <paramref name="theme"/> among the visible ones of <paramref name="root"/> (a list or a dropdown).</summary>
    private static ThemePreviewImage ThumbnailOf(Visual root, BaseSlideTheme theme) =>
        root.GetVisualDescendants().OfType<ThemePreviewImage>().Where(t => t.IsEffectivelyVisible).Single(t => ReferenceEquals(t.SlideTheme, theme));

    /// <summary>Applies a default the way the designer's "Set as default for ..." menu items do (the handlers run this action).</summary>
    private static void SetDefault(DefaultThemeKind kind, BaseSlideTheme theme) =>
        new SetDefaultThemeAction(Globals.Instance.AppPreferences, Globals.Instance.MainViewModel.Playlist, kind, theme.Id, () => { }).Do();

    [TestMethod]
    public async Task Designer_Lists_StarMarksTheDefaultThemes_AndFollowsChanges()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var vm = Globals.Instance.MainViewModel;

            var general = MakeTheme("General theme", SlideThemeType.General);
            var songA = MakeTheme("Song A", SlideThemeType.SongTheme);
            var songB = MakeTheme("Song B", SlideThemeType.SongTheme);
            var songMotion = MakeTheme("Song motion", SlideThemeType.SongTheme);
            songMotion.DefaultMotionBackgroundVideoPath = @"Z:\loop.mp4";
            var scripture = MakeTheme("Scripture theme", SlideThemeType.ScriptureTheme);
            var scriptureOther = MakeTheme("Scripture other", SlideThemeType.ScriptureTheme);
            vm.Playlist.Designs.Clear();
            foreach (var t in new[] { general, songA, songB, songMotion, scripture, scriptureOther }) vm.Playlist.Designs.Add(t);

            Globals.Instance.AppPreferences.DefaultTheme = general; // the general default
            vm.Playlist.DefaultSongThemeId = songA.Id; // a playlist override
            Globals.Instance.AppPreferences.DefaultSongMotionThemeId = songMotion.Id; // an app-wide default
            Globals.Instance.AppPreferences.DefaultScriptureThemeId = scripture.Id;

            var designer = new SlideThemeDesigner { DataContext = vm };
            var window = new Window { Width = 1400, Height = 900, Content = designer };
            window.Show();
            UiTestHelpers.Settle();

            ThemePreviewImage Thumb(BaseSlideTheme t) => ThumbnailOf(designer, t);

            Assert.IsTrue(Thumb(general).IsDefault, "general default");
            Assert.IsTrue(Thumb(songA).IsDefault, "song default (playlist override)");
            Assert.IsFalse(Thumb(songB).IsDefault, "an ordinary song theme must not be starred");
            Assert.IsTrue(Thumb(songMotion).IsDefault, "song motion default (app preference)");
            Assert.IsTrue(Thumb(scripture).IsDefault, "scripture default");
            Assert.IsFalse(Thumb(scriptureOther).IsDefault, "an ordinary scripture theme must not be starred");

            // The star and the text labels agree for every theme.
            foreach (var thumb in designer.GetVisualDescendants().OfType<ThemePreviewImage>().Where(t => t.IsEffectivelyVisible))
            {
                var row = thumb.GetVisualAncestors().OfType<ListBoxItem>().First();
                var labelled = row.GetVisualDescendants().OfType<TextBlock>()
                    .Any(t => t.IsEffectivelyVisible && t.Text != null && t.Text.StartsWith("default"));
                Assert.AreEqual(labelled, thumb.IsDefault, $"{((BaseSlideTheme)thumb.SlideTheme!).Name}: star and 'default' label disagree");
            }

            window.Screenshot("theme-designer-lists-defaults");

            // "Set as default for Songs" on B: the app default moves to B and the playlist override is cleared, so the star moves.
            SetDefault(DefaultThemeKind.Song, songB);
            UiTestHelpers.Settle();
            Assert.IsFalse(Thumb(songA).IsDefault, "the star stayed on the old default");
            Assert.IsTrue(Thumb(songB).IsDefault, "the star did not move to the new default");

            // A playlist override (set outside the menu, e.g. by loading a playlist) moves it back.
            vm.Playlist.DefaultSongThemeId = songA.Id;
            UiTestHelpers.Settle();
            Assert.IsTrue(Thumb(songA).IsDefault, "playlist override did not move the star");
            Assert.IsFalse(Thumb(songB).IsDefault, "the app default kept its star under a playlist override");

            SetDefault(DefaultThemeKind.Scripture, scriptureOther);
            UiTestHelpers.Settle();
            Assert.IsFalse(Thumb(scripture).IsDefault);
            Assert.IsTrue(Thumb(scriptureOther).IsDefault);

            window.Close();
            main.Close();
        });
    }

    [TestMethod]
    public async Task SongEditor_ThemeDropdown_StarsTheDefaultTileOnly()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;
            var plain = MakeTheme("Plain", SlideThemeType.SongTheme);
            var starred = MakeTheme("Starred", SlideThemeType.SongTheme);
            var motion = MakeTheme("Motion default", SlideThemeType.SongTheme);
            motion.DefaultMotionBackgroundVideoPath = @"Z:\loop.mp4";
            playlist.Designs.Clear();
            foreach (var t in new[] { plain, starred, motion }) playlist.Designs.Add(t);
            playlist.DefaultSongThemeId = starred.Id;
            Globals.Instance.AppPreferences.DefaultSongMotionThemeId = motion.Id;

            var editor = new SongEditorViewModel(SongItemInstance.NewDraft(playlist), playlist);
            var control = new SongEditorControl { DataContext = editor };
            var window = new Window { Width = 520, Height = 700, Content = control };
            window.Show();
            control.Find<TabControl>(t => t.Name == "TabControl")!.SelectedIndex = 2; // Design tab
            UiTestHelpers.Settle();
            var combo = control.Find<ComboBox>(c => c.Classes.Contains("themePicker"))!;

            // The closed box keeps its small, star-free thumbnail even for a default theme.
            editor.SelectedSlideTheme = starred;
            UiTestHelpers.Settle();
            Assert.IsFalse(combo.GetVisualDescendants().OfType<ThemePreviewImage>().Single(t => t.IsEffectivelyVisible).IsDefault,
                "the closed box's 48 px thumbnail must not carry a star");

            combo.IsDropDownOpen = true;
            UiTestHelpers.Settle();
            var items = VisibleItems(combo);
            Assert.AreEqual(3, items.Count);
            Assert.IsFalse(Thumbnail(items[0]).IsDefault, "ordinary tile starred");
            Assert.IsTrue(Thumbnail(items[1]).IsDefault, "the default theme's tile has no star");
            Assert.IsTrue(Thumbnail(items[2]).IsDefault, "the song-motion default's tile has no star");
            window.Screenshot("theme-dropdown-song-editor-defaults");

            // Changing the default while the dropdown is open moves the star.
            SetDefault(DefaultThemeKind.Song, plain);
            UiTestHelpers.Settle();
            Assert.IsTrue(Thumbnail(items[0]).IsDefault, "star did not move to the new default tile");
            Assert.IsFalse(Thumbnail(items[1]).IsDefault, "star stayed on the old default tile");

            window.Close();
            main.Close();
        });
    }

    // The tile geometry lives in one place: the x:Double resources of Views/ThemePickerStyles.axaml.
    private static double StyleNumber(string key) =>
        (double)(Application.Current!.FindResource(key) ?? throw new AssertFailedException($"resource {key} not found"));

    private static double TileWidth => StyleNumber("ThemePickerTileWidth");
    private static double ItemWidth => StyleNumber("ThemePickerItemWidth");
    private static double PanelWidth => StyleNumber("ThemePickerPanelWidth");
    private const double SelectionThumbnailWidth = 48; // set on the closed-box thumbnail in the same file

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
    public async Task SongEditor_ClosedBox_TrimsALongThemeName()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;
            var longName = new string('W', 60);
            var theme = MakeTheme(longName, SlideThemeType.SongTheme);
            playlist.Designs.Clear();
            playlist.Designs.Add(theme);

            var editor = new SongEditorViewModel(SongItemInstance.NewDraft(playlist), playlist);
            var control = new SongEditorControl { DataContext = editor };
            var window = new Window { Width = 520, Height = 700, Content = control };
            window.Show();
            control.Find<TabControl>(t => t.Name == "TabControl")!.SelectedIndex = 2; // Design tab
            UiTestHelpers.Settle();
            var combo = control.Find<ComboBox>(c => c.Classes.Contains("themePicker"))!;

            editor.SelectedSlideTheme = theme;
            UiTestHelpers.Settle();

            // A horizontal StackPanel measures its text with unlimited width, so the ellipsis never kicked in and the
            // name just ran out of the box.
            var name = combo.FindByText<TextBlock>(longName) ?? throw new AssertFailedException("closed box shows no name");
            Assert.IsTrue(name.Bounds.Width <= combo.Bounds.Width,
                $"the name is {name.Bounds.Width} wide inside a {combo.Bounds.Width} wide box: it is not trimmed");
            Assert.IsTrue(name.TranslatePoint(new Point(name.Bounds.Width, 0), combo)!.Value.X <= combo.Bounds.Width + 0.5,
                "the name runs past the right edge of the box");

            window.Close();
            main.Close();
        });
    }

    // Headless runs at display scale 1.0, where two 158 DIP items trivially fit a 316 DIP panel. At 125% layout rounding
    // makes each item 158.4 DIP, two need 316.8 and the second tile wrapped onto its own row (one column instead of two).
    // So check the geometry at the scales Windows actually uses, from the realized panel and item.
    [TestMethod]
    public async Task ThemeDropdown_TwoTilesFitPerRow_AtFractionalDisplayScaling()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;
            playlist.Designs.Clear();
            playlist.Designs.Add(MakeTheme("One", SlideThemeType.SongTheme));
            playlist.Designs.Add(MakeTheme("Two", SlideThemeType.SongTheme));

            var editor = new SongEditorViewModel(SongItemInstance.NewDraft(playlist), playlist);
            var control = new SongEditorControl { DataContext = editor };
            var window = new Window { Width = 520, Height = 700, Content = control };
            window.Show();
            control.Find<TabControl>(t => t.Name == "TabControl")!.SelectedIndex = 2; // Design tab
            UiTestHelpers.Settle();
            var combo = control.Find<ComboBox>(c => c.Classes.Contains("themePicker"))!;
            combo.IsDropDownOpen = true;
            UiTestHelpers.Settle();

            var panel = combo.ItemsPanelRoot as WrapPanel ?? throw new AssertFailedException("the theme list is not a WrapPanel");
            var item = VisibleItems(combo)[0];
            Assert.AreEqual(ItemWidth, item.DesiredSize.Width, 0.01, "ThemePickerItemWidth must equal tile + item padding");
            Assert.AreEqual(PanelWidth, panel.Width, 0.01, "panel width");

            foreach (var scale in new[] { 1.0, 1.25, 1.5, 1.75, 2.0, 2.25 })
            {
                var rounded = LayoutHelper.RoundLayoutSizeUp(item.DesiredSize, scale);
                Assert.IsTrue(2 * rounded.Width <= panel.Width + 1e-9,
                    $"scale {scale}: two items need {2 * rounded.Width} DIP but the panel is {panel.Width}, so the second tile wraps");
                if (!double.IsNaN(panel.ItemWidth))
                    Assert.IsTrue(2 * panel.ItemWidth <= panel.Width, $"scale {scale}: two ItemWidth slots do not fit the panel");
            }

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
