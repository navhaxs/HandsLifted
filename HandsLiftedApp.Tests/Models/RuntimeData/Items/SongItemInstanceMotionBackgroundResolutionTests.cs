// HandsLiftedApp.Tests/Models/RuntimeData/Items/SongItemInstanceMotionBackgroundResolutionTests.cs
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class SongItemInstanceMotionBackgroundResolutionTests
{
    private static string TempVideoPath(string fileName) => Path.Combine(Path.GetTempPath(), fileName);

    [TestMethod]
    public void ResolvedMotionBackgroundVideoPath_NoOverrideNoSongVideoNoTheme_ReturnsNull()
    {
        var instance = new SongItemInstance(null) { SongId = Guid.NewGuid() };

        Assert.IsNull(instance.ResolvedMotionBackgroundVideoPath);
        Assert.IsFalse(instance.HasMotionBackground);
    }

    [TestMethod]
    public void ResolvedMotionBackgroundVideoPath_ThemeMotionBackgroundWithDefaultVideo_UsesThemeDefault()
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme
        {
            BackgroundMode = ThemeBackgroundMode.MotionBackground,
            DefaultMotionBackgroundVideoPath = TempVideoPath("theme-default.mp4")
        };
        playlist.Designs.Add(theme);
        // Design's setter only writes through when ResolvedSong is non-null (see
        // SongItemInstance.Design), so an unregistered SongId would silently leave Design at
        // Guid.Empty and never resolve the theme - register a song first, as the other tests below do.
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(playlist) { SongId = song.UUID, Design = theme.Id };

        Assert.AreEqual(TempVideoPath("theme-default.mp4"), instance.ResolvedMotionBackgroundVideoPath);
        Assert.IsTrue(instance.HasMotionBackground);
    }

    [TestMethod]
    public void ResolvedMotionBackgroundVideoPath_SongOwnVideoSet_TakesPriorityOverThemeDefault()
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme
        {
            BackgroundMode = ThemeBackgroundMode.MotionBackground,
            DefaultMotionBackgroundVideoPath = TempVideoPath("theme-default.mp4")
        };
        playlist.Designs.Add(theme);
        var song = new SongItem { Title = "Amazing Grace", MotionBackgroundVideoPath = TempVideoPath("song-video.mp4") };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(playlist) { SongId = song.UUID, Design = theme.Id };

        Assert.AreEqual(TempVideoPath("song-video.mp4"), instance.ResolvedMotionBackgroundVideoPath);
    }

    [TestMethod]
    public void ResolvedMotionBackgroundVideoPath_OverrideSet_TakesPriorityOverSongVideoAndThemeDefault()
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme
        {
            BackgroundMode = ThemeBackgroundMode.MotionBackground,
            DefaultMotionBackgroundVideoPath = TempVideoPath("theme-default.mp4")
        };
        playlist.Designs.Add(theme);
        var song = new SongItem { Title = "Amazing Grace", MotionBackgroundVideoPath = TempVideoPath("song-video.mp4") };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(playlist)
        {
            SongId = song.UUID,
            Design = theme.Id,
            MotionBackgroundVideoOverride = TempVideoPath("item-override.mp4")
        };

        Assert.AreEqual(TempVideoPath("item-override.mp4"), instance.ResolvedMotionBackgroundVideoPath);
    }

    [TestMethod]
    public void ResolvedMotionBackgroundVideoPath_ThemeIsPlain_IgnoresThemeDefaultVideo()
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme
        {
            BackgroundMode = ThemeBackgroundMode.Plain,
            DefaultMotionBackgroundVideoPath = TempVideoPath("theme-default.mp4")
        };
        playlist.Designs.Add(theme);
        // Register a song so Design actually resolves to this theme (see the comment in the
        // MotionBackground-mode test above) - otherwise this assertion would pass vacuously
        // because ResolvedDesignTheme never resolves at all, not because BackgroundMode was checked.
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(playlist) { SongId = song.UUID, Design = theme.Id };

        Assert.IsNull(instance.ResolvedMotionBackgroundVideoPath,
            "A Plain-mode theme's DefaultMotionBackgroundVideoPath must never be used, even if set");
    }
}
