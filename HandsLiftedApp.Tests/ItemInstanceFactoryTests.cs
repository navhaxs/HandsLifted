using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Tests;

[TestClass]
public class ItemInstanceFactoryTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedItemInstanceFactoryTests_" + Guid.NewGuid().ToString("N"));
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
    public void ToItemInstance_ScriptureItem_RoundTripsThroughDiskAndFactory()
    {
        var someDesignId = Guid.NewGuid();
        var original = new ScriptureItem
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

        var path = Path.Combine(_tempDir, "john-3-16.xml");
        var serializer = new XmlSerializer(typeof(ScriptureItem));
        using (var stream = new FileStream(path, FileMode.Create))
        {
            serializer.Serialize(stream, original);
        }

        var deserialized = CreateItem.GenerateItem(path);
        Assert.IsInstanceOfType(deserialized, typeof(ScriptureItem));

        var instance = ItemInstanceFactory.ToItemInstance(deserialized!, null);

        Assert.IsInstanceOfType(instance, typeof(ScriptureItemInstance));
        var scriptureInstance = (ScriptureItemInstance)instance;
        Assert.AreEqual("John 3:16-21", scriptureInstance.Title);
        Assert.AreEqual("eng_bsb", scriptureInstance.Translation);
        Assert.AreEqual("JHN", scriptureInstance.Book);
        Assert.AreEqual(3, scriptureInstance.StartChapter);
        Assert.AreEqual(16, scriptureInstance.StartVerse);
        Assert.AreEqual(3, scriptureInstance.EndChapter);
        Assert.AreEqual(21, scriptureInstance.EndVerse);
        Assert.AreEqual(someDesignId, scriptureInstance.Design);
    }

    [TestMethod]
    public void ToItemInstance_ScriptureItem_RoundTripsTransitionOverride()
    {
        var original = new ScriptureItem
        {
            Title = "John 3:16-21",
            Book = "JHN",
            SlideTransitionDurationMs = 750
        };

        var path = Path.Combine(_tempDir, "scripture-override.xml");
        var serializer = new XmlSerializer(typeof(ScriptureItem));
        using (var stream = new FileStream(path, FileMode.Create))
        {
            serializer.Serialize(stream, original);
        }

        var deserialized = CreateItem.GenerateItem(path);
        var instance = (ScriptureItemInstance)ItemInstanceFactory.ToItemInstance(deserialized!, null);

        Assert.AreEqual(750.0, instance.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void ToItemInstance_SongItem_RoundTripsTransitionOverride()
    {
        var original = new SongItem
        {
            Title = "Amazing Grace",
            SlideTransitionDurationMs = 300
        };

        var path = Path.Combine(_tempDir, "song-override.xml");
        var serializer = new XmlSerializer(typeof(SongItem));
        using (var stream = new FileStream(path, FileMode.Create))
        {
            serializer.Serialize(stream, original);
        }

        var deserialized = CreateItem.GenerateItem(path);
        var instance = (SongItemInstance)ItemInstanceFactory.ToItemInstance(deserialized!, null);

        Assert.AreEqual(300.0, instance.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void ToItemInstance_SongItemReference_ResolvesAgainstLibraryIndex()
    {
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var reference = new SongItemReference { SongId = song.UUID };

        var result = ItemInstanceFactory.ToItemInstance(reference, null);

        Assert.IsInstanceOfType(result, typeof(SongItemInstance));
        var instance = (SongItemInstance)result;
        Assert.AreEqual("Amazing Grace", instance.Title);
        Assert.AreEqual(song.UUID, instance.SongId, "SongId must carry over from the reference");
        Assert.AreEqual(reference.UUID, instance.UUID,
            "The reference's own playlist-item identity must carry over unchanged");
    }

    [TestMethod]
    public void ToItemInstance_LegacyInlineSongItem_ImportsIntoLibraryIndex_WhenNotAlreadyKnown()
    {
        var inlineSong = new SongItem { Title = "Legacy Inline Song", Copyright = "PD" };

        var result = ItemInstanceFactory.ToItemInstance(inlineSong, null);

        Assert.IsInstanceOfType(result, typeof(SongItemInstance));
        var instance = (SongItemInstance)result;
        Assert.AreEqual("Legacy Inline Song", instance.Title);
        Assert.AreEqual(inlineSong.UUID, instance.SongId,
            "The new playlist item must point at the imported song via SongId");
        Assert.AreNotEqual(inlineSong.UUID, instance.UUID,
            "The new playlist item must keep its own identity, distinct from the song's");
        var resolved = Globals.Instance.SongLibraryIndex.Resolve(inlineSong.UUID);
        Assert.IsNotNull(resolved, "Legacy inline song content should have been imported into the shared index");
        Assert.AreEqual("PD", resolved!.Copyright);
    }

    [TestMethod]
    public void ToItemInstance_SongItem_AlreadyKnownToIndex_DoesNotOverwriteExistingEntry()
    {
        var libraryVersion = new SongItem { Title = "Already In Library", Copyright = "Current" };
        Globals.Instance.SongLibraryIndex.Register(libraryVersion, "song.xml", "libdir");
        var staleParsedCopy = new SongItem { UUID = libraryVersion.UUID, Title = "Already In Library", Copyright = "Stale" };

        ItemInstanceFactory.ToItemInstance(staleParsedCopy, null);

        Assert.AreEqual("Current", Globals.Instance.SongLibraryIndex.Resolve(libraryVersion.UUID)!.Copyright);
    }
}
