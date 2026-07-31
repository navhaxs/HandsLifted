using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Utils;

namespace HandsLiftedApp.Tests;

[TestClass]
public class PortableAssetCopierTests
{
    private string _tempDir = null!;
    private string _playlistDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PortableAssetCopierTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _playlistDir = Path.Combine(_tempDir, "Playlist");
        Directory.CreateDirectory(_playlistDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string WriteSourceFile(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    [TestMethod]
    public void CopyIntoSubfolder_NewFile_CopiesKeepingOriginalName()
    {
        var source = WriteSourceFile("photo.jpg", "photo-bytes");

        var result = PortableAssetCopier.CopyIntoSubfolder(source, _playlistDir, Path.Combine("Media", "Images"));

        Assert.AreEqual(Path.Combine(_playlistDir, "Media", "Images", "photo.jpg"), result);
        Assert.IsTrue(File.Exists(result));
        Assert.AreEqual("photo-bytes", File.ReadAllText(result));
    }

    [TestMethod]
    public void CopyIntoSubfolder_SameNameSameContent_ReusesExistingCopyWithoutDuplicating()
    {
        var source = WriteSourceFile("photo.jpg", "photo-bytes");
        var first = PortableAssetCopier.CopyIntoSubfolder(source, _playlistDir, Path.Combine("Media", "Images"));

        var second = PortableAssetCopier.CopyIntoSubfolder(source, _playlistDir, Path.Combine("Media", "Images"));

        Assert.AreEqual(first, second);
        var destDir = Path.Combine(_playlistDir, "Media", "Images");
        Assert.AreEqual(1, Directory.GetFiles(destDir).Length);
    }

    [TestMethod]
    public void CopyIntoSubfolder_SameNameDifferentContent_AppendsHashSuffix()
    {
        var sourceA = WriteSourceFile("photo.jpg", "photo-bytes-A");
        var destA = PortableAssetCopier.CopyIntoSubfolder(sourceA, _playlistDir, Path.Combine("Media", "Images"));

        Directory.CreateDirectory(Path.Combine(_tempDir, "OtherFolder"));
        var sourceB = Path.Combine(_tempDir, "OtherFolder", "photo.jpg");
        File.WriteAllText(sourceB, "photo-bytes-B-different");

        var destB = PortableAssetCopier.CopyIntoSubfolder(sourceB, _playlistDir, Path.Combine("Media", "Images"));

        Assert.AreNotEqual(destA, destB);
        Assert.IsTrue(File.Exists(destA));
        Assert.IsTrue(File.Exists(destB));
        Assert.AreEqual("photo-bytes-A", File.ReadAllText(destA));
        Assert.AreEqual("photo-bytes-B-different", File.ReadAllText(destB));
        StringAssert.StartsWith(Path.GetFileName(destB), "photo_");
    }

    [TestMethod]
    public void CopyIntoSubfolder_SourceAlreadyInsideDestination_IsNoOp()
    {
        var source = WriteSourceFile("photo.jpg", "photo-bytes");
        var firstCopy = PortableAssetCopier.CopyIntoSubfolder(source, _playlistDir, Path.Combine("Media", "Images"));

        var result = PortableAssetCopier.CopyIntoSubfolder(firstCopy, _playlistDir, Path.Combine("Media", "Images"));

        Assert.AreEqual(firstCopy, result);
    }

    [TestMethod]
    public void CopyMediaOrPresentationIntoPlaylist_Image_RoutesToMediaImages()
    {
        var source = WriteSourceFile("photo.png", "png-bytes");

        var result = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(source, _playlistDir);

        Assert.AreEqual(Path.Combine(_playlistDir, "Media", "Images", "photo.png"), result);
    }

    [TestMethod]
    public void CopyMediaOrPresentationIntoPlaylist_Video_RoutesToMediaVideo()
    {
        var source = WriteSourceFile("clip.mp4", "mp4-bytes");

        var result = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(source, _playlistDir);

        Assert.AreEqual(Path.Combine(_playlistDir, "Media", "Video", "clip.mp4"), result);
    }

    [TestMethod]
    public void CopyMediaOrPresentationIntoPlaylist_Pdf_RoutesToSources()
    {
        var source = WriteSourceFile("sermon.pdf", "pdf-bytes");

        var result = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(source, _playlistDir);

        Assert.AreEqual(Path.Combine(_playlistDir, "Sources", "sermon.pdf"), result);
    }

    [TestMethod]
    public void CopyMediaOrPresentationIntoPlaylist_Pptx_RoutesToSources()
    {
        var source = WriteSourceFile("sermon.pptx", "pptx-bytes");

        var result = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(source, _playlistDir);

        Assert.AreEqual(Path.Combine(_playlistDir, "Sources", "sermon.pptx"), result);
    }

    [TestMethod]
    public void CopyMediaOrPresentationIntoPlaylist_SongXml_ReturnsPathUnchanged()
    {
        var source = WriteSourceFile("song.xml", "<Song />");

        var result = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(source, _playlistDir);

        Assert.AreEqual(source, result);
        Assert.IsFalse(Directory.Exists(Path.Combine(_playlistDir, "Media")));
    }

    // A playlist that has never been saved still has PlaylistInstance's class-default
    // relative working directory (@"VisionScreensUserData\"), which would otherwise resolve
    // against Environment.CurrentDirectory.
    [TestMethod]
    public void CopyIntoSubfolder_WorkingDirectoryNotFullyQualified_ReturnsSourceUnchangedAndCreatesNothing()
    {
        var source = WriteSourceFile("photo.jpg", "photo-bytes");
        var relativeWorkingDirectory = @"VisionScreensUserData\";

        var result = PortableAssetCopier.CopyIntoSubfolder(source, relativeWorkingDirectory,
            Path.Combine("Media", "Images"));

        Assert.AreEqual(source, result);
        Assert.IsFalse(
            Directory.Exists(Path.Combine(Environment.CurrentDirectory, relativeWorkingDirectory)),
            "No directory may be created relative to the current working directory.");
    }

    [TestMethod]
    public void CopyIntoSubfolder_WorkingDirectoryNullOrEmpty_ReturnsSourceUnchanged()
    {
        var source = WriteSourceFile("photo.jpg", "photo-bytes");

        Assert.AreEqual(source,
            PortableAssetCopier.CopyIntoSubfolder(source, null!, Path.Combine("Media", "Images")));
        Assert.AreEqual(source,
            PortableAssetCopier.CopyIntoSubfolder(source, "", Path.Combine("Media", "Images")));
    }

    [TestMethod]
    public void CopyMediaOrPresentationIntoPlaylist_WorkingDirectoryNotFullyQualified_ReturnsSourceUnchanged()
    {
        var image = WriteSourceFile("photo.jpg", "photo-bytes");
        var video = WriteSourceFile("clip.mp4", "mp4-bytes");
        var pdf = WriteSourceFile("sermon.pdf", "pdf-bytes");

        Assert.AreEqual(image,
            PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(image, @"VisionScreensUserData\"));
        Assert.AreEqual(video,
            PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(video, @"VisionScreensUserData\"));
        Assert.AreEqual(pdf,
            PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(pdf, @"VisionScreensUserData\"));
    }
}
