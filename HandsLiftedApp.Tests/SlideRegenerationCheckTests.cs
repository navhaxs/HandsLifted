using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Tests;

[TestClass]
public class SlideRegenerationCheckTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SlideRegenerationCheckTests_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void NeedsSlideRegeneration_NoItems_ReturnsTrue()
    {
        var playlist = new PlaylistInstance();
        var pdfInstance = new PDFSlidesGroupItemInstance(playlist);

        Assert.IsTrue(SlideRegenerationCheck.NeedsSlideRegeneration(pdfInstance));
    }

    [TestMethod]
    public void NeedsSlideRegeneration_AllBakedFilesExist_ReturnsFalse()
    {
        var playlist = new PlaylistInstance();
        var pdfInstance = new PDFSlidesGroupItemInstance(playlist);
        var slidePath = Path.Combine(_tempDir, "slide1.png");
        File.WriteAllText(slidePath, "png-bytes");
        pdfInstance.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = slidePath });

        Assert.IsFalse(SlideRegenerationCheck.NeedsSlideRegeneration(pdfInstance));
    }

    [TestMethod]
    public void NeedsSlideRegeneration_ABakedFileIsMissing_ReturnsTrue()
    {
        var playlist = new PlaylistInstance();
        var pdfInstance = new PDFSlidesGroupItemInstance(playlist);
        pdfInstance.Items.Add(new MediaGroupItem.MediaItem
        {
            SourceMediaFilePath = Path.Combine(_tempDir, "does-not-exist.png")
        });

        Assert.IsTrue(SlideRegenerationCheck.NeedsSlideRegeneration(pdfInstance));
    }
}
