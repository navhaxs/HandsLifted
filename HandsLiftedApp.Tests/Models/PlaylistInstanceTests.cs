using System;
using System.Reactive;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Models;

[TestClass]
public class PlaylistInstanceTests
{
    [TestInitialize]
    public void Setup()
    {
        // ResolveSongTheme/ResolveScriptureTheme fall back to Globals.Instance.AppPreferences,
        // which is null unless Globals.OnStartup() has run. Matches the convention already used
        // by ScriptureItemInstanceTests.Setup().
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    private static BaseSlideTheme MakeTheme(string name) => new BaseSlideTheme { Name = name };

    [TestMethod]
    public void ResolveSongTheme_ExplicitDesignPresent_ReturnsExplicitTheme()
    {
        var playlist = new PlaylistInstance();
        var explicitTheme = MakeTheme("Explicit");
        var songDefault = MakeTheme("SongDefault");
        playlist.Designs.Add(explicitTheme);
        playlist.Designs.Add(songDefault);
        playlist.DefaultSongThemeId = songDefault.Id;

        var result = playlist.ResolveSongTheme(explicitTheme.Id, hasMotionBackground: false);

        Assert.AreSame(explicitTheme, result);
    }

    [TestMethod]
    public void ResolveSongTheme_ExplicitDesignMissingFromDesigns_FallsBackToCategoryDefault()
    {
        var playlist = new PlaylistInstance();
        var songDefault = MakeTheme("SongDefault");
        playlist.Designs.Add(songDefault);
        playlist.DefaultSongThemeId = songDefault.Id;

        // explicitDesignId points at a theme that no longer exists in Designs (e.g. deleted)
        var result = playlist.ResolveSongTheme(Guid.NewGuid(), hasMotionBackground: false);

        Assert.AreSame(songDefault, result);
    }

    [TestMethod]
    public void ResolveSongTheme_NoExplicit_NoMotionBackground_ReturnsSongDefault()
    {
        var playlist = new PlaylistInstance();
        var songDefault = MakeTheme("SongDefault");
        var motionDefault = MakeTheme("MotionDefault");
        playlist.Designs.Add(songDefault);
        playlist.Designs.Add(motionDefault);
        playlist.DefaultSongThemeId = songDefault.Id;
        playlist.DefaultSongMotionThemeId = motionDefault.Id;

        var result = playlist.ResolveSongTheme(Guid.Empty, hasMotionBackground: false);

        Assert.AreSame(songDefault, result);
    }

    [TestMethod]
    public void ResolveSongTheme_NoExplicit_WithMotionBackground_ReturnsMotionDefault()
    {
        var playlist = new PlaylistInstance();
        var songDefault = MakeTheme("SongDefault");
        var motionDefault = MakeTheme("MotionDefault");
        playlist.Designs.Add(songDefault);
        playlist.Designs.Add(motionDefault);
        playlist.DefaultSongThemeId = songDefault.Id;
        playlist.DefaultSongMotionThemeId = motionDefault.Id;

        var result = playlist.ResolveSongTheme(Guid.Empty, hasMotionBackground: true);

        Assert.AreSame(motionDefault, result);
    }

    [TestMethod]
    public void ResolveSongTheme_CategoryDefaultUnset_FallsBackToAppDefault()
    {
        var playlist = new PlaylistInstance();

        var result = playlist.ResolveSongTheme(Guid.Empty, hasMotionBackground: false);

        Assert.AreSame(Globals.Instance.AppPreferences.DefaultTheme, result);
    }

    [TestMethod]
    public void ResolveSongTheme_CategoryDefaultPointsToDeletedTheme_FallsBackToAppDefault()
    {
        var playlist = new PlaylistInstance();
        playlist.DefaultSongThemeId = Guid.NewGuid(); // not present in Designs

        var result = playlist.ResolveSongTheme(Guid.Empty, hasMotionBackground: false);

        Assert.AreSame(Globals.Instance.AppPreferences.DefaultTheme, result);
    }

    [TestMethod]
    public void ResolveScriptureTheme_ExplicitDesignPresent_ReturnsExplicitTheme()
    {
        var playlist = new PlaylistInstance();
        var explicitTheme = MakeTheme("Explicit");
        var scriptureDefault = MakeTheme("ScriptureDefault");
        playlist.Designs.Add(explicitTheme);
        playlist.Designs.Add(scriptureDefault);
        playlist.DefaultScriptureThemeId = scriptureDefault.Id;

        var result = playlist.ResolveScriptureTheme(explicitTheme.Id);

        Assert.AreSame(explicitTheme, result);
    }

    [TestMethod]
    public void ResolveScriptureTheme_NoExplicit_ReturnsScriptureDefault()
    {
        var playlist = new PlaylistInstance();
        var scriptureDefault = MakeTheme("ScriptureDefault");
        playlist.Designs.Add(scriptureDefault);
        playlist.DefaultScriptureThemeId = scriptureDefault.Id;

        var result = playlist.ResolveScriptureTheme(Guid.Empty);

        Assert.AreSame(scriptureDefault, result);
    }

    [TestMethod]
    public void ResolveScriptureTheme_DefaultUnset_FallsBackToAppDefault()
    {
        var playlist = new PlaylistInstance();

        var result = playlist.ResolveScriptureTheme(Guid.Empty);

        Assert.AreSame(Globals.Instance.AppPreferences.DefaultTheme, result);
    }

    [TestMethod]
    public void DefaultThemeAssignmentsChanged_FiresWhenAnyOfTheThreeIdsChange()
    {
        var playlist = new PlaylistInstance();
        var fireCount = 0;
        playlist.DefaultThemeAssignmentsChanged.Subscribe(_ => fireCount++);

        playlist.DefaultSongThemeId = Guid.NewGuid();
        playlist.DefaultSongMotionThemeId = Guid.NewGuid();
        playlist.DefaultScriptureThemeId = Guid.NewGuid();

        Assert.AreEqual(3, fireCount);
    }

    [TestMethod]
    public void DefaultThemeAssignmentsChanged_DoesNotFireOnSubscribe()
    {
        var playlist = new PlaylistInstance();
        var fireCount = 0;
        playlist.DefaultThemeAssignmentsChanged.Subscribe(_ => fireCount++);

        Assert.AreEqual(0, fireCount);
    }
}
