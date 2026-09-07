using System;
using System.IO;
using System.Xml.Serialization;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.Items
{
    [TestClass]
    public class SongItemReferenceTests
    {
        [TestMethod]
        public void RoundTrips_UUID_ThroughXml()
        {
            var reference = new SongItemReference { UUID = Guid.NewGuid() };

            var serializer = new XmlSerializer(typeof(SongItemReference));
            using var ms = new MemoryStream();
            serializer.Serialize(ms, reference);
            ms.Position = 0;
            var roundTripped = (SongItemReference)serializer.Deserialize(ms)!;

            Assert.AreEqual(reference.UUID, roundTripped.UUID);
        }
    }
}
