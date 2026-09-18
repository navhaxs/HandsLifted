using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.SlideTheme;
using System.IO;
using System.Text;

namespace HandsLiftedApp.Tests.Models.SlideTheme;

[TestClass]
public class BaseSlideThemeTypeTests
{
    [TestMethod]
    public void Type_DefaultsToGeneral()
    {
        var theme = new BaseSlideTheme();

        Assert.AreEqual(SlideThemeType.General, theme.Type);
    }

    [TestMethod]
    public void Type_CanBeSetToSongTheme()
    {
        var theme = new BaseSlideTheme { Type = SlideThemeType.SongTheme };

        Assert.AreEqual(SlideThemeType.SongTheme, theme.Type);
    }

    [TestMethod]
    public void TryDeserialize_XmlWithNoTypeElement_DefaultsToGeneral()
    {
        const string xml = "<?xml version=\"1.0\"?><BaseSlideTheme><Name>Old Theme</Name></BaseSlideTheme>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var ok = SlideThemeXmlSerializer.TryDeserialize(stream, out var theme);

        Assert.IsTrue(ok);
        Assert.IsNotNull(theme);
        Assert.AreEqual(SlideThemeType.General, theme!.Type);
        Assert.AreEqual("Old Theme", theme.Name);
    }

    [TestMethod]
    public void CopyFrom_CopiesType()
    {
        var source = new BaseSlideTheme { Type = SlideThemeType.ScriptureTheme };
        var target = new BaseSlideTheme();

        target.CopyFrom(source);

        Assert.AreEqual(SlideThemeType.ScriptureTheme, target.Type);
    }
}
