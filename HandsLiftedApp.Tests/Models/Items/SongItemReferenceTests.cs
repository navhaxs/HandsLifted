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
        public void RoundTrips_UUID_AndSongId_ThroughXml()
        {
            // UUID (this playlist item's own identity) and SongId (which library song it
            // points at) are deliberately independent — both must persist.
            var reference = new SongItemReference { UUID = Guid.NewGuid(), SongId = Guid.NewGuid() };

            var serializer = new XmlSerializer(typeof(SongItemReference));
            using var ms = new MemoryStream();
            serializer.Serialize(ms, reference);
            ms.Position = 0;
            var roundTripped = (SongItemReference)serializer.Deserialize(ms)!;

            Assert.AreEqual(reference.UUID, roundTripped.UUID);
            Assert.AreEqual(reference.SongId, roundTripped.SongId);
            Assert.AreNotEqual(roundTripped.UUID, roundTripped.SongId);
        }

        [TestMethod]
        public void Clone_PreservesSongId_ButAssignsFreshUUID()
        {
            // SongItemReference no longer overrides Clone(): the inherited base Item.Clone()
            // XML-round-trips (preserving SongId, so the duplicate still points at the same
            // library song) and then reassigns UUID (so the duplicate is a distinct playlist
            // item — PlaylistInstance.NavigateToReference and SlideThumbnailBehavior both key
            // off UUID and would resolve to the wrong item if the two collided).
            var reference = new SongItemReference { SongId = Guid.NewGuid(), SlideTransitionDurationMs = 250 };

            var clone = (SongItemReference)reference.Clone();

            Assert.AreEqual(reference.SongId, clone.SongId);
            Assert.AreNotEqual(reference.UUID, clone.UUID);
            Assert.AreEqual(250.0, clone.SlideTransitionDurationMs);
        }
    }
}
