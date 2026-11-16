using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models;

/// <summary>
/// Deleting a theme (or putting it back with undo) must reach everything that resolved it: a song or scripture
/// item pointing at it by Id, and the slides drawn with it. Regression: a song kept using a deleted theme.
/// </summary>
[TestClass]
public class ThemeLibraryChangeTests
{
    private static async Task Pump(Func<bool> done)
    {
        for (var i = 0; i < 40; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (done()) return;
            await Task.Delay(25);
        }
    }

    [TestMethod]
    public Task ThemeLibraryChanged_FiresWhenAThemeIsAddedOrRemoved_ButNotWhenOneIsEdited() => DispatcherTestThread.Run(() =>
    {
        var playlist = new PlaylistInstance();
        var emissions = 0;
        playlist.ThemeLibraryChanged.Subscribe(_ => emissions++);

        var theme = new BaseSlideTheme { Name = "A" };
        playlist.Designs.Add(theme);
        Assert.AreEqual(1, emissions);

        theme.FontSize = 33; // an edit, not a change of which themes exist
        Assert.AreEqual(1, emissions);

        playlist.Designs.Remove(theme);
        Assert.AreEqual(2, emissions);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task SongItem_PointingAtADeletedTheme_RaisesResolvedDesignThemeChanged() => DispatcherTestThread.Run(async () =>
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme { Name = "Gone" };
        playlist.Designs.Add(theme);
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var item = new SongItemInstance(playlist) { SongId = song.UUID, Design = theme.Id };
        await Pump(() => ReferenceEquals(item.ResolvedDesignTheme, theme));
        Assert.AreSame(theme, item.ResolvedDesignTheme);

        var raised = false;
        item.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(SongItemInstance.ResolvedDesignTheme);
        playlist.Designs.Remove(theme);
        await Pump(() => raised);

        Assert.IsTrue(raised, "the item's theme picker must find out its theme is gone");
        Assert.IsNull(item.ResolvedDesignTheme);
    });

    [TestMethod]
    public Task ScriptureItem_PointingAtADeletedTheme_RaisesExplicitDesignThemeChanged() => DispatcherTestThread.Run(() =>
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme { Name = "Gone" };
        playlist.Designs.Add(theme);
        var scripture = new ScriptureItemInstance(playlist) { Book = "JHN", Translation = "BSB", Design = theme.Id };

        var raised = false;
        scripture.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(ScriptureItemInstance.ExplicitDesignTheme);
        playlist.Designs.Remove(theme);

        Assert.IsTrue(raised);
        Assert.IsNull(scripture.ExplicitDesignTheme);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task SongSlide_StopsUsingADeletedTheme_AndPicksUpTheRestoredOne() => DispatcherTestThread.Run(async () =>
    {
        var previous = Globals.Instance.MainViewModel;
        try
        {
            Globals.Instance.MainViewModel = new MainViewModel();
            var playlist = Globals.Instance.MainViewModel.Playlist;

            var theme = new BaseSlideTheme { Name = "Custom", FontSize = 77 };
            playlist.Designs.Add(theme);
            var song = new SongItem { Title = "Amazing Grace" };
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
            var item = new SongItemInstance(playlist) { SongId = song.UUID, Design = theme.Id };
            var slide = new SongSlideInstance(item, null, "slide", "text");
            await Pump(() => ReferenceEquals(slide.Theme, theme));
            Assert.AreSame(theme, slide.Theme, "the slide starts out drawn with the song's theme");

            // delete the theme
            playlist.Designs.Remove(theme);
            await Pump(() => !ReferenceEquals(slide.Theme, theme));
            Assert.AreNotSame(theme, slide.Theme, "the slide must not keep drawing with a theme that was deleted");

            // undo of the delete puts back a NEW theme object with the same Id
            var restored = new BaseSlideTheme();
            restored.CopyFrom(theme);
            playlist.Designs.Add(restored);
            await Pump(() => ReferenceEquals(slide.Theme, restored));
            Assert.AreSame(restored, slide.Theme,
                "the slide must use the restored theme, or later edits to that theme would never reach it");
        }
        finally
        {
            Globals.Instance.MainViewModel = previous;
        }
    });
}
