// HandsLiftedApp.Tests/Converters/MotionBackgroundThemeConvertersTests.cs
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Converters;

[TestClass]
public class MotionBackgroundThemeConvertersTests
{
    [DataTestMethod]
    [DataRow(ThemeBackgroundMode.Plain, false)]
    [DataRow(ThemeBackgroundMode.MotionBackground, true)]
    public void IsMotionBackgroundModeConverter_MatchesOnlyMotionBackground(ThemeBackgroundMode mode, bool expected)
    {
        var converter = new IsMotionBackgroundModeConverter();

        var result = converter.Convert(mode, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void IsMotionBackgroundThemeConverter_Null_ReturnsFalse()
    {
        var converter = new IsMotionBackgroundThemeConverter();

        var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(false, result);
    }

    [TestMethod]
    public void IsMotionBackgroundThemeConverter_PlainTheme_ReturnsFalse()
    {
        var converter = new IsMotionBackgroundThemeConverter();
        var theme = new BaseSlideTheme { BackgroundMode = ThemeBackgroundMode.Plain };

        var result = converter.Convert(theme, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(false, result);
    }

    [TestMethod]
    public void IsMotionBackgroundThemeConverter_MotionBackgroundTheme_ReturnsTrue()
    {
        var converter = new IsMotionBackgroundThemeConverter();
        var theme = new BaseSlideTheme { BackgroundMode = ThemeBackgroundMode.MotionBackground };

        var result = converter.Convert(theme, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(true, result);
    }
}
