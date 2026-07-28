# Local USX Source Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scripture slide rendering reads USX data from a local, user-configured directory only — never the network. A separate, network-using download action (triggered from app preferences) populates that directory ahead of time.

**Architecture:** Split `ScriptureSourceLoader` (Phase 1) into two classes: `ScriptureLocalUsxStore` (disk-only, no `HttpClient` field at all — used at render time) and `ScriptureUsxDownloader` (network-only, used only by a new "Download Bible Data" button in `SetupWindow`). `ScriptureItemInstance` swaps its dependency from the loader to the store and gains a try/catch that turns a missing book into a placeholder slide instead of a thrown exception.

**Tech Stack:** .NET 8, MSTest, `System.Net.Http`/`System.Xml.Linq` (same as Phase 1), Avalonia (SetupWindow UI).

## Global Constraints

- net8.0, MSTest, matches Phases 1–4a.
- Exactly one fixed translation for this phase: `eng_bsb`, whole Bible (66 books) — no translation picker, no multi-translation support.
- No first-run wizard — the download is triggered from a button in the existing `SetupWindow`, not a new onboarding flow.
- No resumable/paused downloads beyond "skip a book whose file already exists" — a failed book during download is logged and skipped; the rest of the batch continues; re-clicking the button later re-fetches only what's still missing. No retry-with-backoff on individual book failures.
- `ScriptureSourceLoader` (`HandsLiftedApp.Importer.Scripture/ScriptureSourceLoader.cs`) and its test file are deleted outright once nothing references them — not deprecated in place.
- `ScriptureLocalUsxStore` must have zero network-capable types (no `HttpClient` field, no `System.Net.Http` usage) reachable from it — this is an architectural guarantee, not a runtime flag.

---

### Task 1: `ScriptureLocalUsxStore` (disk-only reader)

**Files:**
- Create: `HandsLiftedApp.Importer.Scripture/ScriptureLocalUsxStore.cs`
- Create: `HandsLiftedApp.Importer.Scripture/ScriptureBookNotFoundException.cs`
- Test: `HandsLiftedApp.Tests/Importer/Scripture/ScriptureLocalUsxStoreTests.cs`

**Interfaces:**
- Produces: `public sealed class ScriptureLocalUsxStore { public ScriptureLocalUsxStore(string rootPath); public Task<ScriptureBook> LoadBookAsync(string bookCode); }` and `public sealed class ScriptureBookNotFoundException : Exception`. Task 3 constructs and consumes both.
- Consumes: `UsxScriptureParser.Parse(XDocument document)` and `ScriptureBook` (both already exist, unchanged, in `HandsLiftedApp.Importer.Scripture`).

This task does not touch `ScriptureSourceLoader` or anything that currently uses it — it's purely additive. `ScriptureSourceLoader` still exists and is still used by `ScriptureItemInstance` until Task 3.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Importer/Scripture/ScriptureLocalUsxStoreTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureLocalUsxStoreTests"`
Expected: FAIL — compile error, `ScriptureLocalUsxStore` and `ScriptureBookNotFoundException` don't exist yet.

- [ ] **Step 3: Implement `ScriptureBookNotFoundException`**

`HandsLiftedApp.Importer.Scripture/ScriptureBookNotFoundException.cs`:

```csharp
using System;

namespace HandsLiftedApp.Importer.Scripture;

public sealed class ScriptureBookNotFoundException : Exception
{
    public string BookCode { get; }

    public string ExpectedPath { get; }

    public ScriptureBookNotFoundException(string bookCode, string expectedPath)
        : base($"Scripture book '{bookCode}' not found at '{expectedPath}'.")
    {
        BookCode = bookCode;
        ExpectedPath = expectedPath;
    }
}
```

- [ ] **Step 4: Implement `ScriptureLocalUsxStore`**

