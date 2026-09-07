using System;
using System.IO;
using System.Threading.Tasks;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.Library
{
    [TestClass]
    public class SongLibraryTests
    {
        private string _libraryDir = "";

        [TestInitialize]
        public void Setup()
        {
            _libraryDir = Path.Combine(Path.GetTempPath(), "SongLibraryTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_libraryDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_libraryDir))
                Directory.Delete(_libraryDir, recursive: true);
        }

        [TestMethod]
        public async Task Scan_RegistersEachSongInto_GlobalSongLibraryIndex()
        {
            var song = new SongItem { Title = "Amazing Grace" };
            var filePath = Path.Combine(_libraryDir, "Amazing Grace.xml");
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(SongItem));
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                serializer.Serialize(stream, song);
            }

            var config = new LibraryConfig.LibraryDefinition { Label = "Test", Directory = _libraryDir };
            var library = new SongLibrary(config, new FileSystemSongLibrarySource(_libraryDir));

            // BuildIndexAsync runs fire-and-forget from the constructor; poll briefly.
            for (var i = 0; i < 50 && !library.IsIndexReady; i++)
            {
                await Task.Delay(20);
            }

            Assert.IsTrue(library.IsIndexReady, "Library index did not become ready in time");
            var resolved = Globals.Instance.SongLibraryIndex.Resolve(song.UUID);
            Assert.IsNotNull(resolved);
            Assert.AreEqual("Amazing Grace", resolved!.Title);
        }
    }
}
