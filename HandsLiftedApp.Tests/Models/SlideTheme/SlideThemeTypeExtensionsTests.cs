using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Models.SlideTheme;

[TestClass]
public class SlideThemeTypeExtensionsTests
{
    [DataTestMethod]
    [DataRow(SlideThemeType.General, true)]
    [DataRow(SlideThemeType.SongTheme, true)]
    [DataRow(SlideThemeType.ScriptureTheme, false)]
    public void AppliesToSongs_MatchesGeneralAndSongTheme(SlideThemeType type, bool expected)
    {
        var theme = new BaseSlideTheme { Type = type };

        Assert.AreEqual(expected, theme.AppliesToSongs());
    }

    [DataTestMethod]
    [DataRow(SlideThemeType.General, true)]
    [DataRow(SlideThemeType.SongTheme, false)]
    [DataRow(SlideThemeType.ScriptureTheme, true)]
    public void AppliesToScripture_MatchesGeneralAndScriptureTheme(SlideThemeType type, bool expected)
    {
        var theme = new BaseSlideTheme { Type = type };

        Assert.AreEqual(expected, theme.AppliesToScripture());
    }
}
