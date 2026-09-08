using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.Library
{
    [TestClass]
    public class SongLibraryIndexTests
    {
        private string _libraryDir = "";

        [TestInitialize]
        public void Setup()
        {
            _libraryDir = Path.Combine(Path.GetTempPath(), "SongLibraryIndexTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_libraryDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_libraryDir))
                    Directory.Delete(_libraryDir, recursive: true);
            }
            catch (IOException)
            {
                // NotifyChanged's debounced (500ms) write-through save can still land in this
                // directory after a test body returns; losing the temp-dir cleanup is harmless
                // and must not fail an otherwise-passing test.
            }
        }

        [TestMethod]
        public void Resolve_UnknownId_ReturnsNull()
        {
            var index = new SongLibraryIndex();
            Assert.IsNull(index.Resolve(Guid.NewGuid()));
        }

        [TestMethod]
        public void Register_ThenResolve_ReturnsSameObject()
        {
            var index = new SongLibraryIndex();
            var song = new SongItem { Title = "Amazing Grace" };
            var filePath = Path.Combine(_libraryDir, "Amazing Grace.xml");

            index.Register(song, filePath, _libraryDir);

            Assert.AreSame(song, index.Resolve(song.UUID));
        }

        [TestMethod]
        public void ImportAndCache_WritesFileAndMakesItResolvable()
        {
            var index = new SongLibraryIndex();
            var song = new SongItem { Title = "New Song" };

            index.ImportAndCache(song, _libraryDir);

            Assert.AreSame(song, index.Resolve(song.UUID));
            var expectedPath = Path.Combine(_libraryDir, "New Song.xml");
            Assert.IsTrue(File.Exists(expectedPath), $"Expected file at {expectedPath}");
        }

        [TestMethod]
        public void Register_ExistingUUID_KeepsOriginalObjectIdentity()
        {
            var index = new SongLibraryIndex();
            var first = new SongItem { Title = "First" };
            index.Register(first, Path.Combine(_libraryDir, "song.xml"), _libraryDir);

            var second = new SongItem { UUID = first.UUID, Title = "Second (re-parsed copy)" };
            index.Register(second, Path.Combine(_libraryDir, "song.xml"), _libraryDir);

            Assert.AreSame(first, index.Resolve(first.UUID));
        }

        // A song XML file written before Item.UUID started persisting to XML has no <UUID>
        // element. XmlSerializer.Deserialize runs the parameterless Item() constructor first
        // (which assigns Guid.NewGuid()) and only overwrites properties it finds elements for,
        // so every parse of such a file yields a DIFFERENT UUID — not just across restarts,
        // but on every library rescan within a single session. RegisterFromScan pins the
        // identity to the file path so a re-parse of the same file keeps the first-seen UUID.
        //
        // Simulated here by calling RegisterFromScan twice for the same filePath with two
        // separately-constructed SongItems, each carrying its own fresh constructor-assigned
        // UUID — exactly what two parses of one legacy file produce.
        [TestMethod]
        public void RegisterFromScan_SameFilePath_ReparsedWithDifferentUUID_KeepsFirstSeenIdentity()
        {
            var index = new SongLibraryIndex();
            var filePath = Path.Combine(_libraryDir, "legacy-song.xml");

            var firstParse = new SongItem { Title = "Legacy Song" };
            var firstSeenId = firstParse.UUID;
            index.RegisterFromScan(firstParse, filePath, _libraryDir);

            var secondParse = new SongItem { Title = "Legacy Song" };
            var secondParseOriginalId = secondParse.UUID;
            Assert.AreNotEqual(firstSeenId, secondParseOriginalId,
                "Precondition: the two simulated parses must start with different UUIDs");

            index.RegisterFromScan(secondParse, filePath, _libraryDir);

            Assert.AreEqual(firstSeenId, secondParse.UUID,
                "The re-parsed song's UUID must be corrected back to the first-seen identity for this file");
            Assert.IsNotNull(index.Resolve(firstSeenId),
                "The song must still resolve under the stable, first-seen UUID after a rescan");
            Assert.AreSame(firstParse, index.Resolve(firstSeenId),
                "Register's existing-object-identity-wins rule must keep the originally cached object");
            Assert.IsNull(index.Resolve(secondParseOriginalId),
                "The re-parse's throwaway constructor-assigned UUID must never become a resolvable identity");
        }

        // RegisterFromScan must only stamp (write back to disk / emit SongChanged) a file that
        // genuinely lacks a persisted <UUID> element. A file that already has one is not a
        // legacy pre-migration file — treating "first seen this session" alone as the stamp
        // trigger would rewrite every library file on every app launch (the in-memory
        // _uuidByPath map is empty again after a restart). This test writes a real, fully
        // serialized SongItem (which therefore already contains <UUID>) and asserts the first
        // RegisterFromScan call for that path does NOT emit SongChanged.
        [TestMethod]
        public void RegisterFromScan_FileAlreadyHasPersistedUuid_DoesNotStampOrNotify()
        {
            var index = new SongLibraryIndex();
            var filePath = Path.Combine(_libraryDir, "already-has-uuid.xml");
            var song = new SongItem { Title = "Already Stable" };

            var serializer = new XmlSerializer(typeof(SongItem));
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                serializer.Serialize(stream, song);
            }

            var rawXmlBefore = File.ReadAllText(filePath);
            StringAssert.Contains(rawXmlBefore, "<UUID>",
                "Precondition: a freshly serialized SongItem must include a <UUID> element");

            var notified = false;
            using var subscription = index.SongChanged.Subscribe(_ => notified = true);

            index.RegisterFromScan(song, filePath, _libraryDir);

            Assert.IsFalse(notified,
                "RegisterFromScan must not emit SongChanged for a file that already has a persisted UUID");
            Assert.AreEqual(rawXmlBefore, File.ReadAllText(filePath),
                "RegisterFromScan must not rewrite a file that already has a persisted UUID");
        }

        // The counterpart to the test above: a file whose raw text genuinely lacks a <UUID>
        // element (the legacy, pre-migration shape) must still be stamped exactly once so
        // future parses read the stable identity directly from disk.
        [TestMethod]
        public async Task RegisterFromScan_FileGenuinelyLacksPersistedUuid_StampsIt()
        {
            var index = new SongLibraryIndex();
            var filePath = Path.Combine(_libraryDir, "legacy-no-uuid.xml");
            var song = new SongItem { Title = "Legacy, No UUID" };

            string rawXmlWithUuid;
            var serializer = new XmlSerializer(typeof(SongItem));
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, song);
                rawXmlWithUuid = writer.ToString();
            }

            // Simulate a pre-UUID-persistence library file: strip the <UUID> element that a
            // fresh serialize would otherwise include, keeping everything else valid XML.
            var uuidElement = $"<UUID>{song.UUID}</UUID>";
            StringAssert.Contains(rawXmlWithUuid, uuidElement,
                "Precondition: expected to find a literal <UUID> element to strip");
            var rawXmlWithoutUuid = rawXmlWithUuid.Replace(uuidElement, string.Empty);
            File.WriteAllText(filePath, rawXmlWithoutUuid);
            StringAssert.DoesNotMatch(File.ReadAllText(filePath), new System.Text.RegularExpressions.Regex("<UUID>"),
                "Precondition: the simulated legacy file must not contain a <UUID> element");

            var notified = false;
            using var subscription = index.SongChanged.Subscribe(_ => notified = true);

            index.RegisterFromScan(song, filePath, _libraryDir);

            Assert.IsTrue(notified,
                "RegisterFromScan must emit SongChanged to stamp a file that genuinely lacks a UUID");

            // NotifyChanged's write-through save is debounced (500ms); wait for it to land, then
            // confirm the file was actually rewritten to now include the UUID.
            var stamped = false;
            for (var i = 0; i < 30 && !stamped; i++)
            {
                await Task.Delay(50);
                stamped = File.Exists(filePath) && File.ReadAllText(filePath).Contains("<UUID>");
            }

            Assert.IsTrue(stamped, "Expected the debounced save to stamp <UUID> onto the file");
        }

        [TestMethod]
        public void NotifyChanged_EmitsOnSongChanged()
        {
            var index = new SongLibraryIndex();
            var song = new SongItem { Title = "Amazing Grace" };
            index.Register(song, Path.Combine(_libraryDir, "Amazing Grace.xml"), _libraryDir);

            Guid? observed = null;
            using var subscription = index.SongChanged.Subscribe(id => observed = id);

            index.NotifyChanged(song.UUID);

            Assert.AreEqual(song.UUID, observed);
        }

        [TestMethod]
        public void SecondInstance_Referencing_SameSong_SeesWriteThroughEdit()
        {
            var index = new SongLibraryIndex();
            var song = new SongItem { Title = "Original Title" };
            index.Register(song, Path.Combine(_libraryDir, "song.xml"), _libraryDir);

            var resolvedElsewhere = index.Resolve(song.UUID)!;
            resolvedElsewhere.Title = "Edited Title";

            Assert.AreEqual("Edited Title", index.Resolve(song.UUID)!.Title);
        }
    }
}
