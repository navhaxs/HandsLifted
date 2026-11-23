using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Controls;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.Views.Designer;
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
}
