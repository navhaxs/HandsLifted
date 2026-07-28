using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels;
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
        // ScriptureItemInstance's default-store fallback (Step 3 of Task 3) reads
        // Globals.Instance.AppPreferences.ScriptureDataPath, which is null unless Globals.OnStartup()
        // has run. Matches the same fix already used by SongImporterTests.Init().
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
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
        var original = new ScriptureItem
        {
            Title = "John 3:16-21",
            Translation = "eng_bsb",
            Book = "JHN",
            StartChapter = 3,
            StartVerse = 16,
            EndChapter = 3,
            EndVerse = 21
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
    }
}
