using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Importer.Scripture;

[TestClass]
public class ScriptureUsxDownloaderTests
{
    private const string MinimalUsx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <usx version="3.0">
          <book code="GEN" style="id">- Genesis</book>
          <para style="mt1">Genesis</para>
          <chapter number="1" style="c" sid="GEN 1"/>
          <para style="p">
            <verse number="1" style="v" sid="GEN 1:1"/>Text.<verse eid="GEN 1:1"/>
          </para>
          <chapter eid="GEN 1"/>
        </usx>
        """;

    private string _tempRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureUsxDownloaderTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task DownloadAllBooksAsync_FetchesAllMissingBooks_WritesOneFilePerBook()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MinimalUsx) });
        var downloader = new ScriptureUsxDownloader(new HttpClient(handler));

        await downloader.DownloadAllBooksAsync(_tempRoot);

        Assert.AreEqual(ScriptureUsxDownloader.AllBookCodes.Count, Directory.GetFiles(_tempRoot, "*.usx").Length);
        Assert.AreEqual(ScriptureUsxDownloader.AllBookCodes.Count, handler.CallCount);
    }

    [TestMethod]
    public async Task DownloadAllBooksAsync_SkipsBookThatAlreadyExists()
    {
        Directory.CreateDirectory(_tempRoot);
        var genPath = Path.Combine(_tempRoot, "gen.usx");
        await File.WriteAllTextAsync(genPath, "already-downloaded-sentinel");

        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString().Contains("/gen.usx"))
            {
                throw new InvalidOperationException("Should not re-fetch a book that already exists on disk.");
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MinimalUsx) };
        });
        var downloader = new ScriptureUsxDownloader(new HttpClient(handler));

        await downloader.DownloadAllBooksAsync(_tempRoot);

        Assert.AreEqual("already-downloaded-sentinel", await File.ReadAllTextAsync(genPath));
    }

    [TestMethod]
    public async Task DownloadAllBooksAsync_OneBookFails_RestOfBatchStillDownloads()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.RequestUri!.ToString().Contains("/gen.usx")
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MinimalUsx) });
        var downloader = new ScriptureUsxDownloader(new HttpClient(handler));

        var failedCount = await downloader.DownloadAllBooksAsync(_tempRoot);

        Assert.IsFalse(File.Exists(Path.Combine(_tempRoot, "gen.usx")));
        Assert.IsTrue(File.Exists(Path.Combine(_tempRoot, "exo.usx")));
        Assert.AreEqual(ScriptureUsxDownloader.AllBookCodes.Count - 1, Directory.GetFiles(_tempRoot, "*.usx").Length);
        Assert.AreEqual(1, failedCount);
    }

    [TestMethod]
    public async Task DownloadAllBooksAsync_MultipleBooksFail_ReturnsMatchingFailedCount()
    {
        var failingCodes = new[] { "gen", "exo", "lev" };
        var handler = new FakeHttpMessageHandler(request =>
            failingCodes.Any(code => request.RequestUri!.ToString().Contains($"/{code}.usx"))
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MinimalUsx) });
        var downloader = new ScriptureUsxDownloader(new HttpClient(handler));

        var failedCount = await downloader.DownloadAllBooksAsync(_tempRoot);

        Assert.AreEqual(failingCodes.Length, failedCount);
        Assert.AreEqual(ScriptureUsxDownloader.AllBookCodes.Count - failingCodes.Length, Directory.GetFiles(_tempRoot, "*.usx").Length);
    }
}
