using System;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace HandsLiftedApp.Tests.ViewModels;

[TestClass]
public class AppPreferencesViewModelTests
{
    [TestMethod]
    public void ThemeSettings_RoundTripThroughJson()
    {
        var song = Guid.NewGuid();
        var motion = Guid.NewGuid();
        var scripture = Guid.NewGuid();
        var prefs = new AppPreferencesViewModel
        {
            SlideThemesPath = @"C:\Some\Themes",
            DefaultSongThemeId = song,
            DefaultSongMotionThemeId = motion,
            DefaultScriptureThemeId = scripture
        };

        var json = JsonConvert.SerializeObject(prefs);
        var loaded = JsonConvert.DeserializeObject<AppPreferencesViewModel>(json)!;

        Assert.AreEqual(@"C:\Some\Themes", loaded.SlideThemesPath);
        Assert.AreEqual(song, loaded.DefaultSongThemeId);
        Assert.AreEqual(motion, loaded.DefaultSongMotionThemeId);
        Assert.AreEqual(scripture, loaded.DefaultScriptureThemeId);
    }

    [TestMethod]
    public void OldAppState_WithoutThemeSettings_GetsDefaults()
    {
        var loaded = JsonConvert.DeserializeObject<AppPreferencesViewModel>("{}")!;

        Assert.IsFalse(string.IsNullOrWhiteSpace(loaded.SlideThemesPath));
        Assert.IsNull(loaded.DefaultSongThemeId);
    }

    [TestMethod]
    public void ResolveSlideThemesFolder_BlankFallsBackToDefault()
    {
        var def = Globals.ResolveSlideThemesFolder(null);

        Assert.IsFalse(string.IsNullOrWhiteSpace(def));
        Assert.AreEqual(def, Globals.ResolveSlideThemesFolder("  "));
        Assert.AreEqual(@"C:\X", Globals.ResolveSlideThemesFolder(@"C:\X"));
    }
}
