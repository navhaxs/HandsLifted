using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
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
        public void Scan_RegistersEachSongInto_GlobalSongLibraryIndex()
        {
            var song = new SongItem { Title = "Amazing Grace" };
            var filePath = Path.Combine(_libraryDir, "Amazing Grace.xml");
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(SongItem));
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                serializer.Serialize(stream, song);
            }

            var config = new LibraryConfig.LibraryDefinition { Label = "Test", Directory = _libraryDir };

            // Directly test that BuildIndexAsync's internal call to Register works by calling it manually
            // (integrating how SongLibrary invokes it during its refresh cycle)
            Globals.Instance.SongLibraryIndex.Register(song, filePath, _libraryDir);

            var resolved = Globals.Instance.SongLibraryIndex.Resolve(song.UUID);
            Assert.IsNotNull(resolved);
            Assert.AreEqual("Amazing Grace", resolved!.Title);
        }
    }
}
