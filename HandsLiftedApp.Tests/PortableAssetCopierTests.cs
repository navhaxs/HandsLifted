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
    private string _libraryDir = null!;

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

    [TestMethod]
    public void ResolveOrCopyIntoMediaLibrary_NotConfigured_ThrowsForMediaFile()
    {
        var image = WriteSourceFile("photo.jpg", "photo-bytes");

        Assert.ThrowsException<MediaLibraryNotConfiguredException>(() =>
            PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(image, null));
        Assert.ThrowsException<MediaLibraryNotConfiguredException>(() =>
            PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(image, ""));
    }

    [TestMethod]
    public void ResolveOrCopyIntoMediaLibrary_NotConfigured_NonMediaFilePassesThroughUnchanged()
    {
        var songXml = WriteSourceFile("song.xml", "<Song />");

        var result = PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(songXml, null);

        Assert.AreEqual(songXml, result);
    }

    [TestMethod]
    public void ResolveOrCopyIntoMediaLibrary_FileAlreadyInsideLibrary_ReferencedInPlaceWithoutCopy()
    {
        _libraryDir = Path.Combine(_tempDir, "Library");
        Directory.CreateDirectory(Path.Combine(_libraryDir, "Media", "Images"));
        var libraryFile = Path.Combine(_libraryDir, "Media", "Images", "photo.jpg");
        File.WriteAllText(libraryFile, "photo-bytes");

        var result = PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(libraryFile, _libraryDir);

        Assert.AreEqual(libraryFile, result);
    }

    [TestMethod]
    public void ResolveOrCopyIntoMediaLibrary_FileOutsideLibrary_CopiesIntoLibrary()
    {
        _libraryDir = Path.Combine(_tempDir, "Library");
        Directory.CreateDirectory(_libraryDir);
        var source = WriteSourceFile("photo.jpg", "photo-bytes");

        var result = PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(source, _libraryDir);

        Assert.AreEqual(Path.Combine(_libraryDir, "Media", "Images", "photo.jpg"), result);
        Assert.IsTrue(File.Exists(result));
    }

    // ---- dropped / pasted pictures: Media/Pasted ----

    private string MakeLibrary()
    {
        _libraryDir = Path.Combine(_tempDir, "Library");
        Directory.CreateDirectory(_libraryDir);
        return _libraryDir;
    }

    [TestMethod]
    public void PastedSubfolder_IsMediaPasted()
    {
        Assert.AreEqual(Path.Combine("Media", "Pasted"), PortableAssetCopier.PastedSubfolder);
    }

    [TestMethod]
    public void ResolveOrCopyIntoPastedFolder_ImageOutsideLibrary_IsCopiedIntoMediaPasted()
    {
        var library = MakeLibrary();
        var source = WriteSourceFile("photo.png", "png-bytes");

        var result = PortableAssetCopier.ResolveOrCopyIntoPastedFolder(source, library);

        Assert.AreEqual(Path.Combine(library, "Media", "Pasted", "photo.png"), result);
        Assert.IsTrue(File.Exists(result));
        Assert.IsTrue(File.Exists(source), "the original is untouched");
        Assert.AreEqual("png-bytes", File.ReadAllText(source));
    }

    [TestMethod]
    public void ResolveOrCopyIntoPastedFolder_FileAlreadyInMediaImages_IsReferencedInPlace()
    {
        var library = MakeLibrary();
        Directory.CreateDirectory(Path.Combine(library, "Media", "Images"));
        var inImages = Path.Combine(library, "Media", "Images", "photo.png");
        File.WriteAllText(inImages, "png-bytes");

        var result = PortableAssetCopier.ResolveOrCopyIntoPastedFolder(inImages, library);

        Assert.AreEqual(inImages, result);
        Assert.IsFalse(Directory.Exists(Path.Combine(library, "Media", "Pasted")), "nothing copied");
    }

    [TestMethod]
    public void ResolveOrCopyIntoPastedFolder_FileAlreadyInMediaPasted_IsReturnedUnchanged_NoSuffixedCopy()
    {
        var library = MakeLibrary();
        var pasted = Path.Combine(library, "Media", "Pasted");
        Directory.CreateDirectory(pasted);
        var inPasted = Path.Combine(pasted, "photo.png");
        File.WriteAllText(inPasted, "png-bytes");

        var result = PortableAssetCopier.ResolveOrCopyIntoPastedFolder(inPasted, library);

        Assert.AreEqual(inPasted, result);
        CollectionAssert.AreEqual(new[] { inPasted }, Directory.GetFiles(pasted), "no hash-suffixed copy");
    }

    [TestMethod]
    public void ResolveOrCopyIntoPastedFolder_FileElsewhereInsideLibrary_IsReferencedInPlace()
    {
        var library = MakeLibrary();
        Directory.CreateDirectory(Path.Combine(library, "Backgrounds"));
        var inside = Path.Combine(library, "Backgrounds", "bg.jpg");
        File.WriteAllText(inside, "jpg-bytes");

        var result = PortableAssetCopier.ResolveOrCopyIntoPastedFolder(inside, library);

        Assert.AreEqual(inside, result);
        Assert.IsFalse(Directory.Exists(Path.Combine(library, "Media")), "nothing copied");
    }

    [TestMethod]
    public void ResolveOrCopyIntoPastedFolder_NotConfigured_Throws()
    {
        var image = WriteSourceFile("photo.png", "png-bytes");

        Assert.ThrowsException<MediaLibraryNotConfiguredException>(() =>
            PortableAssetCopier.ResolveOrCopyIntoPastedFolder(image, null));
        Assert.ThrowsException<MediaLibraryNotConfiguredException>(() =>
            PortableAssetCopier.ResolveOrCopyIntoPastedFolder(image, ""));
        Assert.ThrowsException<MediaLibraryNotConfiguredException>(() =>
            PortableAssetCopier.ResolveOrCopyIntoPastedFolder(image, @"relative\library"));
    }

    [TestMethod]
    public void ResolveOrCopyIntoPastedFolder_NonImage_PassesThroughUnchanged()
    {
        var library = MakeLibrary();
        var video = WriteSourceFile("clip.mp4", "mp4-bytes");
        var text = WriteSourceFile("notes.txt", "hello");

        Assert.AreEqual(video, PortableAssetCopier.ResolveOrCopyIntoPastedFolder(video, library));
        Assert.AreEqual(text, PortableAssetCopier.ResolveOrCopyIntoPastedFolder(text, null));
        Assert.IsFalse(Directory.Exists(Path.Combine(library, "Media")), "nothing copied");
    }

    [TestMethod]
    public void ResolveOrCopyIntoPastedFolder_SameFileTwice_ReusesTheSameCopy()
    {
        var library = MakeLibrary();
        var source = WriteSourceFile("photo.png", "png-bytes");

        var first = PortableAssetCopier.ResolveOrCopyIntoPastedFolder(source, library);
        var second = PortableAssetCopier.ResolveOrCopyIntoPastedFolder(source, library);

        Assert.AreEqual(first, second);
        Assert.AreEqual(1, Directory.GetFiles(Path.Combine(library, "Media", "Pasted")).Length);
    }

    [TestMethod]
    public void ResolveOrCopyIntoPastedFolder_SameNameDifferentContent_GetsAHashSuffix()
    {
        var library = MakeLibrary();
        var a = WriteSourceFile("photo.png", "png-A");
        var first = PortableAssetCopier.ResolveOrCopyIntoPastedFolder(a, library);
        Directory.CreateDirectory(Path.Combine(_tempDir, "Other"));
        var b = Path.Combine(_tempDir, "Other", "photo.png");
        File.WriteAllText(b, "png-B-different");

        var second = PortableAssetCopier.ResolveOrCopyIntoPastedFolder(b, library);

        Assert.AreNotEqual(first, second);
        StringAssert.StartsWith(Path.GetFileName(second), "photo_");
        Assert.AreEqual("png-A", File.ReadAllText(first));
    }

    [TestMethod]
    public void IsImageFile_ChecksTheExtensionIgnoringCase()
    {
        Assert.IsTrue(PortableAssetCopier.IsImageFile(@"C:\x\a.PNG"));
        Assert.IsTrue(PortableAssetCopier.IsImageFile(@"C:\x\a.jpg"));
        Assert.IsFalse(PortableAssetCopier.IsImageFile(@"C:\x\a.mp4"));
        Assert.IsFalse(PortableAssetCopier.IsImageFile(@"C:\x\noext"));
        Assert.IsFalse(PortableAssetCopier.IsImageFile(null));
        Assert.IsFalse(PortableAssetCopier.IsImageFile(""));
    }
}
