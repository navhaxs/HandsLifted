using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Controls;
using HandsLiftedApp.Core.Models;
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

            // The thumbnail must not displace the existing labels: they stay visible beside it, at the top of the text column.
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
                Assert.IsTrue(labelBlock.TranslatePoint(default, list)!.Value.X >= thumbnail.TranslatePoint(default, list)!.Value.X + thumbnail.Bounds.Width - 0.5,
                    $"{listName}: '{label}' label is not to the right of the thumbnail");
                Assert.IsTrue(labelBlock.TranslatePoint(default, list)!.Value.Y < thumbnail.TranslatePoint(default, list)!.Value.Y + thumbnail.Bounds.Height,
                    $"{listName}: '{label}' label is not level with the thumbnail");
            }

            window.Screenshot("theme-designer-lists");
            window.Close();
            main.Close();
        });
    }

    [TestMethod]
    public async Task Designer_Lists_PreviewIsHalfSize_AndLongNamesWrapWithoutWideningTheColumn()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var vm = Globals.Instance.MainViewModel;

            var shortName = MakeTheme("Short", SlideThemeType.SongTheme);
            var longName = MakeTheme("This is an extremely long theme name that has sixty chars!!!My new theme", SlideThemeType.SongTheme);
            vm.Playlist.Designs.Clear();
            vm.Playlist.Designs.Add(shortName);

            var designer = new SlideThemeDesigner { DataContext = vm };
            var window = new Window { Width = 1400, Height = 900, Content = designer };
            window.Show();
            UiTestHelpers.Settle();
            var list = designer.Find<ListBox>(l => l.Name == "songDesignsListBox")!;
            var columnBefore = list.Bounds.Width;
            var shortHeight = list.FindByText<TextBlock>("Short")!.Bounds.Height;

            vm.Playlist.Designs.Add(longName);
            UiTestHelpers.Settle();

            // The preview is half the old 180 DIP width (16:9 follows).
            foreach (var thumbnail in VisibleThumbnails(list))
            {
                Assert.AreEqual(90, thumbnail.Bounds.Width, 0.5, "designer preview width");
                Assert.AreEqual(90 * 9 / 16.0, thumbnail.Bounds.Height, 0.5, "designer preview height");
            }

            // The long name wraps onto several lines inside the item instead of widening the list column.
            var nameBlock = list.FindByText<TextBlock>(longName.Name)!;
            Assert.IsNotNull(nameBlock, "long name is not visible");
            Assert.IsTrue(nameBlock.Bounds.Height > shortHeight * 1.5, $"long name did not wrap: {nameBlock.Bounds.Height} high vs {shortHeight} for one line");
            Assert.AreEqual(columnBefore, list.Bounds.Width, 0.5, "a long name widened the list column");
            Assert.IsTrue(nameBlock.TranslatePoint(new Point(nameBlock.Bounds.Width, 0), list)!.Value.X <= list.Bounds.Width + 0.5,
                "the wrapped name sticks out of the list");

            window.Screenshot("theme-designer-lists-long-name");
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

    /// <summary>Every row of the open dropdown that is laid out, in order: group headers and tiles (hidden themes still exist).</summary>
    private static List<ComboBoxItem> VisibleRows(ComboBox combo) =>
        combo.ItemsPanelRoot!.Children.OfType<ComboBoxItem>().Where(i => i.IsVisible).ToList();

    private static bool IsHeader(ComboBoxItem item) => item.Content is ThemePickerGroupHeader;

    /// <summary>The dropdown's tiles that are laid out (themes and the blank option, not the group headers).</summary>
    private static List<ComboBoxItem> VisibleItems(ComboBox combo) => VisibleRows(combo).Where(i => !IsHeader(i)).ToList();

    /// <summary>The open dropdown as text: "#Header" for a group header, "(None)" for the blank option, else the theme name.</summary>
    private static string DescribeRows(ComboBox combo) => string.Join(", ", VisibleRows(combo).Select(i => i.Content switch
    {
        ThemePickerGroupHeader h => "#" + h.Title,
        BaseSlideTheme t => t.Name,
        null => "(None)",
        var other => "?" + other,
    }));

    private static Point PositionIn(ComboBox combo, Control item) => item.TranslatePoint(default, combo.ItemsPanelRoot!)!.Value;

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

    /// <summary>
    /// Themes hidden by the song/scripture filter sit in the middle of the list (not only at its end) and must not leave
    /// an empty slot in the tile grid: a WrapPanel with an ItemWidth reserves a full slot even for a collapsed child.
    /// </summary>
    [TestMethod]
    public async Task SongEditor_ThemeDropdown_HiddenThemesInTheMiddleLeaveNoGap()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;

            var visible = Enumerable.Range(1, 5).Select(i => MakeTheme($"Song {i}", SlideThemeType.SongTheme)).ToList();
            playlist.Designs.Clear();
            // Hidden scripture themes between visible ones, including one that would land at the start of the second column.
            playlist.Designs.Add(MakeTheme("Hidden A", SlideThemeType.ScriptureTheme));
            playlist.Designs.Add(visible[0]);
            playlist.Designs.Add(MakeTheme("Hidden B", SlideThemeType.ScriptureTheme));
            playlist.Designs.Add(visible[1]);
            playlist.Designs.Add(visible[2]);
            playlist.Designs.Add(MakeTheme("Hidden C", SlideThemeType.ScriptureTheme));
            playlist.Designs.Add(visible[3]);
            playlist.Designs.Add(visible[4]);

            var editor = new SongEditorViewModel(SongItemInstance.NewDraft(playlist), playlist);
            var control = new SongEditorControl { DataContext = editor };
            var window = new Window { Width = 520, Height = 700, Content = control };
            window.Show();
            control.Find<TabControl>(t => t.Name == "TabControl")!.SelectedIndex = 2; // Design tab
            UiTestHelpers.Settle();
            var combo = control.Find<ComboBox>(c => c.Classes.Contains("themePicker"))!;
            combo.IsDropDownOpen = true;
            UiTestHelpers.Settle();

            var items = VisibleItems(combo);
            Assert.AreEqual(5, items.Count, "only the five song themes are visible");
            var positions = items.Select(i => i.TranslatePoint(default, combo.ItemsPanelRoot!)!.Value).ToList();
            var rowHeight = positions[2].Y - positions[0].Y;
            Assert.IsTrue(rowHeight > 0, "the third tile starts a second row");
            for (var i = 0; i < positions.Count; i++)
            {
                Assert.AreEqual(positions[i % 2].X, positions[i].X, 0.5, $"tile {i + 1} is in column {i % 2 + 1}");
                Assert.AreEqual(positions[0].Y + i / 2 * rowHeight, positions[i].Y, 0.5,
                    $"tile {i + 1} is in row {i / 2 + 1}: a hidden theme left a gap before it");
            }
            Assert.IsTrue(positions[1].X > positions[0].X, "two columns");

            window.Close();
            main.Close();
        });
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

            // Grouped: the general theme alone in its group, then the two song themes side by side under their header,
            // with no gap left by the hidden scripture theme.
            Assert.AreEqual("#General, Plain, #Song Themes, Song one, Song two", DescribeRows(combo));
            var positions = items.Select(i => PositionIn(combo, i)).ToList();
            Assert.AreEqual(positions[0].X, positions[1].X, 0.5, "the song group starts a new row");
            Assert.IsTrue(positions[1].Y > positions[0].Y, "the song group is below the general group");
            Assert.AreEqual(positions[1].Y, positions[2].Y, 0.5, "song tiles 1 and 2 share a row");
            Assert.IsTrue(positions[2].X > positions[1].X, "song tile 2 is to the right of song tile 1");

            window.Screenshot("theme-dropdown-song-editor");

            // A real click on a tile inside the popup selects it.
            items[2].ClickInPopup();
            Assert.AreSame(themes[2], editor.SelectedSlideTheme, "clicking a tile did not select its theme");
            Assert.IsFalse(combo.IsDropDownOpen, "dropdown should close after picking");

            window.Close();
            main.Close();
        });
    }

    /// <summary>Shows a <see cref="SongEditorControl"/> on its Design tab for a new draft song and returns its theme dropdown.</summary>
    private static (Window Window, SongEditorViewModel Editor, ComboBox Combo) ShowSongEditorDesignTab(PlaylistInstance playlist)
    {
        var editor = new SongEditorViewModel(SongItemInstance.NewDraft(playlist), playlist);
        var control = new SongEditorControl { DataContext = editor };
        var window = new Window { Width = 520, Height = 700, Content = control };
        window.Show();
        control.Find<TabControl>(t => t.Name == "TabControl")!.SelectedIndex = 2; // Design tab
        UiTestHelpers.Settle();
        return (window, editor, control.Find<ComboBox>(c => c.Classes.Contains("themePicker"))!);
    }

    private static Rect BoundsIn(ComboBox combo, Control item) => new(PositionIn(combo, item), item.Bounds.Size);

    [TestMethod]
    public async Task SongEditor_ThemeDropdown_GroupsTilesUnderCategoryHeaders()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;
            playlist.Designs.Clear();
            // Mixed source order; three general themes so the general group ends on a half-full row.
            foreach (var t in new[]
                     {
                         MakeTheme("Gen A", SlideThemeType.General),
                         MakeTheme("Song A", SlideThemeType.SongTheme),
                         MakeTheme("Gen B", SlideThemeType.General),
                         MakeTheme("Scripture A", SlideThemeType.ScriptureTheme),
                         MakeTheme("Song B", SlideThemeType.SongTheme),
                         MakeTheme("Gen C", SlideThemeType.General),
                     })
                playlist.Designs.Add(t);
            playlist.DefaultSongThemeId = playlist.Designs[4].Id; // Song B: the star must still show on a grouped tile

            var (window, _, combo) = ShowSongEditorDesignTab(playlist);
            combo.IsDropDownOpen = true;
            UiTestHelpers.Settle();

            Assert.AreEqual("#General, Gen A, Gen B, Gen C, #Song Themes, Song A, Song B", DescribeRows(combo),
                "general themes, then song themes, each under its header, in playlist order; no scripture group");

            var rows = VisibleRows(combo);
            var generalHeader = rows[0];
            var songHeader = rows[4];
            ComboBoxItem Tile(string name) => rows.Single(r => r.Content is BaseSlideTheme t && t.Name == name);

            foreach (var header in new[] { generalHeader, songHeader })
            {
                var title = ((ThemePickerGroupHeader)header.Content!).Title;
                Assert.AreEqual(0, PositionIn(combo, header).X, 0.5, $"{title}: header does not start at the left edge");
                Assert.AreEqual(PanelWidth, header.Bounds.Width, 0.5, $"{title}: header does not span the panel");
                Assert.IsNotNull(header.FindByText<TextBlock>(title), $"{title}: header text not shown");
                Assert.AreEqual(0, header.GetVisualDescendants().OfType<ThemePreviewImage>().Count(), $"{title}: a header has no thumbnail");
                Assert.IsFalse(header.IsEnabled || header.Focusable, $"{title}: a header must not be selectable");
                Assert.IsTrue(header.Classes.Contains("themeGroupHeader"), $"{title}: header lacks its style class");
                Assert.IsTrue(header.Bounds.Height < 40, $"{title}: header row is {header.Bounds.Height} high, not a small label");
                // Readable on the dark popup: the tiles' text colour (a header is disabled, and an unresolved or
                // "disabled" brush made it near-black on black), only dimmed a little.
                var tileName = rows[1].FindByText<TextBlock>("Gen A")!;
                var headerText = header.FindByText<TextBlock>(title)!;
                Assert.AreEqual((tileName.Foreground as ISolidColorBrush)?.Color, (headerText.Foreground as ISolidColorBrush)?.Color,
                    $"{title}: header text colour differs from the tiles'");
                var dimming = headerText.GetVisualAncestors().OfType<Visual>().TakeWhile(v => v != header).Aggregate(1.0, (o, v) => o * v.Opacity);
                Assert.IsTrue(dimming is >= 0.5 and < 1, $"{title}: header opacity {dimming}: should be dimmed but readable");
            }

            // Two columns inside a group; the odd third general tile sits alone and the song header starts below it.
            var genA = BoundsIn(combo, Tile("Gen A"));
            var genB = BoundsIn(combo, Tile("Gen B"));
            var genC = BoundsIn(combo, Tile("Gen C"));
            var songA = BoundsIn(combo, Tile("Song A"));
            var songB = BoundsIn(combo, Tile("Song B"));
            var songHeaderRect = BoundsIn(combo, songHeader);
            Assert.IsTrue(genA.Y >= BoundsIn(combo, generalHeader).Bottom - 0.5, "first general tile is below its header");
            Assert.AreEqual(genA.Y, genB.Y, 0.5, "general tiles 1 and 2 share a row");
            Assert.IsTrue(genB.X > genA.X, "general tile 2 is in the second column");
            Assert.AreEqual(genA.X, genC.X, 0.5, "general tile 3 starts a new row");
            Assert.IsTrue(genC.Y > genA.Y, "general tile 3 is on the next row");
            Assert.IsTrue(songHeaderRect.Y >= genC.Bottom - 0.5, "the song header starts below the last general row");
            Assert.AreEqual(genA.X, songA.X, 0.5, "the song group's first tile starts a fresh row (not beside Gen C)");
            Assert.IsTrue(songA.Y >= songHeaderRect.Bottom - 0.5, "song tiles are below their header");
            Assert.AreEqual(songA.Y, songB.Y, 0.5, "song tiles share a row");
            Assert.IsTrue(songB.X > songA.X, "song tile 2 is in the second column");

            // Nothing overlaps.
            var rects = rows.Select(r => BoundsIn(combo, r)).ToList();
            for (var i = 0; i < rects.Count; i++)
            for (var j = i + 1; j < rects.Count; j++)
                Assert.IsFalse(rects[i].Intersects(rects[j]) && rects[i].Intersect(rects[j]).Width > 0.5 && rects[i].Intersect(rects[j]).Height > 0.5,
                    $"rows {i} and {j} overlap: {rects[i]} / {rects[j]}");

            Assert.IsTrue(Thumbnail(Tile("Song B")).IsDefault, "the default theme's tile lost its star");
            Assert.IsFalse(Thumbnail(Tile("Song A")).IsDefault);

            window.Screenshot("theme-dropdown-grouped");
            window.Close();
            main.Close();
        });
    }

    [TestMethod]
    public async Task SongEditor_ThemeDropdown_ClickingAHeaderDoesNothing()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;
            var genA = MakeTheme("Gen A", SlideThemeType.General);
            var songA = MakeTheme("Song A", SlideThemeType.SongTheme);
            playlist.Designs.Clear();
            playlist.Designs.Add(genA);
            playlist.Designs.Add(songA);

            var (window, editor, combo) = ShowSongEditorDesignTab(playlist);
            editor.SelectedSlideTheme = songA;
            UiTestHelpers.Settle();
            combo.IsDropDownOpen = true;
            UiTestHelpers.Settle();

            var headers = VisibleRows(combo).Where(IsHeader).ToList();
            Assert.AreEqual(2, headers.Count, "expected a General and a Song Themes header");
            foreach (var header in headers)
            {
                header.ClickInPopup();
                Assert.AreSame(songA, editor.SelectedSlideTheme, "clicking a header changed the theme");
                Assert.AreSame(songA, combo.SelectedItem);
                Assert.IsTrue(combo.IsDropDownOpen, "clicking a header closed the dropdown");
                Assert.IsFalse(header.Classes.Contains(":pointerover"), "a header highlights on hover");
                Assert.IsFalse(header.Classes.Contains(":selected"));
            }

            // A tile still works.
            VisibleItems(combo).Single(i => i.Content == genA).ClickInPopup();
            Assert.AreSame(genA, editor.SelectedSlideTheme, "clicking a tile did not select its theme");
            Assert.IsFalse(combo.IsDropDownOpen);

            window.Close();
            main.Close();
        });
    }

    [TestMethod]
    public async Task SongEditor_ThemeDropdown_KeyboardNeverLandsOnAHeaderOrAHiddenTheme()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;
            var genA = MakeTheme("Gen A", SlideThemeType.General);
            var genB = MakeTheme("Gen B", SlideThemeType.General);
            var songA = MakeTheme("Song A", SlideThemeType.SongTheme);
            var songB = MakeTheme("Song B", SlideThemeType.SongTheme);
            var scripture = MakeTheme("Scripture A", SlideThemeType.ScriptureTheme); // listed last, hidden
            playlist.Designs.Clear();
            foreach (var t in new[] { genA, genB, songA, songB, scripture }) playlist.Designs.Add(t);

            var (window, editor, combo) = ShowSongEditorDesignTab(playlist);
            combo.IsDropDownOpen = true;
            UiTestHelpers.Settle();
            Assert.AreEqual("#General, Gen A, Gen B, #Song Themes, Song A, Song B", DescribeRows(combo));
            combo.IsDropDownOpen = false;
            UiTestHelpers.Settle();

            void Press(Key key)
            {
                window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
                UiTestHelpers.Settle();
            }

            // Closed box: Up/Down step over the header between the groups and stop at the ends.
            editor.SelectedSlideTheme = songA;
            UiTestHelpers.Settle();
            combo.Focus();
            Press(Key.Up);
            Assert.AreSame(genB, editor.SelectedSlideTheme, "Up from the first song theme should skip the 'Song Themes' header");
            Press(Key.Down);
            Assert.AreSame(songA, editor.SelectedSlideTheme);
            Press(Key.Down);
            Assert.AreSame(songB, editor.SelectedSlideTheme);
            Press(Key.Down);
            Assert.AreSame(songB, editor.SelectedSlideTheme, "Down past the last song theme landed on the hidden scripture theme");
            editor.SelectedSlideTheme = genA;
            UiTestHelpers.Settle();
            Press(Key.Up);
            Assert.AreSame(genA, editor.SelectedSlideTheme, "Up from the first theme landed on the 'General' header");
            Press(Key.Home);
            Press(Key.End);
            Assert.IsInstanceOfType(combo.SelectedItem, typeof(BaseSlideTheme));

            // Type-ahead matches theme names (not the "General" header, not the hidden scripture theme).
            window.KeyTextInput("Gen");
            UiTestHelpers.Settle();
            Assert.AreSame(genA, editor.SelectedSlideTheme, "typing 'Gen' should pick the first general theme, not its header");
            window.KeyTextInput("Song B");
            UiTestHelpers.Settle();
            Assert.AreSame(songB, editor.SelectedSlideTheme, "type-ahead by theme name");
            window.KeyTextInput("Scr");
            UiTestHelpers.Settle();
            Assert.AreSame(songB, editor.SelectedSlideTheme, "type-ahead picked the hidden scripture theme (or a header)");

            // Open list: Home/End/Up/Down move the focus over tiles only; Enter picks the focused tile.
            combo.IsDropDownOpen = true;
            UiTestHelpers.Settle();
            object? Focused() => (TopLevel.GetTopLevel(combo)!.FocusManager!.GetFocusedElement() as ComboBoxItem)?.Content;
            Press(Key.Home);
            Assert.AreSame(genA, Focused(), $"Home should focus the first tile, not the 'General' header (focus is on {(Focused() as BaseSlideTheme)?.Name ?? Focused()?.ToString()})");
            Press(Key.Down);
            Assert.AreSame(genB, Focused());
            Press(Key.Down);
            Assert.AreSame(songA, Focused(), "Down should step over the 'Song Themes' header");
            Press(Key.Up);
            Assert.AreSame(genB, Focused(), "Up should step over the 'Song Themes' header");
            Press(Key.End);
            Assert.AreSame(songB, Focused(), "End should focus the last shown tile");
            Press(Key.Up);
            Press(Key.Enter);
            Assert.AreSame(songA, editor.SelectedSlideTheme, "Enter should pick the focused tile");
            Assert.IsFalse(combo.IsDropDownOpen);

            window.Close();
            main.Close();
        });
    }

    [TestMethod]
    public async Task SongEditor_ThemeDropdown_TypeChangeRegroupsTheTile_AndKeepsTheSelection()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;
            var genA = MakeTheme("Gen A", SlideThemeType.General);
            var songA = MakeTheme("Song A", SlideThemeType.SongTheme);
            var songB = MakeTheme("Song B", SlideThemeType.SongTheme);
            playlist.Designs.Clear();
            foreach (var t in new[] { genA, songA, songB }) playlist.Designs.Add(t);

            var (window, editor, combo) = ShowSongEditorDesignTab(playlist);
            editor.SelectedSlideTheme = songB;
            UiTestHelpers.Settle();
            combo.IsDropDownOpen = true;
            UiTestHelpers.Settle();
            Assert.AreEqual("#General, Gen A, #Song Themes, Song A, Song B", DescribeRows(combo));

            // "Move to General" in the designer, while this dropdown is open.
            songB.Type = SlideThemeType.General;
            UiTestHelpers.Settle();
            Assert.AreEqual("#General, Gen A, Song B, #Song Themes, Song A", DescribeRows(combo));
            var tiles = VisibleItems(combo);
            Assert.AreEqual(PositionIn(combo, tiles[0]).Y, PositionIn(combo, tiles[1]).Y, 0.5, "Song B now sits beside Gen A");
            Assert.AreSame(songB, combo.SelectedItem, "regrouping the selected theme dropped the selection");
            Assert.AreSame(songB, editor.SelectedSlideTheme);
            Assert.AreEqual(songB.Id, editor.Song.Design);
            window.Screenshot("theme-dropdown-regrouped");

            // The last song theme leaves: its header goes too.
            songA.Type = SlideThemeType.ScriptureTheme;
            UiTestHelpers.Settle();
            Assert.AreEqual("#General, Gen A, Song B", DescribeRows(combo));

            // The selected theme becomes a scripture theme: hidden from the song list, but still the song's theme.
            songB.Type = SlideThemeType.ScriptureTheme;
            UiTestHelpers.Settle();
            Assert.AreEqual("#General, Gen A", DescribeRows(combo));
            Assert.AreSame(songB, combo.SelectedItem);
            Assert.AreEqual(songB.Id, editor.Song.Design);

            // And it comes back.
            songA.Type = SlideThemeType.SongTheme;
            UiTestHelpers.Settle();
            Assert.AreEqual("#General, Gen A, #Song Themes, Song A", DescribeRows(combo));

            window.Close();
            main.Close();
        });
    }

    [TestMethod]
    public async Task ItemDock_SongThemeFlyout_TypeChangeKeepsTheItemsTheme()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;
            var genA = MakeTheme("Gen A", SlideThemeType.General);
            var songA = MakeTheme("Song A", SlideThemeType.SongTheme);
            var songB = MakeTheme("Song B", SlideThemeType.SongTheme);
            playlist.Designs.Clear();
            foreach (var t in new[] { genA, songA, songB }) playlist.Designs.Add(t);

            var song = SongItemInstance.NewDraft(playlist);
            song.Design = songB.Id;
            var dock = new ItemEditDockRoot { DataContext = song };
            var window = new Window { Width = 600, Height = 400, Content = dock };
            window.Show();
            UiTestHelpers.Settle();

            var combo = OpenItemDockThemeCombo(dock);
            Assert.AreSame(songB, combo.SelectedItem);

            // The item dock writes a null selection straight into the item's Design, so a dropped selection would lose its theme.
            songB.Type = SlideThemeType.General;
            UiTestHelpers.Settle();
            Assert.AreEqual("#General, Gen A, Song B, #Song Themes, Song A", DescribeRows(combo));
            Assert.AreEqual(songB.Id, song.Design, "regrouping the item's theme cleared it");

            songA.Type = SlideThemeType.General; // another theme moving past it
            UiTestHelpers.Settle();
            Assert.AreEqual(songB.Id, song.Design);

            songB.Type = SlideThemeType.ScriptureTheme;
            UiTestHelpers.Settle();
            Assert.AreEqual(songB.Id, song.Design, "the theme turning into a scripture theme cleared the song's theme");
            Assert.AreSame(songB, combo.SelectedItem);

            window.Close();
            main.Close();
        });
    }

    [TestMethod]
    public async Task ItemDock_ScriptureThemeFlyout_NoneFirst_ThenGeneralAndScriptureGroups()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow();
            var playlist = Globals.Instance.MainViewModel.Playlist;
            var scriptureA = MakeTheme("Scripture A", SlideThemeType.ScriptureTheme);
            playlist.Designs.Clear();
            foreach (var t in new[]
                     {
                         MakeTheme("Song A", SlideThemeType.SongTheme),
                         scriptureA,
                         MakeTheme("Gen A", SlideThemeType.General),
                         MakeTheme("Scripture B", SlideThemeType.ScriptureTheme),
                         MakeTheme("Scripture C", SlideThemeType.ScriptureTheme),
                     })
                playlist.Designs.Add(t);

            var item = new ScriptureItemInstance(playlist) { Title = "John 3" };
            var dock = new ItemEditDockRoot { DataContext = item };
            var window = new Window { Width = 600, Height = 500, Content = dock };
            window.Show();
            UiTestHelpers.Settle();

            var combo = OpenItemDockThemeCombo(dock);
            Assert.AreEqual("(None), #General, Gen A, #Scripture Themes, Scripture A, Scripture B, Scripture C", DescribeRows(combo),
                "(None) first and ungrouped, then general and scripture themes; no song themes");

            var rows = VisibleRows(combo);
            Assert.IsTrue(rows[0].IsEnabled, "(None) is a choice, not a header");
            Assert.IsTrue(BoundsIn(combo, rows[1]).Y >= BoundsIn(combo, rows[0]).Bottom - 0.5, "the General header starts below (None)");
            Assert.AreEqual(PositionIn(combo, rows[4]).Y, PositionIn(combo, rows[5]).Y, 0.5, "scripture tiles 1 and 2 share a row");
            Assert.AreEqual(PositionIn(combo, rows[4]).X, PositionIn(combo, rows[6]).X, 0.5, "scripture tile 3 starts a new row");

            window.Screenshot("theme-dropdown-item-dock-scripture-grouped");

            rows[4].ClickInPopup();
            Assert.AreEqual(scriptureA.Id, item.Design, "clicking a grouped tile did not set the scripture item's theme");

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