`HandsLiftedApp.Importer.Scripture/ScriptureLocalUsxStore.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using HandsLiftedApp.Importer.Scripture.Models;

namespace HandsLiftedApp.Importer.Scripture;

public sealed class ScriptureLocalUsxStore
{
    private static readonly Regex ValidIdentifierPattern = new("^[a-z0-9_]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _rootPath;
    private readonly ConcurrentDictionary<string, string> _memoryCache = new(StringComparer.OrdinalIgnoreCase);

    public ScriptureLocalUsxStore(string rootPath)
    {
        _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
    }

    public async Task<ScriptureBook> LoadBookAsync(string bookCode)
    {
        if (string.IsNullOrWhiteSpace(bookCode))
        {
            throw new ArgumentException("Book code is required.", nameof(bookCode));
        }

        if (!ValidIdentifierPattern.IsMatch(bookCode))
        {
            throw new ArgumentException(
                $"Book code '{bookCode}' is invalid; only letters, digits, and underscores are allowed.",
                nameof(bookCode));
        }

        var xml = await GetXmlAsync(bookCode).ConfigureAwait(false);
        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        return UsxScriptureParser.Parse(document);
    }

    private async Task<string> GetXmlAsync(string bookCode)
    {
        var normalizedBook = bookCode.Trim().ToLowerInvariant();

        if (_memoryCache.TryGetValue(normalizedBook, out var cached))
        {
            return cached;
        }

        var diskPath = GetDiskPath(normalizedBook);
        if (!File.Exists(diskPath))
        {
            throw new ScriptureBookNotFoundException(normalizedBook, diskPath);
        }

        var xml = await File.ReadAllTextAsync(diskPath).ConfigureAwait(false);
        _memoryCache[normalizedBook] = xml;
        return xml;
    }

    private string GetDiskPath(string normalizedBook) => Path.Combine(_rootPath, $"{normalizedBook}.usx");
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureLocalUsxStoreTests"`
Expected: PASS (4 tests).

- [ ] **Step 6: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 131 + 4 = 135, no regressions.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Importer.Scripture/ScriptureLocalUsxStore.cs HandsLiftedApp.Importer.Scripture/ScriptureBookNotFoundException.cs HandsLiftedApp.Tests/Importer/Scripture/ScriptureLocalUsxStoreTests.cs
git commit -m "feat: add ScriptureLocalUsxStore, a disk-only USX reader with no network dependency"
```

---

### Task 2: `ScriptureUsxDownloader` (network-only fetcher)

**Files:**
- Create: `HandsLiftedApp.Importer.Scripture/ScriptureUsxDownloader.cs`
- Test: `HandsLiftedApp.Tests/Importer/Scripture/ScriptureUsxDownloaderTests.cs`

**Interfaces:**
- Produces: `public sealed class ScriptureUsxDownloader { public const string FixedTranslation = "eng_bsb"; public static readonly IReadOnlyList<string> AllBookCodes; public ScriptureUsxDownloader(HttpClient? httpClient = null); public Task DownloadAllBooksAsync(string rootPath, IProgress<(int done, int total)>? progress = null, CancellationToken ct = default); }`. Task 4's `SetupWindow` code-behind constructs and calls this.
- Consumes: nothing from Task 1 — this class is independent of `ScriptureLocalUsxStore` (they share the same on-disk file layout — `<root>/<book>.usx` — by convention, not by any shared type).

This task is independent of Task 1 and Task 3; it can be done in any order relative to them, but is numbered here to match the plan's narrative order.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Importer/Scripture/ScriptureUsxDownloaderTests.cs`:

```csharp
using System;
using System.IO;
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

        await downloader.DownloadAllBooksAsync(_tempRoot);

        Assert.IsFalse(File.Exists(Path.Combine(_tempRoot, "gen.usx")));
        Assert.IsTrue(File.Exists(Path.Combine(_tempRoot, "exo.usx")));
        Assert.AreEqual(ScriptureUsxDownloader.AllBookCodes.Count - 1, Directory.GetFiles(_tempRoot, "*.usx").Length);
    }
}
```

`FakeHttpMessageHandler` (`HandsLiftedApp.Tests/Importer/Scripture/FakeHttpMessageHandler.cs`) already exists from Phase 1 and is `internal` (assembly-scoped) — visible here without any change.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureUsxDownloaderTests"`
Expected: FAIL — compile error, `ScriptureUsxDownloader` doesn't exist yet.

