using System;
using System.IO;
using System.Linq;
using HandsLiftedApp.Core.Models.RuntimeData;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.RuntimeData;

[TestClass]
public class SlideIdentityTests
{
    private string _dir = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SlideIdentityTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string File_(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [TestMethod]
    public void SourceIds_AreUsedWhenThereIsOnePerFile()
    {
        var files = new[] { File_("a.png", "1"), File_("b.png", "2") };

        var keys = SlideIdentity.KeysFor(files, new[] { "256", "258" });

        CollectionAssert.AreEqual(new[] { "id:256", "id:258" }, keys);
    }

    [TestMethod]
    public void WhenIdsDontLineUpWithTheFiles_ImageHashesAreUsedInstead()
    {
        var files = new[] { File_("a.png", "same"), File_("b.png", "same"), File_("c.png", "different") };

        var keys = SlideIdentity.KeysFor(files, new[] { "256" }); // 1 id, 3 files

        Assert.IsTrue(keys.All(k => k != null && k.StartsWith("sha:")));
        Assert.AreEqual(keys[0], keys[1]);
        Assert.AreNotEqual(keys[0], keys[2]);
    }

    [TestMethod]
    public void NoIds_FilesThatAreNotImagesGetNoKey()
    {
        var files = new[] { File_("a.png", "x"), File_("Slide.2.mp4", "video") };

        var keys = SlideIdentity.KeysFor(files, null);

        Assert.IsNotNull(keys[0]);
        Assert.IsNull(keys[1]);
    }

    [TestMethod]
    public void MapIndex_FollowsTheSlideWhereverItWentInTheNewSet()
    {
        var oldKeys = new string?[] { "id:1", "id:2", "id:3" };
        var newKeys = new string?[] { "id:9", "id:3", "id:1", "id:2" }; // a slide was added, others re-ordered

        Assert.AreEqual(2, SlideIdentity.MapIndex(oldKeys, 0, newKeys));
        Assert.AreEqual(3, SlideIdentity.MapIndex(oldKeys, 1, newKeys));
        Assert.AreEqual(1, SlideIdentity.MapIndex(oldKeys, 2, newKeys));
    }

    [TestMethod]
    public void MapIndex_ADeletedSlideCannotBeFound()
    {
        Assert.AreEqual(-1, SlideIdentity.MapIndex(new string?[] { "id:1", "id:2" }, 1, new string?[] { "id:1" }));
    }

    [TestMethod]
    public void MapIndex_UnknownKeysOrIndicesNeverMatch()
    {
        Assert.AreEqual(-1, SlideIdentity.MapIndex(null, 0, new string?[] { "id:1" }));
        Assert.AreEqual(-1, SlideIdentity.MapIndex(new string?[] { null }, 0, new string?[] { null }));
        Assert.AreEqual(-1, SlideIdentity.MapIndex(new string?[] { "id:1" }, 5, new string?[] { "id:1" }));
        Assert.AreEqual(-1, SlideIdentity.MapIndex(new string?[] { "id:1" }, -1, new string?[] { "id:1" }));
    }

    [TestMethod]
    public void MapIndex_IdenticalSlides_PicksTheOneNearestTheOldPosition()
    {
        var oldKeys = new string?[] { "sha:blank", "sha:a", "sha:blank", "sha:b", "sha:blank" };
        var newKeys = new string?[] { "sha:blank", "sha:a", "sha:blank", "sha:b", "sha:blank", "sha:c" };

        Assert.AreEqual(0, SlideIdentity.MapIndex(oldKeys, 0, newKeys));
        Assert.AreEqual(2, SlideIdentity.MapIndex(oldKeys, 2, newKeys));
        Assert.AreEqual(4, SlideIdentity.MapIndex(oldKeys, 4, newKeys));
    }
}
