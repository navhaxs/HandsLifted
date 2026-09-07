using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Tests.TestSupport;
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

        // Runs on DispatcherTestThread (see HandsLiftedApp.Tests/TestSupport/DispatcherTestThread.cs -
        // the same fix ScriptureItemInstanceTests uses, reused here rather than reimplemented) because
        // SongLibrary.BuildIndexAsync awaits Dispatcher.UIThread.InvokeAsync(() => IsIndexReady = true).
        // Without running on the thread Dispatcher.UIThread is bound to, that call throws "the calling
        // thread cannot access this object because a different thread owns it" the first time any test in
        // the assembly touches Dispatcher.UIThread from an uncontrolled thread. ScriptureItemInstanceTests'
        // [AssemblyInitialize] already guarantees Dispatcher.UIThread is bound to DispatcherTestThread
        // before this test runs, so no extra initialization is needed here beyond DispatcherTestThread.Run.
        //
        // Dispatcher.UIThread.InvokeAsync posts onto Dispatcher.UIThread's own internal job queue, which -
        // unlike DispatcherTestThread's pump loop - only drains when something explicitly calls
        // Dispatcher.UIThread.RunJobs(). BuildIndexAsync runs fire-and-forget from the SongLibrary
        // constructor, so nothing else will ever call RunJobs() for it; the polling loop below must pump
        // it itself on each iteration for IsIndexReady to ever actually flip to true.
        [TestMethod]
        public Task Scan_RegistersEachSongInto_GlobalSongLibraryIndex() => DispatcherTestThread.Run(async () =>
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

            // BuildIndexAsync runs fire-and-forget from the constructor; poll while pumping
            // Dispatcher.UIThread's job queue (see comment above the test for why RunJobs() is needed here).
            for (var i = 0; i < 50 && !library.IsIndexReady; i++)
            {
                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.IsTrue(library.IsIndexReady, "Library index did not become ready in time");
            var resolved = Globals.Instance.SongLibraryIndex.Resolve(song.UUID);
            Assert.IsNotNull(resolved);
            Assert.AreEqual("Amazing Grace", resolved!.Title);
        });
    }
}
