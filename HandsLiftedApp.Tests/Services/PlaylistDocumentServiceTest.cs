using System;
using System.IO;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.ViewModels;
using JetBrains.Annotations;

namespace HandsLiftedApp.Tests.Services
{
    [TestClass]
    [TestSubject(typeof(PlaylistDocumentService))]
    public class PlaylistDocumentServiceTest
    {
        private string _tempDir = null!;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PlaylistDocumentServiceTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            Globals.Instance.AppPreferences = new AppPreferencesViewModel();
        }

        [TestCleanup]
        public void Teardown()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        public void AssertLegacyAutoSaveSidecarPath()
        {
            Assert.AreEqual("C:\\My VisionScreens Data\\My playlist.autosave.xml", PlaylistDocumentService.GetLegacyAutoSaveSidecarPath("C:\\My VisionScreens Data\\My playlist.xml"));
        }

        [TestMethod]
        public void DeleteLegacyAutoSaveSidecar_RemovesSidecarOnly()
        {
            var path = Path.Combine(_tempDir, "p.xml");
            var sidecar = PlaylistDocumentService.GetLegacyAutoSaveSidecarPath(path);
            File.WriteAllText(path, "real");
            File.WriteAllText(sidecar, "stale");

            PlaylistDocumentService.DeleteLegacyAutoSaveSidecar(path);

            Assert.IsFalse(File.Exists(sidecar));
            Assert.IsTrue(File.Exists(path));
        }

        [TestMethod]
        public void AutoSaveToFile_NoFilePath_Skips()
        {
            var playlist = new PlaylistInstance { Title = "x" };

            Assert.AreEqual(PlaylistDocumentService.AutoSaveResult.Skipped, PlaylistDocumentService.AutoSaveToFile(playlist));
        }

        [TestMethod]
        public void AutoSaveToFile_WhileLoading_SkipsAndLeavesFileAlone()
        {
            var path = Path.Combine(_tempDir, "p.xml");
            File.WriteAllText(path, "original");
            var playlist = new PlaylistInstance { Title = "half loaded", PlaylistFilePath = path, IsPlaylistLoading = true };

            Assert.AreEqual(PlaylistDocumentService.AutoSaveResult.Skipped, PlaylistDocumentService.AutoSaveToFile(playlist));
            Assert.AreEqual("original", File.ReadAllText(path));
        }

        [TestMethod]
        public void AutoSaveToFile_NewFile_WritesWithoutBackup()
        {
            var path = Path.Combine(_tempDir, "p.xml");
            var playlist = new PlaylistInstance { Title = "first", PlaylistFilePath = path };

            Assert.AreEqual(PlaylistDocumentService.AutoSaveResult.Saved, PlaylistDocumentService.AutoSaveToFile(playlist));

            Assert.IsTrue(File.Exists(path));
            Assert.IsFalse(File.Exists(PlaylistDocumentService.GetBackupFilePath(path)));
            Assert.IsFalse(File.Exists(path + ".autosave.tmp"));
            Assert.IsFalse(playlist.IsDirty);
            Assert.IsNotNull(playlist.LastSaved);
        }

        [TestMethod]
        public void AutoSaveToFile_UnchangedSecondCall_DoesNotRewrite()
        {
            var path = Path.Combine(_tempDir, "p.xml");
            var playlist = new PlaylistInstance { Title = "same", PlaylistFilePath = path };
            PlaylistDocumentService.AutoSaveToFile(playlist);
            var stamp = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, stamp);
            playlist.IsDirty = true; // simulate a stale/incorrect dirty bit

            Assert.AreEqual(PlaylistDocumentService.AutoSaveResult.Unchanged, PlaylistDocumentService.AutoSaveToFile(playlist));

            Assert.AreEqual(stamp, File.GetLastWriteTimeUtc(path));
            Assert.IsFalse(playlist.IsDirty);
        }

        [TestMethod]
        public void AutoSaveToFile_IdleCheckComparesToMemoryNotDisk()
        {
            var path = Path.Combine(_tempDir, "p.xml");
            var playlist = new PlaylistInstance { Title = "same", PlaylistFilePath = path };
            PlaylistDocumentService.AutoSaveToFile(playlist);
            File.WriteAllText(path, "tampered outside the app");

            // Unchanged in memory => no write, and the file isn't even consulted.
            Assert.AreEqual(PlaylistDocumentService.AutoSaveResult.Unchanged, PlaylistDocumentService.AutoSaveToFile(playlist));
            Assert.AreEqual("tampered outside the app", File.ReadAllText(path));
        }

        [TestMethod]
        public void SaveDocument_ThenAutoSaveUnchanged_ReLearnsDiskOnce()
        {
            var path = Path.Combine(_tempDir, "p.xml");
            var playlist = new PlaylistInstance { Title = "v1", PlaylistFilePath = path };
            PlaylistDocumentService.AutoSaveToFile(playlist);
            playlist.Title = "v2";
            PlaylistDocumentService.SaveDocument(playlist); // manual save writes v2, bypassing the cache

            Assert.AreEqual(PlaylistDocumentService.AutoSaveResult.Unchanged, PlaylistDocumentService.AutoSaveToFile(playlist));
        }

        [TestMethod]
        public void AutoSaveToFile_ChangeIsSavedEvenIfDirtyBitMissed()
        {
            var path = Path.Combine(_tempDir, "p.xml");
            var playlist = new PlaylistInstance { Title = "v1", PlaylistFilePath = path };
            PlaylistDocumentService.AutoSaveToFile(playlist);

            playlist.Title = "v2";
            playlist.IsDirty = false; // change event "missed"

            Assert.AreEqual(PlaylistDocumentService.AutoSaveResult.Saved, PlaylistDocumentService.AutoSaveToFile(playlist));
            StringAssert.Contains(File.ReadAllText(path), "v2");
        }

        [TestMethod]
        public void AutoSaveToFile_BackupIsTakenOnceFromPreAutosaveFile()
        {
            var path = Path.Combine(_tempDir, "p.xml");
            File.WriteAllText(path, "ORIGINAL");
            var playlist = new PlaylistInstance { Title = "v1", PlaylistFilePath = path };

            PlaylistDocumentService.AutoSaveToFile(playlist);
            playlist.Title = "v2";
            PlaylistDocumentService.AutoSaveToFile(playlist);

            Assert.AreEqual("ORIGINAL", File.ReadAllText(PlaylistDocumentService.GetBackupFilePath(path)));
            StringAssert.Contains(File.ReadAllText(path), "v2");
        }

        [TestMethod]
        public void ResetAutoSaveState_NextWriteReSnapshotsBackup()
        {
            var path = Path.Combine(_tempDir, "p.xml");
            File.WriteAllText(path, "ORIGINAL");
            var playlist = new PlaylistInstance { Title = "v1", PlaylistFilePath = path };
            PlaylistDocumentService.AutoSaveToFile(playlist);

            PlaylistDocumentService.ResetAutoSaveState(path);
            playlist.Title = "v2";
            PlaylistDocumentService.AutoSaveToFile(playlist);

            StringAssert.Contains(File.ReadAllText(PlaylistDocumentService.GetBackupFilePath(path)), "v1");
        }
    }
}
