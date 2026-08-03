using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;

namespace HandsLiftedApp.Tests.Models.Library;

[TestClass]
public class LibraryTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedLibraryTests_" + Guid.NewGuid().ToString("N"));
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
    public void Refresh_IgnoresUnsupportedExtensions()
    {
        File.WriteAllText(Path.Combine(_tempDir, "background.png"), "fake png");
        File.WriteAllText(Path.Combine(_tempDir, "Thumbs.db"), "windows thumbnail cache");
        File.WriteAllText(Path.Combine(_tempDir, "source.pdn"), "paint.net project source");

        var config = new LibraryConfig.LibraryDefinition { Label = "Backgrounds", Directory = _tempDir, Type = LibraryType.Media };
        var library = new HandsLiftedApp.Core.Models.Library.Library(config);

        Assert.AreEqual(1, library.Items.Count);
        Assert.IsTrue(library.Items[0].FullFilePath.EndsWith("background.png"));
    }

    [TestMethod]
    public void Refresh_PicksUpAllSupportedMediaExtensions()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.png"), "img");
        File.WriteAllText(Path.Combine(_tempDir, "b.mp4"), "vid");
        File.WriteAllText(Path.Combine(_tempDir, "c.pdf"), "pdf");
        File.WriteAllText(Path.Combine(_tempDir, "d.pptx"), "ppt");

        var config = new LibraryConfig.LibraryDefinition { Label = "Media", Directory = _tempDir, Type = LibraryType.Media };
        var library = new HandsLiftedApp.Core.Models.Library.Library(config);

        Assert.AreEqual(4, library.Items.Count);
    }

    [TestMethod]
    public void Refresh_EmptyDirectory_ReturnsNoItems()
    {
        var config = new LibraryConfig.LibraryDefinition { Label = "Media", Directory = _tempDir, Type = LibraryType.Media };
        var library = new HandsLiftedApp.Core.Models.Library.Library(config);

        Assert.AreEqual(0, library.Items.Count);
    }
}
