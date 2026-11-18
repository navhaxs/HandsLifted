using System;
using System.IO;
using HandsLiftedApp.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests;

[TestClass]
public class ConversionToolboxTests
{
    private string _root = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ConversionToolboxTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [TestMethod]
    public void UniqueDirectory_unused_name_is_used_as_is()
    {
        Assert.AreEqual(Path.Combine(_root, "Deck"), ConversionToolbox.UniqueDirectory(_root, "Deck"));
    }

    [TestMethod]
    public void UniqueDirectory_existing_name_gets_numbered_suffix()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Deck"));
        Assert.AreEqual(Path.Combine(_root, "Deck (2)"), ConversionToolbox.UniqueDirectory(_root, "Deck"));

        Directory.CreateDirectory(Path.Combine(_root, "Deck (2)"));
        Assert.AreEqual(Path.Combine(_root, "Deck (3)"), ConversionToolbox.UniqueDirectory(_root, "Deck"));
    }

    [TestMethod]
    public void UniqueDirectory_sanitises_invalid_characters_and_blank_names()
    {
        Assert.AreEqual(Path.Combine(_root, "a_b"), ConversionToolbox.UniqueDirectory(_root, "a|b"));
        Assert.AreEqual(Path.Combine(_root, "Converted"), ConversionToolbox.UniqueDirectory(_root, "  "));
    }

    [TestMethod]
    public void Run_missing_input_throws()
    {
        Assert.ThrowsException<FileNotFoundException>(() =>
            ConversionToolbox.Run(ToolboxConversion.PdfToPngs, Path.Combine(_root, "nope.pdf"), _root));
    }
}
