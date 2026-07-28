using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Importer.Scripture;

[TestClass]
public class ScriptureLocalUsxStoreTests
{
    private const string GenesisUsx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <usx version="3.0">
          <book code="GEN" style="id">- Genesis</book>
          <para style="mt1">Genesis</para>
          <chapter number="1" style="c" sid="GEN 1"/>
          <para style="p">
            <verse number="1" style="v" sid="GEN 1:1"/>In the beginning God created the heaven and the earth.<verse eid="GEN 1:1"/>
          </para>
          <chapter eid="GEN 1"/>
        </usx>
        """;

    private string _tempRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureLocalUsxStoreTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadBookAsync_FileExists_ReadsAndParses()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "gen.usx"), GenesisUsx);
        var store = new ScriptureLocalUsxStore(_tempRoot);

        var book = await store.LoadBookAsync("gen");

        Assert.AreEqual("GEN", book.Code);
        Assert.AreEqual("Genesis", book.Title);
    }

    [TestMethod]
    public async Task LoadBookAsync_FileMissing_ThrowsScriptureBookNotFoundException()
    {
        var store = new ScriptureLocalUsxStore(_tempRoot);

        await Assert.ThrowsExceptionAsync<ScriptureBookNotFoundException>(() => store.LoadBookAsync("gen"));
    }

    [TestMethod]
    public async Task LoadBookAsync_SecondCallForSameBook_ReadsFromMemoryCacheNotDisk()
    {
        var filePath = Path.Combine(_tempRoot, "gen.usx");
        await File.WriteAllTextAsync(filePath, GenesisUsx);
        var store = new ScriptureLocalUsxStore(_tempRoot);

        await store.LoadBookAsync("gen");
        File.Delete(filePath);
        var book = await store.LoadBookAsync("gen");

        Assert.AreEqual("GEN", book.Code);
    }

    [TestMethod]
    public async Task LoadBookAsync_BookCodeContainsPathTraversal_ThrowsArgumentException()
    {
        var store = new ScriptureLocalUsxStore(_tempRoot);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.LoadBookAsync("../../etc"));
    }
}
