using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Tests.Models.Items;

[TestClass]
public class ItemTests
{
    [TestMethod]
    public void SlideTransitionDurationMs_DefaultsToNull()
    {
        var item = new BlankItem();

        Assert.IsNull(item.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void SlideTransitionDurationMs_CanBeSetAndRead()
    {
        var item = new BlankItem();

        item.SlideTransitionDurationMs = 500.0;

        Assert.AreEqual(500.0, item.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void SongItem_UUID_RoundTripsThroughXmlSerialization()
    {
        var original = new SongItem { Title = "Amazing Grace" };
        var originalUuid = original.UUID;

        var serializer = new XmlSerializer(typeof(SongItem));
        using var ms = new MemoryStream();
        serializer.Serialize(ms, original);
        ms.Position = 0;
        var roundTripped = (SongItem)serializer.Deserialize(ms)!;

        Assert.AreEqual(originalUuid, roundTripped.UUID);
    }

    [TestMethod]
    public void Item_Clone_StillAssignsFreshUUID()
    {
        var original = new SongItem { Title = "Amazing Grace" };
        var clone = (SongItem)original.Clone();

        Assert.AreNotEqual(original.UUID, clone.UUID);
    }
}
