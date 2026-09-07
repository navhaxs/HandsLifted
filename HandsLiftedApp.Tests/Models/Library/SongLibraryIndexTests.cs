using System;
using System.IO;
using System.Reactive.Linq;
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
            if (Directory.Exists(_libraryDir))
                Directory.Delete(_libraryDir, recursive: true);
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