- [ ] **Step 3: Implement `ScriptureUsxDownloader`**

`HandsLiftedApp.Importer.Scripture/ScriptureUsxDownloader.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace HandsLiftedApp.Importer.Scripture;

public sealed class ScriptureUsxDownloader
{
    public const string FixedTranslation = "eng_bsb";

    private const string BaseUrl = "https://v1.fetch.bible/bibles/";

    public static readonly IReadOnlyList<string> AllBookCodes = new[]
    {
        "gen", "exo", "lev", "num", "deu", "jos", "jdg", "rut", "1sa", "2sa",
        "1ki", "2ki", "1ch", "2ch", "ezr", "neh", "est", "job", "psa", "pro",
        "ecc", "sng", "isa", "jer", "lam", "ezk", "dan", "hos", "jol", "amo",
        "oba", "jon", "mic", "nam", "hab", "zep", "hag", "zec", "mal",
        "mat", "mrk", "luk", "jhn", "act", "rom", "1co", "2co", "gal", "eph",
        "php", "col", "1th", "2th", "1ti", "2ti", "tit", "phm", "heb", "jas",
        "1pe", "2pe", "1jn", "2jn", "3jn", "jud", "rev"
    };

    private readonly HttpClient _httpClient;

    public ScriptureUsxDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task DownloadAllBooksAsync(string rootPath, IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(rootPath);
        var total = AllBookCodes.Count;
        var done = 0;

        foreach (var bookCode in AllBookCodes)
        {
            ct.ThrowIfCancellationRequested();

            var destPath = Path.Combine(rootPath, $"{bookCode}.usx");
            if (!File.Exists(destPath))
            {
                try
                {
                    await DownloadOneBookAsync(bookCode, destPath, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to download scripture book {BookCode}", bookCode);
                }
            }

            done++;
            progress?.Report((done, total));
        }
    }

    private async Task DownloadOneBookAsync(string bookCode, string destPath, CancellationToken ct)
    {
        var uri = new Uri($"{BaseUrl}{FixedTranslation}/usx/{bookCode}.usx", UriKind.Absolute);
        using var response = await _httpClient.GetAsync(uri, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Scripture fetch failed ({(int)response.StatusCode} {response.ReasonPhrase}) for {bookCode}.");
        }

        var xml = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        var tempPath = destPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(tempPath, xml, ct).ConfigureAwait(false);
        File.Move(tempPath, destPath, overwrite: true);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureUsxDownloaderTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 135 + 3 = 138, no regressions.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Importer.Scripture/ScriptureUsxDownloader.cs HandsLiftedApp.Tests/Importer/Scripture/ScriptureUsxDownloaderTests.cs
git commit -m "feat: add ScriptureUsxDownloader for network-only Bible data pre-download"
```

---

### Task 3: Migrate `ScriptureItemInstance` to the local store, add placeholder-slide error handling, delete `ScriptureSourceLoader`

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`
- Modify: `HandsLiftedApp.Data/Models/Items/ScriptureItem.cs`
- Modify: `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs`
- Delete: `HandsLiftedApp.Importer.Scripture/ScriptureSourceLoader.cs`
- Delete: `HandsLiftedApp.Tests/Importer/Scripture/ScriptureSourceLoaderTests.cs`

**Interfaces:**
- Consumes: `ScriptureLocalUsxStore` and `ScriptureBookNotFoundException` from Task 1.
- Produces: `ScriptureItemInstance`'s constructor signature changes from `(PlaylistInstance? parentPlaylist, ScriptureSourceLoader? loader = null)` to `(PlaylistInstance? parentPlaylist, ScriptureLocalUsxStore? store = null)`. `GenerateSlidesAsync()`'s public signature is unchanged, but it no longer throws on a missing book — it produces a one-slide placeholder instead. Task 4 does not depend on this task's internals, but must land after it (the `ScriptureDataPath` preference this task's default constructor reads is defined in Task 4) — **do Task 4's `AppPreferencesViewModel.ScriptureDataPath` property addition first if executing out of the plan's written order, or land Task 4's property (just the property, not the UI) as a prerequisite before this task's Step 3.**

This task's Step 3 constructs `new ScriptureLocalUsxStore(Globals.Instance.AppPreferences.ScriptureDataPath)` as the default-store fallback — `AppPreferencesViewModel.ScriptureDataPath` must exist before this compiles. To keep task order linear as written, this plan does Task 4's `AppPreferencesViewModel` property change first as this task's Step 0.

- [ ] **Step 0: Add `AppPreferencesViewModel.ScriptureDataPath` (prerequisite for this task's default-store fallback)**

In `HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs`, insert this new property immediately after the existing `LibraryPath` property (after line 158, before the `_ndiMainOutputName` field):

```csharp
        private string _scriptureDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HandsLifted", "ScriptureData");
        [DataMember]
        public string ScriptureDataPath
        {
            get => _scriptureDataPath;
            set => this.RaiseAndSetIfChanged(ref _scriptureDataPath, value);
        }
