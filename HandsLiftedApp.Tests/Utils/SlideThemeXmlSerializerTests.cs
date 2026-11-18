using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.SlideTheme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Utils;

[TestClass]
public class SlideThemeXmlSerializerTests
{
    [TestMethod]
    public void TrySerializeToBytes_RoundTripsThroughTryDeserialize()
    {
        var theme = new BaseSlideTheme { Name = "Bytes Theme" };

        Assert.IsTrue(SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var bytes));
        using var ms = new MemoryStream(bytes);
        Assert.IsTrue(SlideThemeXmlSerializer.TryDeserialize(ms, out var loaded));

        Assert.AreEqual(theme.Id, loaded!.Id);
        Assert.AreEqual("Bytes Theme", loaded.Name);
    }

    // Serializes a theme with BackgroundOpacity 40, then rewrites its XML element: removed when
    // replacementValue is null, otherwise given that raw value. Returns the deserialized theme.
    private static BaseSlideTheme RoundTripWithOpacityElement(string? replacementValue)
    {
        var theme = new BaseSlideTheme { Name = "Opacity", BackgroundOpacity = 40 };
        Assert.IsTrue(SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var bytes));
        var xml = Encoding.UTF8.GetString(bytes);
        var element = Regex.Match(xml, "<BackgroundOpacity>-?\\d+</BackgroundOpacity>");
        Assert.IsTrue(element.Success, "serialized theme should contain a BackgroundOpacity element");
        xml = xml.Replace(element.Value, replacementValue == null
            ? ""
            : $"<BackgroundOpacity>{replacementValue}</BackgroundOpacity>");

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        Assert.IsTrue(SlideThemeXmlSerializer.TryDeserialize(ms, out var loaded));
        return loaded!;
    }

    [TestMethod]
    public void BackgroundOpacity_RoundTripsThroughXml()
    {
        var theme = new BaseSlideTheme { BackgroundOpacity = 40 };

        Assert.IsTrue(SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var bytes));
        using var ms = new MemoryStream(bytes);
        Assert.IsTrue(SlideThemeXmlSerializer.TryDeserialize(ms, out var loaded));

        Assert.AreEqual(40, loaded!.BackgroundOpacity);
    }

    [TestMethod]
    public void ThemeXmlWithoutBackgroundOpacity_LoadsAs100()
    {
        var loaded = RoundTripWithOpacityElement(null);

        Assert.AreEqual(100, loaded.BackgroundOpacity);
    }

    [TestMethod]
    public void ThemeXmlWithOutOfRangeBackgroundOpacity_IsClamped()
    {
        Assert.AreEqual(100, RoundTripWithOpacityElement("250").BackgroundOpacity);
        Assert.AreEqual(0, RoundTripWithOpacityElement("-3").BackgroundOpacity);
    }

    [TestMethod]
    public void TrySerializeToBytes_IsDeterministicForUnchangedTheme()
    {
        var theme = new BaseSlideTheme { Name = "Same" };

        SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var a);
        SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var b);

        CollectionAssert.AreEqual(a, b);
    }
}
