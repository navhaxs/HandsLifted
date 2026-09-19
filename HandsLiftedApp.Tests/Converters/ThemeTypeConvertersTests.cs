using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Converters;
using HandsLiftedApp.Data.SlideTheme;
using System.Globalization;

namespace HandsLiftedApp.Tests.Converters;

[TestClass]
public class ThemeTypeConvertersTests
{
    [DataTestMethod]
    [DataRow(SlideThemeType.General, true)]
    [DataRow(SlideThemeType.SongTheme, false)]
    [DataRow(SlideThemeType.ScriptureTheme, false)]
    public void IsGeneralThemeTypeConverter_MatchesOnlyGeneral(SlideThemeType type, bool expected)
    {
        var converter = new IsGeneralThemeTypeConverter();

        var result = converter.Convert(type, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow(SlideThemeType.General, false)]
    [DataRow(SlideThemeType.SongTheme, true)]
    [DataRow(SlideThemeType.ScriptureTheme, false)]
    public void IsSongThemeTypeConverter_MatchesOnlySongTheme(SlideThemeType type, bool expected)
    {
        var converter = new IsSongThemeTypeConverter();

        var result = converter.Convert(type, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow(SlideThemeType.General, false)]
    [DataRow(SlideThemeType.SongTheme, false)]
    [DataRow(SlideThemeType.ScriptureTheme, true)]
    public void IsScriptureThemeTypeConverter_MatchesOnlyScriptureTheme(SlideThemeType type, bool expected)
    {
        var converter = new IsScriptureThemeTypeConverter();

        var result = converter.Convert(type, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void AppliesToSongsVisibilityConverter_NullIsAlwaysVisible()
    {
        var converter = new AppliesToSongsVisibilityConverter();

        var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(true, result);
    }

    [DataTestMethod]
    [DataRow(SlideThemeType.General, true)]
    [DataRow(SlideThemeType.SongTheme, true)]
    [DataRow(SlideThemeType.ScriptureTheme, false)]
    public void AppliesToSongsVisibilityConverter_FiltersByType(SlideThemeType type, bool expected)
    {
        var converter = new AppliesToSongsVisibilityConverter();
        var theme = new BaseSlideTheme { Type = type };

        var result = converter.Convert(theme, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void AppliesToScriptureVisibilityConverter_NullIsAlwaysVisible()
    {
        var converter = new AppliesToScriptureVisibilityConverter();

        var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(true, result);
    }

    [DataTestMethod]
    [DataRow(SlideThemeType.General, true)]
    [DataRow(SlideThemeType.SongTheme, false)]
    [DataRow(SlideThemeType.ScriptureTheme, true)]
    public void AppliesToScriptureVisibilityConverter_FiltersByType(SlideThemeType type, bool expected)
    {
        var converter = new AppliesToScriptureVisibilityConverter();
        var theme = new BaseSlideTheme { Type = type };

        var result = converter.Convert(theme, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }
}
