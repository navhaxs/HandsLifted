using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Importer.Scripture;

[TestClass]
public class ScriptureSourceLoaderTests
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

    private string _tempCacheRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempCacheRoot = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureTests_" + Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempCacheRoot))
        {
            Directory.Delete(_tempCacheRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadBookAsync_FetchesAndParsesFromHttp()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            Assert.AreEqual("https://v1.fetch.bible/bibles/eng_bsb/usx/gen.usx", request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(GenesisUsx) };
        });
        var loader = new ScriptureSourceLoader(new HttpClient(handler), _tempCacheRoot);

        var book = await loader.LoadBookAsync("eng_bsb", "gen");

        Assert.AreEqual("GEN", book.Code);
        Assert.AreEqual("Genesis", book.Title);
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task LoadBookAsync_SecondCallForSameBook_UsesMemoryCache()
    {
        var handler = new FakeHttpMessageHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(GenesisUsx) });
        var loader = new ScriptureSourceLoader(new HttpClient(handler), _tempCacheRoot);

        await loader.LoadBookAsync("eng_bsb", "gen");
        await loader.LoadBookAsync("eng_bsb", "gen");

        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task LoadBookAsync_DiskCacheHit_NeverCallsHttp()
    {
        var cacheDir = Path.Combine(_tempCacheRoot, "eng_bsb");
        Directory.CreateDirectory(cacheDir);
        await File.WriteAllTextAsync(Path.Combine(cacheDir, "gen.usx"), GenesisUsx);

        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called on a disk-cache hit."));
        var loader = new ScriptureSourceLoader(new HttpClient(handler), _tempCacheRoot);

        var book = await loader.LoadBookAsync("eng_bsb", "gen");

        Assert.AreEqual("GEN", book.Code);
        Assert.AreEqual(0, handler.CallCount);
    }

    [TestMethod]
    public async Task LoadBookAsync_HttpFailure_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var loader = new ScriptureSourceLoader(new HttpClient(handler), _tempCacheRoot);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => loader.LoadBookAsync("eng_bsb", "gen"));
    }

    [TestMethod]
    public async Task LoadBookAsync_SuccessfulFetch_WritesDiskCache()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(GenesisUsx) });
        var loader = new ScriptureSourceLoader(new HttpClient(handler), _tempCacheRoot);

        await loader.LoadBookAsync("eng_bsb", "gen");

        var cachedPath = Path.Combine(_tempCacheRoot, "eng_bsb", "gen.usx");
        Assert.IsTrue(File.Exists(cachedPath));
    }
}