```

No new `using` needed — `System` and `System.IO` are already imported in this file (lines 1 and 3).

- [ ] **Step 1: Update the existing tests to use the new store (RED)**

Replace the whole content of `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs` with:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureItemInstanceTests
{
    private const string GenesisChapterOneUsx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <usx version="3.0">
          <book code="GEN" style="id">- Genesis</book>
          <para style="mt1">Genesis</para>
          <chapter number="1" style="c" sid="GEN 1"/>
          <para style="p">
            <verse number="1" style="v" sid="GEN 1:1"/>In the beginning God created the heaven and the earth.<verse eid="GEN 1:1"/>
            <verse number="2" style="v" sid="GEN 1:2"/>And the earth was without form, and void.<verse eid="GEN 1:2"/>
            <verse number="3" style="v" sid="GEN 1:3"/>And God said, Let there be light.<verse eid="GEN 1:3"/>
          </para>
          <chapter eid="GEN 1"/>
        </usx>
        """;

    private static ScriptureLocalUsxStore MakeFakeStore(string xml)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureItemInstanceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "gen.usx"), xml);
        return new ScriptureLocalUsxStore(tempDir);
    }

    private static ScriptureLocalUsxStore MakeEmptyStore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureItemInstanceTests_" + Guid.NewGuid().ToString("N"));
        return new ScriptureLocalUsxStore(tempDir);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_ProducesOneSlidePerVerseInRange()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual(2, instance.Slides.Count);
        var first = (ScriptureSlideInstance)instance.Slides[0];
        var second = (ScriptureSlideInstance)instance.Slides[1];
        Assert.AreEqual("In the beginning God created the heaven and the earth.", first.Text);
        Assert.AreEqual("And the earth was without form, and void.", second.Text);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_SlideLabel_UsesParsedBookTitle_NotBookCode()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 1
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        var first = (ScriptureSlideInstance)instance.Slides[0];
        Assert.AreEqual("Genesis 1:1", first.Label);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_SecondCallWithSameRange_PreservesSlideIdentity()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();
        var firstCallSlide = instance.Slides[0];

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();
        var secondCallSlide = instance.Slides[0];

        Assert.AreSame(firstCallSlide, secondCallSlide);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_NarrowerRangeOnRegenerate_ShrinksSlideList()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 3
        };
        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(3, instance.Slides.Count);

        instance.EndVerse = 2;
        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual(2, instance.Slides.Count);
    }

    [TestMethod]
    public async Task ActiveSlide_TracksSelectedSlideIndex()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };
        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        instance.SelectedSlideIndex = 1;

        Assert.AreSame(instance.Slides[1], instance.ActiveSlide);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_BookFileMissing_ProducesPlaceholderSlide()
    {
        var instance = new ScriptureItemInstance(null, MakeEmptyStore())
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual(1, instance.Slides.Count);
        var slide = (ScriptureSlideInstance)instance.Slides[0];
        StringAssert.Contains(slide.Text, "Scripture data not found");
        StringAssert.Contains(slide.Text, "gen");
    }
}
```

