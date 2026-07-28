using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;

namespace HandsLiftedApp.Tests.Models.Library;

[TestClass]
public class ScriptureLibraryTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureLibraryTests_" + Guid.NewGuid().ToString("N"));
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
    public void Refresh_OnlyPicksUpXmlFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "john-3-16.xml"), "<Scripture><Title>John 3:16</Title></Scripture>");
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "not a scripture item");

        var config = new LibraryConfig.LibraryDefinition { Label = "Scripture", Directory = _tempDir, Type = LibraryType.Scripture };
        var library = new ScriptureLibrary(config);

        Assert.AreEqual(1, library.Items.Count);
        Assert.IsTrue(library.Items[0].FullFilePath.EndsWith("john-3-16.xml"));
    }

    [TestMethod]
    public void Refresh_EmptyDirectory_ReturnsNoItems()
    {
        var config = new LibraryConfig.LibraryDefinition { Label = "Scripture", Directory = _tempDir, Type = LibraryType.Scripture };
        var library = new ScriptureLibrary(config);

        Assert.AreEqual(0, library.Items.Count);
    }

    [TestMethod]
    public void Refresh_NonExistentDirectory_ReturnsNoItemsWithoutThrowing()
    {
        var config = new LibraryConfig.LibraryDefinition
        {
            Label = "Scripture",
            Directory = Path.Combine(_tempDir, "does-not-exist"),
            Type = LibraryType.Scripture
        };

        var library = new ScriptureLibrary(config);

        Assert.AreEqual(0, library.Items.Count);
    }
}
