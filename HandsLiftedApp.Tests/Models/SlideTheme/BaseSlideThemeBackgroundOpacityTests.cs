// HandsLiftedApp.Tests/Models/SlideTheme/BaseSlideThemeBackgroundOpacityTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Models.SlideTheme;

[TestClass]
public class BaseSlideThemeBackgroundOpacityTests
{
    [TestMethod]
    public void BackgroundOpacity_DefaultsTo100()
    {
        var theme = new BaseSlideTheme();

        Assert.AreEqual(100, theme.BackgroundOpacity);
    }

    [TestMethod]
    public void BackgroundOpacity_ClampsToZeroAndHundred()
    {
        var theme = new BaseSlideTheme { BackgroundOpacity = 150 };
        Assert.AreEqual(100, theme.BackgroundOpacity);

        theme.BackgroundOpacity = -5;
        Assert.AreEqual(0, theme.BackgroundOpacity);
    }

    [TestMethod]
    public void CopyFrom_CopiesBackgroundOpacity()
    {
        var source = new BaseSlideTheme { BackgroundOpacity = 40 };
        var target = new BaseSlideTheme();

        target.CopyFrom(source);

        Assert.AreEqual(40, target.BackgroundOpacity);
    }
}
