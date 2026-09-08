using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Services;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class ExternalVideoDownloaderTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ExternalVideoDownloaderTests_" + Guid.NewGuid().ToString("N"));
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
    public void PadSlideNumber_SingleDigitTotal_NoPadding()
    {
        var result = ExternalVideoDownloader.PadSlideNumber(slideNumber: 1, shownSlideCount: 5);

        Assert.AreEqual("1", result);
    }

    [TestMethod]
    public void PadSlideNumber_DoubleDigitTotal_PadsToTwoDigits()
    {
        var result = ExternalVideoDownloader.PadSlideNumber(slideNumber: 3, shownSlideCount: 12);

        Assert.AreEqual("03", result);
    }

    [TestMethod]
    public void AlreadyDownloaded_MatchingVideoFileExists_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Slide.1.mp4"), "video-bytes");

        Assert.IsTrue(ExternalVideoDownloader.AlreadyDownloaded(_tempDir, "1"));
    }

    [TestMethod]
    public void AlreadyDownloaded_OnlyPngExists_ReturnsFalse()
    {
        // The static-image step always runs first, so a PNG existing alone must not be mistaken
        // for an already-downloaded video (that PNG is exactly what still needs replacing).
        File.WriteAllText(Path.Combine(_tempDir, "Slide.1.png"), "png-bytes");

        Assert.IsFalse(ExternalVideoDownloader.AlreadyDownloaded(_tempDir, "1"));
    }

    [TestMethod]
    public void AlreadyDownloaded_NothingExists_ReturnsFalse()
    {
        Assert.IsFalse(ExternalVideoDownloader.AlreadyDownloaded(_tempDir, "1"));
    }

    [TestMethod]
    public void BuildArguments_ReturnsExpectedArgumentList()
    {
        var result = ExternalVideoDownloader.BuildArguments(
            "https://player.vimeo.com/video/1117314702?app_id=122963",
            "/cache/Slide.1.%(ext)s");

        CollectionAssert.AreEqual(
            new[]
            {
                "-f", "best[height<=1080]/bestvideo[height<=1080]+bestaudio",
                "--merge-output-format", "mp4",
                "-o", "/cache/Slide.1.%(ext)s",
                "--no-playlist",
                "--newline",
                "https://player.vimeo.com/video/1117314702?app_id=122963",
            },
            result.ToArray());
    }
}
