// HandsLiftedApp.Tests/Models/SlideTheme/BaseSlideThemeBackgroundModeTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Models.SlideTheme;

[TestClass]
public class BaseSlideThemeBackgroundModeTests
{
    [TestMethod]
    public void BackgroundMode_DefaultsToPlain()
    {
        var theme = new BaseSlideTheme();

        Assert.AreEqual(ThemeBackgroundMode.Plain, theme.BackgroundMode);
    }

    [TestMethod]
    public void BackgroundMode_CanBeSetToMotionBackground()
    {
        var theme = new BaseSlideTheme { BackgroundMode = ThemeBackgroundMode.MotionBackground };

        Assert.AreEqual(ThemeBackgroundMode.MotionBackground, theme.BackgroundMode);
    }

    [TestMethod]
    public void DefaultMotionBackgroundVideoPath_DefaultsToNull()
    {
        var theme = new BaseSlideTheme();

        Assert.IsNull(theme.DefaultMotionBackgroundVideoPath);
    }

    [TestMethod]
    public void CopyFrom_CopiesBackgroundModeAndDefaultVideoPath()
    {
        var source = new BaseSlideTheme
        {
            BackgroundMode = ThemeBackgroundMode.MotionBackground,
            DefaultMotionBackgroundVideoPath = @"C:\Videos\bg.mp4"
        };
        var target = new BaseSlideTheme();

        target.CopyFrom(source);

        Assert.AreEqual(ThemeBackgroundMode.MotionBackground, target.BackgroundMode);
        Assert.AreEqual(@"C:\Videos\bg.mp4", target.DefaultMotionBackgroundVideoPath);
    }
}