- [ ] **Step 2: Run tests to verify the new/changed ones fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureItemInstanceTests"`
Expected: FAIL — compile error, `ScriptureItemInstance`'s constructor still takes `ScriptureSourceLoader`, not `ScriptureLocalUsxStore`; `GenerateSlidesAsync` doesn't yet produce a placeholder slide.

- [ ] **Step 3: Update `ScriptureItemInstance`**

In `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`:

The existing `using HandsLiftedApp.Importer.Scripture;` line needs no change — that namespace holds both the old loader (being removed) and the new store. Add one new line, `using Serilog;`, anywhere in the using block (e.g. immediately before `using ReactiveUI;`) — needed for the `Log.Error` call below. Then replace the constructor and `GenerateSlidesAsync`. Replace:

```csharp
        private readonly ScriptureSourceLoader _loader;

        public ScriptureItemInstance(PlaylistInstance? parentPlaylist, ScriptureSourceLoader? loader = null) : base()
        {
            ParentPlaylist = parentPlaylist;
            _loader = loader ?? new ScriptureSourceLoader();
```

with:

```csharp
        private readonly ScriptureLocalUsxStore _store;

        public ScriptureItemInstance(PlaylistInstance? parentPlaylist, ScriptureLocalUsxStore? store = null) : base()
        {
            ParentPlaylist = parentPlaylist;
            _store = store ?? new ScriptureLocalUsxStore(Globals.Instance.AppPreferences.ScriptureDataPath);
```

Replace:

```csharp
        public async Task GenerateSlidesAsync()
        {
            var book = await _loader.LoadBookAsync(Translation, Book).ConfigureAwait(false);
            var verses = ScriptureVerseRangeExtractor.Extract(book, StartChapter, StartVerse, EndChapter, EndVerse);
            UpdateVerseSlides(book.Title, verses);
        }
```

with:

```csharp
        public async Task GenerateSlidesAsync()
        {
            try
            {
                var book = await _store.LoadBookAsync(Book).ConfigureAwait(false);
                var verses = ScriptureVerseRangeExtractor.Extract(book, StartChapter, StartVerse, EndChapter, EndVerse);
                UpdateVerseSlides(book.Title, verses);
            }
            catch (ScriptureBookNotFoundException ex)
            {
                Log.Error(ex, "Scripture data not found for {Book} ({Translation})", Book, Translation);
                UpdateVerseSlides(Book, MakeMissingDataPlaceholder());
            }
        }

        private System.Collections.Generic.List<ScriptureVerseRef> MakeMissingDataPlaceholder()
        {
            var text =
                $"Scripture data not found: {Book} {StartChapter}:{StartVerse}-{EndChapter}:{EndVerse} ({Translation})\n" +
                "Check Setup > Library > Scripture Data Path";
            return new System.Collections.Generic.List<ScriptureVerseRef> { new ScriptureVerseRef(StartChapter, StartVerse, text) };
        }
```

Add `using Serilog;` to this file's using block (it doesn't have it yet — `Log.Error` above needs it).

- [ ] **Step 4: Update `ScriptureItem.cs`'s doc comment**

In `HandsLiftedApp.Data/Models/Items/ScriptureItem.cs`, replace:

```csharp
    // Deliberately stores only the passage reference, not cached parsed content:
    // HandsLiftedApp.Data has no dependency on HandsLiftedApp.Importer.Scripture (and shouldn't gain
    // one), and ScriptureSourceLoader already caches fetched USX in memory + on disk.
```

with:

```csharp
    // Deliberately stores only the passage reference, not cached parsed content:
    // HandsLiftedApp.Data has no dependency on HandsLiftedApp.Importer.Scripture (and shouldn't gain
    // one), and ScriptureLocalUsxStore already caches parsed USX in memory, reading from a local,
    // user-configured directory (see AppPreferencesViewModel.ScriptureDataPath) rather than the network.
```

- [ ] **Step 5: Delete `ScriptureSourceLoader` and its test file**

