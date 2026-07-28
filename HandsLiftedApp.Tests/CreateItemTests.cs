using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Tests;

[TestClass]
public class CreateItemTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedCreateItemTests_" + Guid.NewGuid().ToString("N"));
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

    private string WriteXml<T>(T item, string fileName) where T : class
    {
        var path = Path.Combine(_tempDir, fileName);
        var serializer = new XmlSerializer(typeof(T));
        using (var stream = new FileStream(path, FileMode.Create))
        {
            serializer.Serialize(stream, item);
        }
        return path;
    }

    [TestMethod]
    public void GenerateItem_SongXml_ReturnsSongItem()
    {
        var song = new SongItem { Title = "Amazing Grace" };
        var path = WriteXml(song, "song.xml");

        var result = CreateItem.GenerateItem(path);

        Assert.IsInstanceOfType(result, typeof(SongItem));
        Assert.AreEqual("Amazing Grace", ((SongItem)result!).Title);
    }

    [TestMethod]
    public void GenerateItem_ScriptureXml_ReturnsScriptureItem()
    {
        var scripture = new ScriptureItem
        {
            Title = "John 3:16-21",
            Translation = "eng_bsb",
            Book = "JHN",
            StartChapter = 3,
            StartVerse = 16,
            EndChapter = 3,
            EndVerse = 21
        };
        var path = WriteXml(scripture, "scripture.xml");

        var result = CreateItem.GenerateItem(path);

        Assert.IsInstanceOfType(result, typeof(ScriptureItem));
        var loaded = (ScriptureItem)result!;
        Assert.AreEqual("John 3:16-21", loaded.Title);
        Assert.AreEqual("eng_bsb", loaded.Translation);
        Assert.AreEqual("JHN", loaded.Book);
        Assert.AreEqual(3, loaded.StartChapter);
        Assert.AreEqual(16, loaded.StartVerse);
        Assert.AreEqual(3, loaded.EndChapter);
        Assert.AreEqual(21, loaded.EndVerse);
    }

    [TestMethod]
    public void GenerateItem_UnrecognizedXmlRoot_ReturnsNull()
    {
        var path = Path.Combine(_tempDir, "unknown.xml");
        File.WriteAllText(path, "<SomeUnrelatedRoot><Foo>bar</Foo></SomeUnrelatedRoot>");

        var result = CreateItem.GenerateItem(path);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GenerateItem_MalformedXml_ReturnsNull()
    {
        var path = Path.Combine(_tempDir, "malformed.xml");
        File.WriteAllText(path, "<Song><Title>Unclosed");

        var result = CreateItem.GenerateItem(path);

        Assert.IsNull(result);
    }
}
