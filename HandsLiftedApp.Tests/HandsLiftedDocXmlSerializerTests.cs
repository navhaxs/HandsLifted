using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Tests;

[TestClass]
public class HandsLiftedDocXmlSerializerTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedDocXmlSerializerTests_" + Guid.NewGuid().ToString("N"));
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
    public void SerializePlaylist_ThenDeserialize_RoundTripsScriptureItem()
    {
        var someDesignId = Guid.NewGuid();
        var playlist = new PlaylistInstance();
        var scriptureInstance = new ScriptureItemInstance(playlist)
        {
            Title = "John 3:16-21",
            Translation = "eng_bsb",
            Book = "JHN",
            StartChapter = 3,
            StartVerse = 16,
            EndChapter = 3,
            EndVerse = 21,
            Design = someDesignId
        };
        playlist.Items.Add(scriptureInstance);

        var path = Path.Combine(_tempDir, "playlist.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

        Assert.AreEqual(1, deserialized.Items.Count);
        var roundTripped = deserialized.Items.Single();
        Assert.IsInstanceOfType(roundTripped, typeof(ScriptureItem));
        Assert.IsFalse(roundTripped is ScriptureItemInstance);

        var scriptureItem = (ScriptureItem)roundTripped;
        // UUID deliberately excluded: per this codebase's established, cross-cutting convention,
        // UUID does not round-trip through serialize/deserialize for ANY item type.
        Assert.AreEqual("John 3:16-21", scriptureItem.Title);
        Assert.AreEqual("eng_bsb", scriptureItem.Translation);
        Assert.AreEqual("JHN", scriptureItem.Book);
        Assert.AreEqual(3, scriptureItem.StartChapter);
        Assert.AreEqual(16, scriptureItem.StartVerse);
        Assert.AreEqual(3, scriptureItem.EndChapter);
        Assert.AreEqual(21, scriptureItem.EndVerse);
        Assert.AreEqual(someDesignId, scriptureItem.Design);
    }
}