```bash
git rm HandsLiftedApp.Importer.Scripture/ScriptureSourceLoader.cs HandsLiftedApp.Tests/Importer/Scripture/ScriptureSourceLoaderTests.cs
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureItemInstanceTests"`
Expected: PASS (6 tests — 5 existing + 1 new placeholder test).

- [ ] **Step 7: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS. Starting count 138 (after Tasks 1–2), minus 7 deleted `ScriptureSourceLoaderTests` tests, plus 1 new placeholder test = 132. No regressions elsewhere (in particular, confirm no other file still references `ScriptureSourceLoader` — `grep -rn "ScriptureSourceLoader" --include=*.cs .` should return nothing outside `docs/`).

- [ ] **Step 8: Commit**

```bash
git add HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs HandsLiftedApp.Data/Models/Items/ScriptureItem.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs
git add -u HandsLiftedApp.Importer.Scripture/ScriptureSourceLoader.cs HandsLiftedApp.Tests/Importer/Scripture/ScriptureSourceLoaderTests.cs
git commit -m "feat: switch ScriptureItemInstance to disk-only ScriptureLocalUsxStore, add missing-data placeholder slide"
```

---

### Task 4: `SetupWindow` UI — Scripture Data path + Download button

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml`
- Modify: `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml.cs`

**Interfaces:**
- Consumes: `AppPreferencesViewModel.ScriptureDataPath` (added in Task 3, Step 0) and `ScriptureUsxDownloader` (Task 2).
- Produces: nothing further downstream — this is the last task in the plan.

No automated UI test harness exists in this codebase for Avalonia windows (consistent with Phase 3/4a precedent — verified by manual run + rigorous code review rather than an automated UI test). This task is verified by running the app and by the existing full test suite staying green (confirming no compile/wiring regression elsewhere).

- [ ] **Step 1: Add the Scripture Data UI to `SetupWindow.axaml`'s Library tab**

In `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml`, inside the `<TabItem Header="Library">` block, insert this markup immediately before the closing `</StackPanel>` (after the existing "Reload library.yml" button, currently ending around line 228):

```xml
                        <TextBlock
                            FontWeight="SemiBold"
                            Margin="0,24,0,4"
                            Text="Scripture Data" />
                        <TextBlock
                            Foreground="{DynamicResource SystemColorGrayTextBrush}"
                            Margin="0,0,0,4"
                            Text="Local folder where scripture (Bible) data is stored. Scripture slides render from this folder only — download must complete before scripture slides can render."
                            TextWrapping="Wrap" />
                        <TextBox Text="{Binding Source={x:Static app:Globals.Instance}, Path=AppPreferences.ScriptureDataPath}" />
                        <Button
                            Click="DownloadScriptureDataButton_OnClick"
                            HorizontalAlignment="Center"
                            Margin="0,12,0,0"
                            MinWidth="200"
                            Padding="20"
                            x:Name="DownloadScriptureDataButton">
                            Download Bible Data
                        </Button>
                        <TextBlock
                            IsVisible="False"
                            Margin="0,8,0,0"
                            x:Name="ScriptureDownloadStatusText" />
```

This reuses the `app:` xmlns alias already used elsewhere in this file (e.g. the `Integrations` tab's `GoogleClientId` binding) — no new namespace import needed.

- [ ] **Step 2: Add the click handler to `SetupWindow.axaml.cs`**

Add `using HandsLiftedApp.Importer.Scripture;` to this file's using block.

Add this method next to the other `*_OnClick` handlers (e.g. immediately after `ReloadLibraryButton_OnClick`):

```csharp
        private void DownloadScriptureDataButton_OnClick(object? sender, RoutedEventArgs e)
        {
            var button = this.Get<Button>("DownloadScriptureDataButton");
            var statusText = this.Get<TextBlock>("ScriptureDownloadStatusText");
            var rootPath = Globals.Instance.AppPreferences.ScriptureDataPath;
            var totalBooks = ScriptureUsxDownloader.AllBookCodes.Count;

            button.IsEnabled = false;
            statusText.IsVisible = true;
            statusText.Text = $"Downloading... 0/{totalBooks} books";

            var progress = new Progress<(int done, int total)>(p =>
            {
                statusText.Text = $"Downloading... {p.done}/{p.total} books";
            });

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var downloader = new ScriptureUsxDownloader();
                    await downloader.DownloadAllBooksAsync(rootPath, progress);

                    Dispatcher.UIThread.Post(() =>
                    {
                        statusText.Text = "Download complete.";
                        button.IsEnabled = true;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        statusText.Text = $"Download failed: {ex.Message}";
                        button.IsEnabled = true;
                    });
                }
            });
        }
