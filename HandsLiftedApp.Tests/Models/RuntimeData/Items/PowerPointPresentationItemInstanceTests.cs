using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Importer.PowerPointLib;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class PowerPointPresentationItemInstanceTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PowerPointPresentationItemInstanceTests_" + Guid.NewGuid().ToString("N"));
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
    public void CollectSlideFilesInOrder_MixOfImagesAndVideos_ReturnsAllInNaturalSortOrder()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Slide.1.png"), "png");
        File.WriteAllText(Path.Combine(_tempDir, "Slide.2.mp4"), "mp4");
        File.WriteAllText(Path.Combine(_tempDir, "Slide.10.png"), "png");
        File.WriteAllText(Path.Combine(_tempDir, "Slide.9.webm"), "webm");
        // Non-media files must be ignored (e.g. the intermediate PDF, or our own warnings sidecar).
        File.WriteAllText(Path.Combine(_tempDir, "test.pdf"), "pdf");
        File.WriteAllText(Path.Combine(_tempDir, EmbeddedVideoExtractor.WarningsFileName), "warning");

        var result = PowerPointPresentationItemInstance.CollectSlideFilesInOrder(_tempDir);

        CollectionAssert.AreEqual(
            new[] { "Slide.1.png", "Slide.2.mp4", "Slide.9.webm", "Slide.10.png" },
            result.Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public void ReadSyncWarnings_WarningsFilePresent_ReturnsJoinedLines()
    {
        File.WriteAllLines(Path.Combine(_tempDir, EmbeddedVideoExtractor.WarningsFileName),
            new[]
            {
                "Slide 2: video is linked to an external file and can't be imported.",
                "Slide 4: video format '.mpg' isn't supported.",
            });

        var result = PowerPointPresentationItemInstance.ReadSyncWarnings(_tempDir);

        Assert.AreEqual(
            "Slide 2: video is linked to an external file and can't be imported." + Environment.NewLine +
            "Slide 4: video format '.mpg' isn't supported.",
            result);
    }

    [TestMethod]
    public void ReadSyncWarnings_NoWarningsFile_ReturnsNull()
    {
        var result = PowerPointPresentationItemInstance.ReadSyncWarnings(_tempDir);

        Assert.IsNull(result);
    }
}
