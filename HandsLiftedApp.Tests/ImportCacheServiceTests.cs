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
}
