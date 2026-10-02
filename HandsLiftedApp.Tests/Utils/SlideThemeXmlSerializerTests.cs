using System.IO;
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

    [TestMethod]
    public void TrySerializeToBytes_IsDeterministicForUnchangedTheme()
    {
        var theme = new BaseSlideTheme { Name = "Same" };

        SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var a);
        SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var b);

        CollectionAssert.AreEqual(a, b);
    }
}