```

This follows the exact `Task.Run` + `Dispatcher.UIThread.Post` pattern already used by `SignInWithGoogle_OnClick` in this same file — no `async void` event handler. `Progress<T>` captures the UI thread's `SynchronizationContext` at construction (it's constructed here, synchronously, inside the UI-thread click handler), so its callback marshals automatically — no explicit `Dispatcher.UIThread.Post` needed inside the `progress` lambda itself.

- [ ] **Step 3: Build and run the full test suite**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --nologo`
Expected: build succeeds, no XAML/compile errors.

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, same count as end of Task 3 (this task adds no new tests, per the no-UI-test-harness note above) — confirms nothing else broke.

- [ ] **Step 4: Manual verification**

There is no scripture editor UI yet (Phase 4b), so this step verifies the download button itself manually, and verifies the render-from-disk path with a small throwaway console check rather than through the app's UI.

**4a — Download button.** Run the app (check `docs/superpowers/HANDOVER.md` or the repo's build docs for the exact launch command if unsure — this plan does not introduce a new one). Open Setup, go to the Library tab, confirm:
- The "Scripture Data" section appears below the existing library buttons, with the path pre-filled to the default `%APPDATA%\HandsLifted\ScriptureData`.
- Clicking "Download Bible Data" disables the button, shows "Downloading... N/66 books" ticking up, and finishes with "Download complete." (requires real network access for this one manual check — this is the one intentional, explicit network use in the whole feature).

**4b — Render-from-disk, no network.** After 4a completes, with network access still available, run this throwaway snippet once (e.g. paste into a scratch `.csx`/temporary `Program.cs`, or a temporary `[TestMethod]` you delete afterward — do not commit it):

```csharp
var store = new HandsLiftedApp.Importer.Scripture.ScriptureLocalUsxStore(
    Globals.Instance.AppPreferences.ScriptureDataPath);
var instance = new HandsLiftedApp.Core.Models.RuntimeData.Items.ScriptureItemInstance(null, store)
{
    Translation = "eng_bsb", Book = "jhn", StartChapter = 3, StartVerse = 16, EndChapter = 3, EndVerse = 16
};
await instance.GenerateSlidesAsync();
Avalonia.Threading.Dispatcher.UIThread.RunJobs();
Console.WriteLine(instance.Slides.Count); // expect 1
Console.WriteLine(((HandsLiftedApp.Core.Models.RuntimeData.Slides.ScriptureSlideInstance)instance.Slides[0]).Text); // expect real John 3:16 text, not the placeholder
```

Confirm it prints real verse text (not the "Scripture data not found" placeholder). Then disconnect network entirely and re-run the same snippet against a *different* already-downloaded book (e.g. `"rom"`, `StartChapter = 1, StartVerse = 1`) — confirm it still prints real verse text with no network available, proving the render path never reaches the network.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml.cs
git commit -m "feat: add Scripture Data path + download button to SetupWindow"
```

---

## Final Whole-Branch Review

After all 4 tasks: full suite should be at 132 tests (135 after Task 2, minus 7 deleted `ScriptureSourceLoaderTests`, plus 1 new placeholder-slide test in Task 3; Task 4 adds none). Confirm `grep -rn "ScriptureSourceLoader" --include=*.cs .` (excluding `docs/`) returns nothing. Confirm no test in the suite makes a real network call (grep test files for `HttpClient()` without a `FakeHttpMessageHandler` — the only legitimate real-`HttpClient()` construction left should be inside `ScriptureUsxDownloader`'s own default constructor, never invoked by a test).
