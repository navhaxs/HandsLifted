using Avalonia.Media;
using HandsLiftedApp.Data.Data.Models.Types;
using HandsLiftedApp.Data.SlideTheme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.SlideTheme;

[TestClass]
public class BaseSlideThemeCopyFromTests
{
    [DataTestMethod]
    [DataRow(100)]
    [DataRow(300)]
    [DataRow(400)]
    [DataRow(500)]
    [DataRow(600)]
    [DataRow(700)]
    [DataRow(900)]
    public void CopyFrom_KeepsEveryFontWeight(int weight)
    {
        var source = new BaseSlideTheme { FontWeight = (XmlFontWeight)(FontWeight)weight };
        var copy = new BaseSlideTheme();

        copy.CopyFrom(source);

        Assert.AreEqual(weight, (int)copy.FontWeight);
    }

    [TestMethod]
    public void CopyFrom_KeepsObliqueFontStyle_AndOtherSettings()
    {
        var source = new BaseSlideTheme
        {
            FontStyle = FontStyle.Oblique,
            TextAlignment = TextAlignment.Right,
            TitleTextAlignment = TextAlignment.Left,
            FontSize = 77,
            Name = "Source",
        };
        var copy = new BaseSlideTheme();

        copy.CopyFrom(source);

        Assert.AreEqual(FontStyle.Oblique, copy.FontStyle);
        Assert.AreEqual(TextAlignment.Right, copy.TextAlignment);
        Assert.AreEqual(TextAlignment.Left, copy.TitleTextAlignment);
        Assert.AreEqual(77, copy.FontSize);
        Assert.AreEqual("Source", copy.Name);
        Assert.AreEqual(source.Id, copy.Id);
    }
}
