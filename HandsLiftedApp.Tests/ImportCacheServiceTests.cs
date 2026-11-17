using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Services;

namespace HandsLiftedApp.Tests;

[TestClass]
public class ImportCacheServiceTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ImportCacheServiceTests_" + Guid.NewGuid().ToString("N"));
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
    public void GetFileImportCacheDirectory_SameContentDifferentAbsolutePath_ReturnsSameDirectory()
    {
        var dirA = Path.Combine(_tempDir, "MachineA_Users_Jeremy");
        var dirB = Path.Combine(_tempDir, "MachineB_VisionScreens_Data");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        var fileA = Path.Combine(dirA, "sermon.pdf");
        var fileB = Path.Combine(dirB, "sermon.pdf");
        File.WriteAllText(fileA, "identical-pdf-bytes");
        File.WriteAllText(fileB, "identical-pdf-bytes");

        var cacheDirA = ImportCacheService.GetFileImportCacheDirectory(fileA);
        var cacheDirB = ImportCacheService.GetFileImportCacheDirectory(fileB);

        Assert.AreEqual(cacheDirA, cacheDirB);
    }

    [TestMethod]
    public void GetFileImportCacheDirectory_DifferentContent_ReturnsDifferentDirectory()
    {
        var fileA = Path.Combine(_tempDir, "sermon-v1.pdf");
        var fileB = Path.Combine(_tempDir, "sermon-v2.pdf");
        File.WriteAllText(fileA, "version-one-bytes");
        File.WriteAllText(fileB, "version-two-bytes");

        var cacheDirA = ImportCacheService.GetFileImportCacheDirectory(fileA);
        var cacheDirB = ImportCacheService.GetFileImportCacheDirectory(fileB);

        Assert.AreNotEqual(cacheDirA, cacheDirB);
    }

    [TestMethod]
    public void HasUsableCachedExports_DirectoryDoesNotExist_ReturnsFalse()
    {
        var missing = Path.Combine(_tempDir, "never-created");

        Assert.IsFalse(ImportCacheService.HasUsableCachedExports(missing));
    }

    [TestMethod]
    public void HasUsableCachedExports_NullOrEmptyPath_ReturnsFalse()
    {
        Assert.IsFalse(ImportCacheService.HasUsableCachedExports(null));
        Assert.IsFalse(ImportCacheService.HasUsableCachedExports(""));
        Assert.IsFalse(ImportCacheService.HasUsableCachedExports("   "));
    }

    [TestMethod]
    public void HasUsableCachedExports_EmptyDirectory_ReturnsFalse()
    {
        var emptyDir = Path.Combine(_tempDir, "cold-cache");
        Directory.CreateDirectory(emptyDir);

        Assert.IsFalse(ImportCacheService.HasUsableCachedExports(emptyDir));
    }

    [TestMethod]
    public void HasUsableCachedExports_OnlyIntermediateNonImageFiles_ReturnsFalse()
    {
        var dir = Path.Combine(_tempDir, "half-converted");
        Directory.CreateDirectory(dir);
        // A crashed/interrupted PowerPoint import can leave the intermediate PDF behind
        // with no rasterized pages — that must not count as a warm cache.
        File.WriteAllText(Path.Combine(dir, "sermon.pdf"), "pdf-bytes");

        Assert.IsFalse(ImportCacheService.HasUsableCachedExports(dir));
    }

    [TestMethod]
    public void HasUsableCachedExports_ContainsPngExports_ReturnsTrue()
    {
        var dir = Path.Combine(_tempDir, "warm-cache");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "slide1.png"), "png-bytes");
        File.WriteAllText(Path.Combine(dir, "slide2.png"), "png-bytes");
        ImportCacheService.MarkImportComplete(dir);

        Assert.IsTrue(ImportCacheService.HasUsableCachedExports(dir));
    }

    [TestMethod]
    public void HasUsableCachedExports_ContainsJpgExportsAlongsideIntermediatePdf_ReturnsTrue()
    {
        var dir = Path.Combine(_tempDir, "warm-cache-jpg");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "sermon.pdf"), "pdf-bytes");
        File.WriteAllText(Path.Combine(dir, "slide1.JPG"), "jpg-bytes");
        ImportCacheService.MarkImportComplete(dir);

        Assert.IsTrue(ImportCacheService.HasUsableCachedExports(dir));
    }

    [TestMethod]
    public void HasUsableCachedExports_ContainsOnlyExtractedVideoFile_ReturnsTrue()
    {
        // A deck whose every shown slide has embedded video has all its PNGs deleted by
        // EmbeddedVideoExtractor, leaving only video files in the cache directory.
        var dir = Path.Combine(_tempDir, "warm-cache-video-only");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Slide.1.mp4"), "mp4-bytes");
        ImportCacheService.MarkImportComplete(dir);

        Assert.IsTrue(ImportCacheService.HasUsableCachedExports(dir));
    }

    [TestMethod]
    public void HasUsableCachedExports_ImagesButNoCompletionMarker_ReturnsFalse()
    {
        var dir = Path.Combine(_tempDir, "interrupted");
        Directory.CreateDirectory(dir);
        // a conversion that crashed half way leaves some pages behind: not a usable cache
        File.WriteAllText(Path.Combine(dir, "Slide.1.png"), "png-bytes");

        Assert.IsFalse(ImportCacheService.HasUsableCachedExports(dir));
    }

    [TestMethod]
    public void BeginImport_ClearsLeftoversAndTheMarker()
    {
        var dir = Path.Combine(_tempDir, "stale");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Slide.1.png"), "old");
        File.WriteAllText(Path.Combine(dir, "Slide.2.png"), "old");
        ImportCacheService.MarkImportComplete(dir);
        Assert.IsTrue(ImportCacheService.HasUsableCachedExports(dir));

        ImportCacheService.BeginImport(dir);

        Assert.AreEqual(0, Directory.GetFiles(dir).Length);
        Assert.IsFalse(ImportCacheService.HasUsableCachedExports(dir));
    }

    [TestMethod]
    public void GetContentSubdirectory_FollowsTheContentOfTheFile()
    {
        var parent = Path.Combine(_tempDir, "google");
        Directory.CreateDirectory(parent);
        var v1 = Path.Combine(parent, "deck-v1.pdf");
        var v1Again = Path.Combine(parent, "deck-v1-again.pdf");
        var v2 = Path.Combine(parent, "deck-v2.pdf");
        File.WriteAllText(v1, "version one");
        File.WriteAllText(v1Again, "version one");
        File.WriteAllText(v2, "version two");

        var a = ImportCacheService.GetContentSubdirectory(parent, v1, "_export_");
        var b = ImportCacheService.GetContentSubdirectory(parent, v1Again, "_export_");
        var c = ImportCacheService.GetContentSubdirectory(parent, v2, "_export_");

        Assert.AreEqual(a, b, "unchanged content reuses the directory");
        Assert.AreNotEqual(a, c, "edited content gets a fresh directory, so image paths never repeat");
        Assert.IsTrue(Directory.Exists(c));
    }
}
