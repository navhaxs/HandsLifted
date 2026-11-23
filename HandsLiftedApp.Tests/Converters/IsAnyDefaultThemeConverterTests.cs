using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Converters;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Converters;

[TestClass]
public class IsAnyDefaultThemeConverterTests
{
    private readonly IsAnyDefaultThemeConverter _converter = new();

    [TestInitialize]
    public void Setup()
    {
        // The general default is read from the app preferences, which start with a fresh default theme.
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    // Values, in order: theme, app song, playlist song, app song-motion, playlist song-motion, app scripture, playlist scripture.
    private object? Run(params object?[] values) =>
        _converter.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

    [TestMethod]
    public void SongDefaultViaAppPreference_IsDefault()
    {
        var theme = new BaseSlideTheme();
        Assert.AreEqual(true, Run(theme, theme.Id, null, null, null, null, null));
    }

    [TestMethod]
    public void SongDefaultPlaylistOverride_BeatsAppPreference()
    {
        var appTheme = new BaseSlideTheme();
        var overrideTheme = new BaseSlideTheme();
        Assert.AreEqual(false, Run(appTheme, appTheme.Id, overrideTheme.Id, null, null, null, null),
            "the app-preference theme is not the default while a playlist override names another theme");
        Assert.AreEqual(true, Run(overrideTheme, appTheme.Id, overrideTheme.Id, null, null, null, null));
    }

    [TestMethod]
    public void SongMotionDefault_IsDefault_AndHonoursItsOwnOverride()
    {
        var appTheme = new BaseSlideTheme();
        var overrideTheme = new BaseSlideTheme();
        Assert.AreEqual(true, Run(appTheme, null, null, appTheme.Id, null, null, null));
        Assert.AreEqual(false, Run(appTheme, null, null, appTheme.Id, overrideTheme.Id, null, null));
        Assert.AreEqual(true, Run(overrideTheme, null, null, appTheme.Id, overrideTheme.Id, null, null));
    }

    [TestMethod]
    public void ScriptureDefault_IsDefault_AndHonoursItsOwnOverride()
    {
        var appTheme = new BaseSlideTheme();
        var overrideTheme = new BaseSlideTheme();
        Assert.AreEqual(true, Run(appTheme, null, null, null, null, appTheme.Id, null));
        Assert.AreEqual(false, Run(appTheme, null, null, null, null, appTheme.Id, overrideTheme.Id));
        Assert.AreEqual(true, Run(overrideTheme, null, null, null, null, appTheme.Id, overrideTheme.Id));
    }

    [TestMethod]
    public void OverrideOfOneKind_DoesNotAffectTheOthers()
    {
        // The playlist song override must not hide a theme that is the app's scripture default.
        var theme = new BaseSlideTheme();
        var other = new BaseSlideTheme();
        Assert.AreEqual(true, Run(theme, null, other.Id, null, null, theme.Id, null));
    }

    [TestMethod]
    public void GeneralDefault_IsDefault()
    {
        var theme = Globals.Instance.AppPreferences.DefaultTheme;
        Assert.AreEqual(true, Run(theme, null, null, null, null, null, null));
        Assert.AreEqual(true, Run(theme), "only the theme: the general default needs no ids");
    }

    [TestMethod]
    public void NotDefaultAnywhere_IsNotDefault()
    {
        var theme = new BaseSlideTheme();
        var other = Guid.NewGuid();
        Assert.AreEqual(false, Run(theme, other, other, other, other, other, other));
        Assert.AreEqual(false, Run(theme, null, null, null, null, null, null));
    }

    [TestMethod]
    public void ShortNullOrUnsetInput_IsNotDefault_AndNeverThrows()
    {
        var theme = new BaseSlideTheme();
        Assert.AreEqual(false, Run());
        Assert.AreEqual(false, Run(theme));
        Assert.AreEqual(false, Run(theme, Guid.NewGuid()));
        Assert.AreEqual(false, Run((object?)null));
        Assert.AreEqual(false, Run(AvaloniaProperty.UnsetValue, theme.Id, theme.Id, theme.Id, theme.Id, theme.Id, theme.Id),
            "no theme yet (binding source unavailable)");
        Assert.AreEqual(false, Run(theme, AvaloniaProperty.UnsetValue, AvaloniaProperty.UnsetValue, AvaloniaProperty.UnsetValue,
            AvaloniaProperty.UnsetValue, AvaloniaProperty.UnsetValue, AvaloniaProperty.UnsetValue));
        Assert.AreEqual(false, Run(theme, new BindingNotification(new InvalidOperationException("x")), "not a guid", 5, null, null, null));
        Assert.AreEqual(false, _converter.Convert(null!, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void UnsetOverride_FallsBackToTheAppDefault()
    {
        // While the playlist source is unavailable the binding delivers UnsetValue; the app default still applies, as in the text labels.
        var theme = new BaseSlideTheme();
        Assert.AreEqual(true, Run(theme, theme.Id, AvaloniaProperty.UnsetValue, null, null, null, null));
    }

    [TestMethod]
    public void NullAppPreferences_DoesNotThrow()
    {
        Globals.Instance.AppPreferences = null!;
        var theme = new BaseSlideTheme();
        Assert.AreEqual(false, Run(theme, null, null, null, null, null, null));
        Assert.AreEqual(true, Run(theme, theme.Id, null, null, null, null, null));
    }
}
