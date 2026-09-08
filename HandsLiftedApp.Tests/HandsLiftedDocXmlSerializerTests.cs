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
        // No SongId registered in SongLibraryIndex and no local draft, so under the
        // SongItemInstance facade (Task 6) this Title write is a no-op — Title isn't
        // asserted below, only SlideTransitionDurationMs, which is a real local field
        // on Item unaffected by the facade.
        var songInstance = new SongItemInstance(playlist)
        {
            Title = "Amazing Grace",
            SlideTransitionDurationMs = 300
        };
        playlist.Items.Add(songInstance);

        var path = Path.Combine(_tempDir, "playlist-song-override.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

        // Songs now serialize as a lightweight SongItemReference, not a full SongItem.
        var songReference = (SongItemReference)deserialized.Items.Single();
        Assert.AreEqual(300.0, songReference.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void SerializeItem_SongItemInstance_ReturnsReferenceNotFullContent()
    {
        var song = new SongItem { Title = "Amazing Grace", Copyright = "PD" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(null) { SongId = song.UUID };

        var serialized = HandsLiftedDocXmlSerializer.SerializeItem(instance, "irrelevant-playlist-dir");

        Assert.IsInstanceOfType(serialized, typeof(SongItemReference));
        var reference = (SongItemReference)serialized;
        Assert.AreEqual(song.UUID, reference.SongId, "The reference must carry which song it points at");
        Assert.AreEqual(instance.UUID, reference.UUID, "The reference must carry the playlist item's own identity");
    }

    [TestMethod]
    public void SerializePlaylist_ThenDeserialize_SongItem_RoundTripsAsReference()
    {
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var playlist = new PlaylistInstance { Title = "Test Playlist" };
        var instance = new SongItemInstance(playlist) { SongId = song.UUID };
        playlist.Items.Add(instance);

        var path = Path.Combine(_tempDir, "playlist-song-reference.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);
        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

        Assert.IsInstanceOfType(deserialized.Items.Single(), typeof(SongItemReference));
        var reference = (SongItemReference)deserialized.Items.Single();
        Assert.AreEqual(song.UUID, reference.SongId, "SongId must round-trip through the playlist XML");
        Assert.AreEqual(instance.UUID, reference.UUID, "UUID (playlist-item identity) must round-trip too");
    }

    [TestMethod]
    public void SongItemReference_Clone_PreservesSongId_GetsOwnUUID()
    {
        // Regression test for MainViewModel's "duplicate item" command:
        // SerializeItem -> Item.Clone() -> ItemInstanceFactory.ToItemInstance.
        //
        // A duplicated song reference must keep pointing at the same library song (SongId
        // preserved by the base Clone()'s XML round-trip) while getting its OWN fresh
        // playlist-item identity (UUID reassigned by the base Clone()). The UUID half is
        // load-bearing: PlaylistInstance.NavigateToReference and SlideThumbnailBehavior both
        // look items up by UUID, so two references sharing a UUID make a slide-thumbnail
        // click on the second occurrence navigate to the first one's slide instead.
        var song = new SongItem { Title = "Amazing Grace", Copyright = "PD" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(null) { SongId = song.UUID, SlideTransitionDurationMs = 300 };

        var serialized = HandsLiftedDocXmlSerializer.SerializeItem(instance, "irrelevant-playlist-dir");
        var originalReference = (SongItemReference)serialized;
        var cloned = serialized.Clone();

        Assert.IsInstanceOfType(cloned, typeof(SongItemReference));
        var clonedReference = (SongItemReference)cloned;

        // Still points at the same library song.
        Assert.AreEqual(song.UUID, clonedReference.SongId);
        Assert.AreSame(song, Globals.Instance.SongLibraryIndex.Resolve(clonedReference.SongId),
            "Duplicated song reference must still resolve to the original library song.");

        // ...but is a distinct playlist item.
        Assert.AreNotEqual(originalReference.UUID, clonedReference.UUID,
            "A duplicated song reference must get its own playlist-item identity (UUID), otherwise " +
            "slide navigation and drag-reorder cannot tell the two playlist items apart.");

        Assert.AreEqual(300.0, clonedReference.SlideTransitionDurationMs);
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
        Assert.IsFalse(rawXml.Contains(_tempDir), "Absolute path must not be written; SourcePresentationFile should be relative.");
        Assert.IsFalse(rawXml.Contains("SomeOldExportDir"), "Stale export directory/baked slide paths must not be written.");

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);
        var pdfItem = (PDFSlidesGroupItem)deserialized.Items.Single();
        Assert.AreEqual(0, pdfItem.Items.Count);
    }

    [TestMethod]
    public void SerializePlaylist_MediaGroupItem_SourceMediaFilePathIsRelative()
    {
        var playlist = new PlaylistInstance();
        var imagesDir = Path.Combine(_tempDir, "Media", "Images");
        Directory.CreateDirectory(imagesDir);
        var mediaFile = Path.Combine(imagesDir, "photo.jpg");
        File.WriteAllText(mediaFile, "jpg-bytes");

        var mediaGroupInstance = new MediaGroupItemInstance(playlist) { Title = "Photos" };
        mediaGroupInstance.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = mediaFile });
        playlist.Items.Add(mediaGroupInstance);

        var path = Path.Combine(_tempDir, "playlist-mediagroup-relative.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var rawXml = File.ReadAllText(path);
        StringAssert.Contains(rawXml, @"Media\Images\photo.jpg");
        Assert.IsFalse(rawXml.Contains(_tempDir),
            "Absolute path must not be written; SourceMediaFilePath should be relative to the playlist directory.");

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);
        var mediaGroupItem = (MediaGroupItem)deserialized.Items.Single();
        var roundTrippedMediaItem = (MediaGroupItem.MediaItem)mediaGroupItem.Items.Single();
        Assert.AreEqual(@"Media\Images\photo.jpg", roundTrippedMediaItem.SourceMediaFilePath);
    }

    [TestMethod]
    public void SerializePlaylist_PdfItem_SourceOutsidePlaylistFolder_KeepsAbsolutePath()
    {
        // A legacy playlist (or one whose PDF was added before copy-on-add covered that path)
        // references a source that does not live under the playlist folder. Relativizing it
        // would emit "..\Outside\sermon.pdf" — portable-looking, but not portable.
        var playlistDir = Path.Combine(_tempDir, "PlaylistFolder");
        var outsideDir = Path.Combine(_tempDir, "Outside");
        Directory.CreateDirectory(playlistDir);
        Directory.CreateDirectory(outsideDir);
        var sourceFile = Path.Combine(outsideDir, "sermon.pdf");
        File.WriteAllText(sourceFile, "pdf-bytes");

        var playlist = new PlaylistInstance();
        playlist.Items.Add(new PDFSlidesGroupItemInstance(playlist)
        {
            Title = "Sermon Slides",
            SourcePresentationFile = sourceFile
        });

        var path = Path.Combine(playlistDir, "playlist-pdf-outside.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var rawXml = File.ReadAllText(path);
        StringAssert.Contains(rawXml, sourceFile);
        // NOTE: not asserting the absence of ".." across the whole document — the default
        // avares:// LogoGraphicFile is separately (and pre-existingly) mangled into a ".." path
        // by this serializer. Assert specifically that THIS source was not relativized.
        Assert.IsFalse(rawXml.Contains(Path.GetRelativePath(playlistDir, sourceFile)),
            "A source outside the playlist folder must stay absolute, not become a '..' relative path.");

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);
        var pdfItem = (PDFSlidesGroupItem)deserialized.Items.Single();
        Assert.AreEqual(sourceFile, pdfItem.SourcePresentationFile);
    }

    [TestMethod]
    public void SerializePlaylist_MediaGroupItem_SourceOutsidePlaylistFolder_KeepsAbsolutePath()
    {
        var playlistDir = Path.Combine(_tempDir, "PlaylistFolder");
        var outsideDir = Path.Combine(_tempDir, "Outside");
        Directory.CreateDirectory(playlistDir);
        Directory.CreateDirectory(outsideDir);
        var mediaFile = Path.Combine(outsideDir, "photo.jpg");
        File.WriteAllText(mediaFile, "jpg-bytes");

        var playlist = new PlaylistInstance();
        var mediaGroupInstance = new MediaGroupItemInstance(playlist) { Title = "Photos" };
        mediaGroupInstance.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = mediaFile });
        playlist.Items.Add(mediaGroupInstance);

        var path = Path.Combine(playlistDir, "playlist-mediagroup-outside.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var rawXml = File.ReadAllText(path);
        StringAssert.Contains(rawXml, mediaFile);
        Assert.IsFalse(rawXml.Contains(Path.GetRelativePath(playlistDir, mediaFile)),
            "A media file outside the playlist folder must stay absolute, not become a '..' relative path.");
    }
}
