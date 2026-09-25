// HandsLiftedApp.Tests/Models/RuntimeData/Items/SongItemInstanceMotionBackgroundResolutionTests.cs
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Tests.TestSupport;

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

    // Regression test: the live motion-background layer (MotionBackgroundLayer) and the title
    // slide's video-thumbnail render both watch ResolvedMotionBackgroundVideoPath, not the raw
    // MotionBackgroundVideoPath, so that an item override or theme-default change actually reaches
    // playback. That only works if writing through the shared song's own video path also raises
    // ResolvedMotionBackgroundVideoPath - otherwise MotionBackgroundLayer's subscription never fires
    // for a plain song-video edit and playback goes stale.
    [TestMethod]
    public Task WritingThroughSharedSongMotionBackgroundVideoPath_RaisesResolvedPathChanged() => DispatcherTestThread.Run(async () =>
    {
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(null) { SongId = song.UUID };

        var raised = false;
        instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SongItemInstance.ResolvedMotionBackgroundVideoPath))
                raised = true;
        };

        instance.MotionBackgroundVideoPath = TempVideoPath("song-video.mp4");

        for (int i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (raised) break;
            await Task.Delay(50);
        }

        Assert.IsTrue(raised,
            "Writing the song's own MotionBackgroundVideoPath must raise ResolvedMotionBackgroundVideoPath so live " +
            "playback (MotionBackgroundLayer) and title-slide rendering pick up the change");
    });

    // Regression tests: the ItemEditDockRoot "Video" button binds to ShowMotionBackgroundVideoButton
    // (not just ResolvedDesignTheme's mode), because an override set while a MotionBackground theme
    // was assigned must stay visible/clearable even after the item's theme is later changed to Plain
    // or cleared - otherwise the override keeps silently applying with no way to see or remove it.
    [TestMethod]
    public void ShowMotionBackgroundVideoButton_PlainThemeNoOverride_IsFalse()
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme { BackgroundMode = ThemeBackgroundMode.Plain };
        playlist.Designs.Add(theme);
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(playlist) { SongId = song.UUID, Design = theme.Id };

        Assert.IsFalse(instance.ShowMotionBackgroundVideoButton);
    }

    [TestMethod]
    public void ShowMotionBackgroundVideoButton_NoThemeAssignedNoOverride_IsFalse()
    {
        var instance = new SongItemInstance(null) { SongId = Guid.NewGuid() };

        Assert.IsFalse(instance.ShowMotionBackgroundVideoButton);
    }

    [TestMethod]
    public void ShowMotionBackgroundVideoButton_MotionBackgroundTheme_IsTrue()
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme { BackgroundMode = ThemeBackgroundMode.MotionBackground };
        playlist.Designs.Add(theme);
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(playlist) { SongId = song.UUID, Design = theme.Id };

        Assert.IsTrue(instance.ShowMotionBackgroundVideoButton);
    }

    [TestMethod]
    public void ShowMotionBackgroundVideoButton_PlainThemeButOverrideSet_StaysTrue()
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme { BackgroundMode = ThemeBackgroundMode.Plain };
        playlist.Designs.Add(theme);
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(playlist)
        {
            SongId = song.UUID,
            Design = theme.Id,
            MotionBackgroundVideoOverride = TempVideoPath("item-override.mp4")
        };

        Assert.IsTrue(instance.ShowMotionBackgroundVideoButton,
            "An override set while a MotionBackground theme was assigned must stay visible/clearable even after the theme changes to Plain");
    }

    // Setting Design writes through NotifySharedSongChanged -> SongLibraryIndex.NotifyChanged, whose
    // subscription in the SongItemInstance constructor is .ObserveOn(RxSchedulers.MainThreadScheduler)
    // - so ResolvedDesignTheme (and this subscription's re-attachment to the new theme's Changed
    // observable) only updates once the dispatcher is pumped, exactly like
    // Disposing_UnsubscribesFromSharedSongChanged above. Must run on DispatcherTestThread with a
    // RunJobs poll, not as a plain synchronous test.
    [TestMethod]
    public Task ShowMotionBackgroundVideoButton_TogglingAssignedThemesOwnBackgroundMode_RaisesPropertyChanged() => DispatcherTestThread.Run(async () =>
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme { BackgroundMode = ThemeBackgroundMode.Plain };
        playlist.Designs.Add(theme);
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(playlist) { SongId = song.UUID, Design = theme.Id };

        // instance.ResolvedDesignTheme (a live getter over Design) already reads as `theme`
        // synchronously - Design write-through is immediate. But this test's own subscription
        // chain (WhenAnyValue(ResolvedDesignTheme).Switch()) only re-attaches to theme.Changed once
        // ResolvedDesignTheme's PropertyChanged notification itself fires, which is routed through
        // the dispatcher (RaiseForwardedPropertiesChanged, called from the ObserveOn(MainThreadScheduler)
        // SongChanged subscription) - so wait for that notification specifically, not just for the
        // value to match, before asserting on the toggle below.
        var resolvedDesignThemeNotified = false;
        instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SongItemInstance.ResolvedDesignTheme))
                resolvedDesignThemeNotified = true;
        };
        for (int i = 0; i < 20 && !resolvedDesignThemeNotified; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(50);
        }
        Assert.IsTrue(resolvedDesignThemeNotified, "Precondition: ResolvedDesignTheme change notification must have fired");
        Assert.AreSame(theme, instance.ResolvedDesignTheme, "Precondition: Design must have resolved to theme");

        var raised = false;
        instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SongItemInstance.ShowMotionBackgroundVideoButton))
                raised = true;
        };

        // Simulates editing the already-assigned theme's Background Mode in SlideThemeDesigner
        // while this item stays selected in ItemEditDockRoot - the button must react without the
        // item needing to be re-selected.
        theme.BackgroundMode = ThemeBackgroundMode.MotionBackground;

        Assert.IsTrue(raised,
            "Editing the currently-assigned theme's own BackgroundMode must re-raise ShowMotionBackgroundVideoButton");
    });
}
