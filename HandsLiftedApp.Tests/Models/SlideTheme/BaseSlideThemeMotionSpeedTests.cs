using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.SlideTheme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.SlideTheme;

[TestClass]
public class BaseSlideThemeMotionSpeedTests
{
    [TestMethod]
    public void MotionBackgroundSpeed_DefaultsTo1()
    {
        Assert.AreEqual(1.0, new BaseSlideTheme().MotionBackgroundSpeed);
    }

    [TestMethod]
    public void MotionBackgroundSpeed_ClampsToRange_AndRejectsNonFinite()
    {
        var theme = new BaseSlideTheme { MotionBackgroundSpeed = 50 };
        Assert.AreEqual(BaseSlideTheme.MaxMotionBackgroundSpeed, theme.MotionBackgroundSpeed);

        theme.MotionBackgroundSpeed = -1;
        Assert.AreEqual(BaseSlideTheme.MinMotionBackgroundSpeed, theme.MotionBackgroundSpeed);

        theme.MotionBackgroundSpeed = double.NaN;
        Assert.AreEqual(1.0, theme.MotionBackgroundSpeed);
    }

    [TestMethod]
    public void CopyFrom_CopiesMotionBackgroundSpeed()
    {
        var target = new BaseSlideTheme();
        target.CopyFrom(new BaseSlideTheme { MotionBackgroundSpeed = 0.5 });
        Assert.AreEqual(0.5, target.MotionBackgroundSpeed);
    }

    [TestMethod]
    public void MotionBackgroundSpeed_RoundTripsThroughXml()
    {
        var theme = new BaseSlideTheme { MotionBackgroundSpeed = 0.5 };
        Assert.IsTrue(SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var bytes));
        using var ms = new MemoryStream(bytes);
        Assert.IsTrue(SlideThemeXmlSerializer.TryDeserialize(ms, out var loaded));
        Assert.AreEqual(0.5, loaded!.MotionBackgroundSpeed);
    }

    [TestMethod]
    public void ThemeXmlWithoutMotionBackgroundSpeed_LoadsAs1()
    {
        var theme = new BaseSlideTheme { MotionBackgroundSpeed = 0.5 };
        Assert.IsTrue(SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var bytes));
        var xml = Regex.Replace(Encoding.UTF8.GetString(bytes),
            "<MotionBackgroundSpeed>[^<]*</MotionBackgroundSpeed>", "");

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        Assert.IsTrue(SlideThemeXmlSerializer.TryDeserialize(ms, out var loaded));
        Assert.AreEqual(1.0, loaded!.MotionBackgroundSpeed);
    }
}
