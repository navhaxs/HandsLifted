using HandsLiftedApp.Core.Services;
using JetBrains.Annotations;

namespace HandsLiftedApp.Tests.Services
{
    [TestClass]
    [TestSubject(typeof(PlaylistDocumentService))]
    public class PlaylistDocumentServiceTest
    {

        [TestMethod]
        public void AssertAutoSavePlaylistFilePaths()
        {
            Assert.AreEqual("C:\\My VisionScreens Data\\My playlist.autosave.xml", PlaylistDocumentService.GetAutoSavePlaylistFilePath("C:\\My VisionScreens Data\\My playlist.xml"));
        }
    }
}