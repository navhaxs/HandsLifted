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

    [TestMethod]
    public void SerializePlaylist_ThenDeserialize_RoundTripsDefaultThemeIds()
    {
        var songThemeId = Guid.NewGuid();
        var songMotionThemeId = Guid.NewGuid();
        var scriptureThemeId = Guid.NewGuid();
        var playlist = new PlaylistInstance
        {
            DefaultSongThemeId = songThemeId,
            DefaultSongMotionThemeId = songMotionThemeId,
            DefaultScriptureThemeId = scriptureThemeId
        };

        var path = Path.Combine(_tempDir, "playlist-defaults.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

        Assert.AreEqual(songThemeId, deserialized.DefaultSongThemeId);
        Assert.AreEqual(songMotionThemeId, deserialized.DefaultSongMotionThemeId);
        Assert.AreEqual(scriptureThemeId, deserialized.DefaultScriptureThemeId);
    }

    [TestMethod]
    public void SerializePlaylist_DefaultThemeIdsUnset_RoundTripAsNull()
    {
        var playlist = new PlaylistInstance();

        var path = Path.Combine(_tempDir, "playlist-no-defaults.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

        Assert.IsNull(deserialized.DefaultSongThemeId);
        Assert.IsNull(deserialized.DefaultSongMotionThemeId);
        Assert.IsNull(deserialized.DefaultScriptureThemeId);
    }

    [TestMethod]
    public void SerializePlaylist_ThenDeserialize_RoundTripsScriptureItemTransitionOverride()
    {
        var playlist = new PlaylistInstance { SlideTransitionDurationMs = 120 };
        var scriptureInstance = new ScriptureItemInstance(playlist)
        {
            Title = "John 3:16-21",
            Book = "JHN",
            SlideTransitionDurationMs = 750
        };
        playlist.Items.Add(scriptureInstance);

        var path = Path.Combine(_tempDir, "playlist-scripture-override.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

        var scriptureItem = (ScriptureItem)deserialized.Items.Single();
        Assert.AreEqual(750.0, scriptureItem.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void SerializePlaylist_ThenDeserialize_RoundTripsSongItemTransitionOverride()
    {
        var playlist = new PlaylistInstance { SlideTransitionDurationMs = 120 };
        var songInstance = new SongItemInstance(playlist)
        {
            Title = "Amazing Grace",
            SlideTransitionDurationMs = 300
        };
        playlist.Items.Add(songInstance);

        var path = Path.Combine(_tempDir, "playlist-song-override.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

        var songItem = (SongItem)deserialized.Items.Single();
        Assert.AreEqual(300.0, songItem.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void SerializePlaylist_ThenDeserialize_RoundTripsMediaGroupItemTransitionOverride()
    {
        var playlist = new PlaylistInstance { SlideTransitionDurationMs = 120 };
        var mediaGroupInstance = new MediaGroupItemInstance(playlist)
        {
            Title = "Photos",
            SlideTransitionDurationMs = 1000
        };
        playlist.Items.Add(mediaGroupInstance);

        var path = Path.Combine(_tempDir, "playlist-mediagroup-override.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

        var mediaGroupItem = (MediaGroupItem)deserialized.Items.Single();
        Assert.AreEqual(1000.0, mediaGroupItem.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void SerializePlaylist_ThenDeserialize_ItemWithNoOverride_StaysNull()
    {
        var playlist = new PlaylistInstance { SlideTransitionDurationMs = 120 };
        var scriptureInstance = new ScriptureItemInstance(playlist) { Title = "No override" };
        playlist.Items.Add(scriptureInstance);

        var path = Path.Combine(_tempDir, "playlist-no-override.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

        var scriptureItem = (ScriptureItem)deserialized.Items.Single();
        Assert.IsNull(scriptureItem.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void SerializePlaylist_PdfItem_SourcePresentationFileIsRelative_ItemsAndExportDirNotWritten()
    {
        var playlist = new PlaylistInstance();
        var sourcesDir = Path.Combine(_tempDir, "Sources");
        Directory.CreateDirectory(sourcesDir);
        var sourceFile = Path.Combine(sourcesDir, "sermon.pdf");
        File.WriteAllText(sourceFile, "pdf-bytes");

        var pdfInstance = new PDFSlidesGroupItemInstance(playlist)
        {
            Title = "Sermon Slides",
            SourcePresentationFile = sourceFile,
            SourceSlidesExportDirectory = Path.Combine(_tempDir, "SomeOldExportDir")
        };
        pdfInstance.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = Path.Combine(_tempDir, "SomeOldExportDir", "slide1.png") });
        playlist.Items.Add(pdfInstance);

        var path = Path.Combine(_tempDir, "playlist-pdf.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var rawXml = File.ReadAllText(path);
        StringAssert.Contains(rawXml, @"Sources\sermon.pdf");
        Assert.IsFalse(rawXml.Contains("SomeOldExportDir"), "Stale export directory/baked slide paths must not be written.");

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);
        var pdfItem = (PDFSlidesGroupItem)deserialized.Items.Single();
        Assert.AreEqual(0, pdfItem.Items.Count);
    }
}
