# Scripture Translation Merge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the user configure multiple named scripture translations (each a Setup-screen "Scripture Library" entry: a label, a local USX folder, and — for downloaded ones — a fetch.bible translation code), and pick which translation to pull from when adding scripture to a playlist, replacing the single global `ScriptureDataPath` preference and the never-wired-up, file-browsing `ScriptureLibrary` class.

**Architecture:** `LibraryConfig.LibraryDefinition` (already the Setup screen's per-row model, already persisted to `library.yml`) gains one field, `TranslationCode`. A new pure static helper, `ScriptureTranslationResolver`, resolves a translation label to its local folder given the currently-configured list — used by both slide rendering (`ScriptureItemInstance`) and the Add Scripture dialog. A new `ScriptureTranslationCatalog` class fetches and filters fetch.bible's public manifest for a translation picker. Scripture-type library entries stop being sidebar-browsable (`ScriptureLibrary.cs` is deleted); they become pure configuration.

**Tech Stack:** C# / .NET 10, Avalonia 11 (code-behind views, no bindings in the touched dialogs — matches `ScriptureAddDialog`'s existing style), YamlDotNet (`library.yml`), `System.Text.Json` (fetch.bible manifest), MSTest (`[TestClass]`/`[TestMethod]`, matching this codebase's existing scripture tests).

**Spec:** `docs/superpowers/specs/2026-09-18-scripture-translation-merge-design.md`

## Global Constraints

- Only `license: "public"` (public domain) translations are ever offered in the download picker — no attribution/redistribution obligations. See spec's Decisions section.
- No migration of an existing `ScriptureDataPath` value — clean cutover (confirmed with user).
- Scripture Library entries never become sidebar-browsable `Library` objects — configuration only.
- fetch.bible book fetch URL: `https://v1.fetch.bible/bibles/{translationCode}/usx/{bookCode}.usx` (lowercase book codes). Manifest: `https://v1.fetch.bible/manifest.json`.

---

## Task 1: `TranslationCode` field on `LibraryDefinition`

**Files:**
- Modify: `HandsLiftedApp.Core/Models/Library/Config/LibraryConfig.cs`
- Test: `HandsLiftedApp.Tests/Models/Library/LibraryConfigYamlTests.cs`

**Interfaces:**
- Produces: `LibraryConfig.LibraryDefinition.TranslationCode` (`string?`, get/set, raises `PropertyChanged` like the other fields).

- [ ] **Step 1: Write the failing test**

Add to `HandsLiftedApp.Tests/Models/Library/LibraryConfigYamlTests.cs` (new test method in the existing `LibraryConfigYamlTests` class):

```csharp
[TestMethod]
public void RoundTrips_TranslationCode_OnScriptureEntries()
{
    var config = new LibraryConfig();
    config.LibraryItems.Add(new LibraryConfig.LibraryDefinition
    {
        Label = "KJV", Directory = @"C:\Scripture\KJV", Type = LibraryType.Scripture, TranslationCode = "eng_kjv"
    });

    var serializer = new SerializerBuilder().Build();
    var yaml = serializer.Serialize(config);

    var deserializer = new DeserializerBuilder().Build();
    var roundTripped = deserializer.Deserialize<LibraryConfig>(new StringReader(yaml));

    Assert.AreEqual("eng_kjv", roundTripped.LibraryItems[0].TranslationCode);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~RoundTrips_TranslationCode_OnScriptureEntries"`
Expected: FAIL — compile error, `TranslationCode` doesn't exist yet.

- [ ] **Step 3: Add the field**

In `HandsLiftedApp.Core/Models/Library/Config/LibraryConfig.cs`, inside `LibraryDefinition`, alongside the existing `Directory` field:

```csharp
private string? _translationCode;
public string? TranslationCode { get => _translationCode; set => SetField(ref _translationCode, value); }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~LibraryConfigYamlTests"`
Expected: PASS (both tests in the file).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Models/Library/Config/LibraryConfig.cs HandsLiftedApp.Tests/Models/Library/LibraryConfigYamlTests.cs
git commit -m "feat: add TranslationCode field to LibraryDefinition for scripture translations"
```

---

## Task 2: Parameterize `ScriptureUsxDownloader`'s translation code

**Files:**
- Modify: `HandsLiftedApp.Importer.Scripture/ScriptureUsxDownloader.cs`
- Modify: `HandsLiftedApp.Tests/Importer/Scripture/ScriptureUsxDownloaderTests.cs`

**Interfaces:**
- Produces: `ScriptureUsxDownloader.DownloadAllBooksAsync(string rootPath, string translationCode, IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)`. `FixedTranslation` constant is removed.

- [ ] **Step 1: Update the existing tests to pass a translation code (this is the "write the test first" step — these tests currently pass, they must fail to compile once the constant they implicitly rely on is removed)**

In `HandsLiftedApp.Tests/Importer/Scripture/ScriptureUsxDownloaderTests.cs`, change all four call sites from:

```csharp
await downloader.DownloadAllBooksAsync(_tempRoot);
```

to:

```csharp
await downloader.DownloadAllBooksAsync(_tempRoot, "eng_bsb");
```

(Lines 52, 75, 89, 107 — same pattern each time.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureUsxDownloaderTests"`
Expected: FAIL — compile error, no overload of `DownloadAllBooksAsync` takes 2 positional args yet (current signature is `(rootPath, progress, ct)` with `rootPath` first — actually this will currently compile since `progress`/`ct` are optional and a second positional string doesn't match `IProgress<...>` — expect a genuine compile error: `"eng_bsb"` cannot convert to `IProgress<(int,int)>`).

- [ ] **Step 3: Update the downloader**

In `HandsLiftedApp.Importer.Scripture/ScriptureUsxDownloader.cs`:

```csharp
public sealed class ScriptureUsxDownloader
{
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

    public async Task<int> DownloadAllBooksAsync(string rootPath, string translationCode, IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(rootPath);
        var total = AllBookCodes.Count;
        var done = 0;
        var failed = 0;

        foreach (var bookCode in AllBookCodes)
        {
            ct.ThrowIfCancellationRequested();

            var destPath = Path.Combine(rootPath, $"{bookCode}.usx");
            if (!File.Exists(destPath))
            {
                try
                {
                    await DownloadOneBookAsync(translationCode, bookCode, destPath, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    failed++;
                    Log.Error(ex, "Failed to download scripture book {BookCode} ({TranslationCode})", bookCode, translationCode);
                }
            }

            done++;
            progress?.Report((done, total));
        }

        return failed;
    }

    private async Task DownloadOneBookAsync(string translationCode, string bookCode, string destPath, CancellationToken ct)
    {
        var uri = new Uri($"{BaseUrl}{translationCode}/usx/{bookCode}.usx", UriKind.Absolute);
        using var response = await _httpClient.GetAsync(uri, ct).ConfigureAwait(false);
```

(Everything from `response.EnsureSuccessStatusCode()` onward in `DownloadOneBookAsync` is unchanged — only the signature and the two lines above it change. Remove the old `public const string FixedTranslation = "eng_bsb";` line entirely.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureUsxDownloaderTests"`
Expected: PASS (all 4 tests).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Importer.Scripture/ScriptureUsxDownloader.cs HandsLiftedApp.Tests/Importer/Scripture/ScriptureUsxDownloaderTests.cs
git commit -m "feat: parameterize ScriptureUsxDownloader's translation code"
```

---

## Task 3: `ScriptureTranslationCatalog` — fetch.bible manifest fetch + filter

**Files:**
- Create: `HandsLiftedApp.Importer.Scripture/ScriptureTranslationCatalog.cs`
- Test: `HandsLiftedApp.Tests/Importer/Scripture/ScriptureTranslationCatalogTests.cs`

**Interfaces:**
- Consumes: `HandsLiftedApp.Tests.Importer.Scripture.FakeHttpMessageHandler` (existing test helper, same one `ScriptureUsxDownloaderTests` uses — takes a `Func<HttpRequestMessage, HttpResponseMessage>`).
- Produces: `ScriptureTranslationCatalog(HttpClient? httpClient = null)`, `Task<IReadOnlyList<ScriptureTranslationCatalog.Translation>> GetPublicDomainEnglishTranslationsAsync(CancellationToken ct = default)`, and the record `ScriptureTranslationCatalog.Translation(string Code, string Name, string Abbrev)`.

- [ ] **Step 1: Write the failing test**

Create `HandsLiftedApp.Tests/Importer/Scripture/ScriptureTranslationCatalogTests.cs`:

```csharp
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Importer.Scripture;

[TestClass]
public class ScriptureTranslationCatalogTests
{
    // A trimmed real-shape fixture covering every license shape actually seen in fetch.bible's
    // manifest.json: a bare "public" string, an object with a "license" field (cc-by-sa, not
    // public domain), an object with only forbid_* flags and no "license" field at all (Lexham
    // English Bible's real shape - commercially licensed despite appearing in this catalog), and
    // a non-English entry that must be excluded regardless of its license.
    private const string ManifestJson = """
        {
          "bibles": {
            "eng_bsb": {
              "name": { "english": "Berean Standard Bible", "english_abbrev": "BSB" },
              "copyright": { "licenses": ["public"] }
            },
            "eng_ulb": {
              "name": { "english": "Unlocked Literal Bible", "english_abbrev": "ULB" },
              "copyright": { "licenses": [{ "license": "cc-by-sa", "url": "https://example.com" }] }
            },
            "eng_leb": {
              "name": { "english": "Lexham English Bible", "english_abbrev": "LEB" },
              "copyright": { "licenses": [{ "forbid_commercial": true, "forbid_derivatives": true }] }
            },
            "fra_lsg": {
              "name": { "english": "Louis Segond", "english_abbrev": "LSG" },
              "copyright": { "licenses": ["public"] }
            }
          }
        }
        """;

    [TestMethod]
    public async Task GetPublicDomainEnglishTranslationsAsync_FiltersToEnglishPublicDomainOnly()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ManifestJson) });
        var catalog = new ScriptureTranslationCatalog(new HttpClient(handler));

        var translations = await catalog.GetPublicDomainEnglishTranslationsAsync();

        Assert.AreEqual(1, translations.Count);
        Assert.AreEqual("eng_bsb", translations[0].Code);
        Assert.AreEqual("Berean Standard Bible", translations[0].Name);
        Assert.AreEqual("BSB", translations[0].Abbrev);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureTranslationCatalogTests"`
Expected: FAIL — compile error, `ScriptureTranslationCatalog` doesn't exist yet.

- [ ] **Step 3: Create the catalog class**

Create `HandsLiftedApp.Importer.Scripture/ScriptureTranslationCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace HandsLiftedApp.Importer.Scripture;

public sealed class ScriptureTranslationCatalog
{
    private const string ManifestUrl = "https://v1.fetch.bible/manifest.json";

    private readonly HttpClient _httpClient;

    public ScriptureTranslationCatalog(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public sealed record Translation(string Code, string Name, string Abbrev);

    public async Task<IReadOnlyList<Translation>> GetPublicDomainEnglishTranslationsAsync(CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(ManifestUrl, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var manifest = await response.Content.ReadFromJsonAsync<ManifestRoot>(cancellationToken: ct).ConfigureAwait(false);
        return Filter(manifest);
    }

    internal static IReadOnlyList<Translation> Filter(ManifestRoot? manifest)
    {
        if (manifest?.Bibles == null)
        {
            return Array.Empty<Translation>();
        }

        var result = new List<Translation>();
        foreach (var (code, entry) in manifest.Bibles)
        {
            if (!code.StartsWith("eng_", StringComparison.Ordinal)) continue;
            if (entry.Name?.English is not { Length: > 0 } name) continue;
            if (!IsPublicDomain(entry.Copyright?.Licenses)) continue;

            result.Add(new Translation(code, name, entry.Name.EnglishAbbrev ?? name));
        }

        return result.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Each manifest license entry is either a bare string (e.g. "public", "cc-by-sa") or an
    // object with a "license" string field - but a few entries (Lexham English Bible, NET Bible)
    // instead carry only forbid_* restriction flags with no "license" name at all. Treating
    // anything that isn't an explicit "public" as non-public-domain is both the safe default and
    // happens to be correct for those two specific entries, which are commercially licensed
    // despite appearing in this catalog.
    private static bool IsPublicDomain(List<JsonElement>? licenses)
    {
        if (licenses == null) return false;

        foreach (var license in licenses)
        {
            if (license.ValueKind == JsonValueKind.String && license.GetString() == "public")
            {
                return true;
            }

            if (license.ValueKind == JsonValueKind.Object &&
                license.TryGetProperty("license", out var licenseProp) &&
                licenseProp.ValueKind == JsonValueKind.String &&
                licenseProp.GetString() == "public")
            {
                return true;
            }
        }

        return false;
    }

    internal sealed class ManifestRoot
    {
        [JsonPropertyName("bibles")]
        public Dictionary<string, BibleEntry>? Bibles { get; set; }
    }

    internal sealed class BibleEntry
    {
        [JsonPropertyName("name")]
        public NameInfo? Name { get; set; }

        [JsonPropertyName("copyright")]
        public CopyrightInfo? Copyright { get; set; }
    }

    internal sealed class NameInfo
    {
        [JsonPropertyName("english")]
        public string? English { get; set; }

        [JsonPropertyName("english_abbrev")]
        public string? EnglishAbbrev { get; set; }
    }

    internal sealed class CopyrightInfo
    {
        [JsonPropertyName("licenses")]
        public List<JsonElement>? Licenses { get; set; }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureTranslationCatalogTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Importer.Scripture/ScriptureTranslationCatalog.cs HandsLiftedApp.Tests/Importer/Scripture/ScriptureTranslationCatalogTests.cs
git commit -m "feat: add ScriptureTranslationCatalog for fetch.bible public-domain translation list"
```

---

## Task 4: `ScriptureTranslationResolver` — translation label to local folder

**Files:**
- Create: `HandsLiftedApp.Core/Models/Library/ScriptureTranslationResolver.cs`
- Test: `HandsLiftedApp.Tests/Models/Library/ScriptureTranslationResolverTests.cs`

**Interfaces:**
- Produces: `ScriptureTranslationResolver.ResolveDirectory(string? translationLabel, IEnumerable<LibraryConfig.LibraryDefinition> configuredTranslations)` — a pure function (no `Globals` dependency, so callers pass in the current list; keeps this independently testable the same way `ScriptureItemInstance`'s `_injectedStore` seam does).

- [ ] **Step 1: Write the failing test**

Create `HandsLiftedApp.Tests/Models/Library/ScriptureTranslationResolverTests.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;

namespace HandsLiftedApp.Tests.Models.Library;

[TestClass]
public class ScriptureTranslationResolverTests
{
    private static LibraryConfig.LibraryDefinition MakeTranslation(string label, string directory) =>
        new() { Label = label, Directory = directory, Type = LibraryType.Scripture };

    [TestMethod]
    public void ResolveDirectory_MatchingLabel_ReturnsItsDirectory()
    {
        var configured = new List<LibraryConfig.LibraryDefinition>
        {
            MakeTranslation("BSB", @"C:\Scripture\BSB"),
            MakeTranslation("KJV", @"C:\Scripture\KJV")
        };

        var result = ScriptureTranslationResolver.ResolveDirectory("KJV", configured);

        Assert.AreEqual(@"C:\Scripture\KJV", result);
    }

    [TestMethod]
    public void ResolveDirectory_NoMatchingLabel_FallsBackToFirstConfigured()
    {
        var configured = new List<LibraryConfig.LibraryDefinition>
        {
            MakeTranslation("BSB", @"C:\Scripture\BSB"),
            MakeTranslation("KJV", @"C:\Scripture\KJV")
        };

        var result = ScriptureTranslationResolver.ResolveDirectory("NIV", configured);

        Assert.AreEqual(@"C:\Scripture\BSB", result);
    }

    [TestMethod]
    public void ResolveDirectory_NoneConfigured_ReturnsEmptyString()
    {
        var result = ScriptureTranslationResolver.ResolveDirectory("BSB", new List<LibraryConfig.LibraryDefinition>());

        Assert.AreEqual("", result);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureTranslationResolverTests"`
Expected: FAIL — compile error, `ScriptureTranslationResolver` doesn't exist yet.

- [ ] **Step 3: Create the resolver**

Create `HandsLiftedApp.Core/Models/Library/ScriptureTranslationResolver.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using HandsLiftedApp.Core.Models.Library.Config;

namespace HandsLiftedApp.Core.Models.Library
{
    public static class ScriptureTranslationResolver
    {
        // Falls back to the first configured translation if the label doesn't match any (e.g.
        // stale ScriptureItem.Translation data from before a translation was renamed/removed);
        // returns "" if none are configured at all, matching ScriptureLocalUsxStore's existing
        // not-found handling (empty root path -> File.Exists false -> graceful placeholder).
        public static string ResolveDirectory(string? translationLabel, IEnumerable<LibraryConfig.LibraryDefinition> configuredTranslations)
        {
            var list = configuredTranslations as IReadOnlyCollection<LibraryConfig.LibraryDefinition>
                       ?? configuredTranslations.ToList();
            var match = list.FirstOrDefault(d => d.Label == translationLabel) ?? list.FirstOrDefault();
            return match?.Directory ?? "";
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureTranslationResolverTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Models/Library/ScriptureTranslationResolver.cs HandsLiftedApp.Tests/Models/Library/ScriptureTranslationResolverTests.cs
git commit -m "feat: add ScriptureTranslationResolver for translation-label-to-folder lookup"
```

---

## Task 5: Delete `ScriptureLibrary`, stop building it in `LibraryViewModel`

**Files:**
- Delete: `HandsLiftedApp.Core/Models/Library/ScriptureLibrary.cs`
- Delete: `HandsLiftedApp.Tests/Models/Library/ScriptureLibraryTests.cs`
- Modify: `HandsLiftedApp.Core/ViewModels/LibraryViewModel.cs`
- Test: `HandsLiftedApp.Tests/Models/Library/LibraryViewModelScriptureExclusionTests.cs` (new)

**Interfaces:**
- Produces: `internal static Library? LibraryViewModel.BuildLibraryOrNull(LibraryConfig.LibraryDefinition libDef)` — the per-definition Library-or-not decision, extracted out of `RebuildLibraries()` so it's testable without touching `Globals`/disk. `internal` is visible to `HandsLiftedApp.Tests` via the existing `[assembly: InternalsVisibleTo("HandsLiftedApp.Tests")]` in `HandsLiftedApp.Core/AssemblyInfo.cs`.

`LibraryViewModel`'s constructor calls `ReloadLibraries()`, which calls `LoadConfig()`/`WriteConfig()` against the real `Constants.LIBRARY_CONFIG_FILEPATH` on disk whenever `Avalonia.Controls.Design.IsDesignMode` is false — which it is in a plain test process. No existing test constructs `LibraryViewModel` directly (confirmed: nothing in `HandsLiftedApp.Tests` references `Globals.Instance.MainViewModel` today), so this task must not become the first one to do so — it would read/overwrite the real `library.yml` on whatever machine runs `dotnet test`. Testing the extracted static method instead avoids all of that.

- [ ] **Step 1: Write the failing test**

Create `HandsLiftedApp.Tests/Models/Library/LibraryViewModelScriptureExclusionTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Core.ViewModels;

namespace HandsLiftedApp.Tests.Models.Library;

[TestClass]
public class LibraryViewModelScriptureExclusionTests
{
    [TestMethod]
    public void BuildLibraryOrNull_ScriptureType_ReturnsNull()
    {
        var def = new LibraryConfig.LibraryDefinition { Label = "KJV", Directory = "", Type = LibraryType.Scripture };

        Assert.IsNull(LibraryViewModel.BuildLibraryOrNull(def));
    }

    [TestMethod]
    public void BuildLibraryOrNull_MediaType_ReturnsLibrary()
    {
        var def = new LibraryConfig.LibraryDefinition { Label = "Media", Directory = "", Type = LibraryType.Media };

        Assert.IsNotNull(LibraryViewModel.BuildLibraryOrNull(def));
    }

    [TestMethod]
    public void BuildLibraryOrNull_SongType_ReturnsSongLibrary()
    {
        var def = new LibraryConfig.LibraryDefinition { Label = "Songs", Directory = "", Type = LibraryType.Song };

        Assert.IsInstanceOfType(LibraryViewModel.BuildLibraryOrNull(def), typeof(SongLibrary));
    }
}
```

(`Directory = ""` is safe here — both `Library.Refresh()` and `SongLibrary`'s underlying source check `Directory.Exists(...)` first, which returns `false` for an empty path without throwing; this matches the pattern the now-deleted `ScriptureLibraryTests.cs` and the still-present `LibraryTests.cs`/`SongLibraryTests.cs` already use with real temp directories, just without needing one here since these three tests only care whether a `Library` comes back at all, not its contents.)

Add `using HandsLiftedApp.Core.Models.Library;` to the test file for the `SongLibrary` type reference.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~LibraryViewModelScriptureExclusionTests"`
Expected: FAIL — compile error, `LibraryViewModel.BuildLibraryOrNull` doesn't exist yet.

- [ ] **Step 3: Extract `BuildLibraryOrNull` and use it from `RebuildLibraries()`**

In `HandsLiftedApp.Core/ViewModels/LibraryViewModel.cs`, replace the body of `RebuildLibraries()`'s library-construction block and add the new method:

```csharp
        private void RebuildLibraries()
        {
            Libraries = new ObservableCollection<Library>();

            // Each Library's constructor does its own synchronous directory listing (often over
            // a network/cloud-synced path - see H:\...\Service Docs\* in LibraryConfig above),
            // so building them one at a time serializes N round-trips of I/O latency. Build them
            // concurrently instead. This only touches each Library's own private, not-yet-exposed
            // Items collection during construction - the shared, UI-bound Libraries collection is
            // only mutated afterward, sequentially, on this (UI) thread, so there's no cross-thread
            // binding contention.
            var libDefs = LibraryConfig.LibraryItems;
            var built = new Library?[libDefs.Count];
            Parallel.For(0, libDefs.Count, i =>
            {
                built[i] = BuildLibraryOrNull(libDefs[i]);
            });

            foreach (var lib in built)
            {
                if (lib != null)
                {
                    Libraries.Add(lib);
                }
            }

            var mediaLibraryPath = Globals.Instance.AppPreferences?.MediaLibraryPath;
            if (!string.IsNullOrWhiteSpace(mediaLibraryPath))
            {
                Libraries.Add(new Library(new LibraryConfig.LibraryDefinition
                {
                    Label = "Home",
                    Directory = mediaLibraryPath,
                    Type = LibraryType.Media,
                    Icon = "Home"
                }));
            }
        }

        // Scripture-type entries are excluded (return null) rather than becoming sidebar-browsable
        // libraries - they're translation configuration (see ScriptureTranslationResolver), not
        // something the user browses file-by-file the way Media/Song libraries work.
        internal static Library? BuildLibraryOrNull(LibraryConfig.LibraryDefinition libDef) =>
            libDef.Type switch
            {
                LibraryType.Scripture => null,
                LibraryType.Song => new SongLibrary(libDef, new FileSystemSongLibrarySource(libDef.Directory)),
                _ => new Library(libDef)
            };
```

- [ ] **Step 4: Delete `ScriptureLibrary.cs` and its test**

```bash
git rm HandsLiftedApp.Core/Models/Library/ScriptureLibrary.cs HandsLiftedApp.Tests/Models/Library/ScriptureLibraryTests.cs
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~LibraryViewModelScriptureExclusionTests"`
Expected: PASS (all 3 tests).

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj` — confirm no remaining reference to `ScriptureLibrary` anywhere (the deleted class had exactly one caller, the switch arm just removed).
Expected: Build succeeds, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/ViewModels/LibraryViewModel.cs HandsLiftedApp.Tests/Models/Library/LibraryViewModelScriptureExclusionTests.cs
git commit -m "refactor: stop building sidebar Library entries for Scripture-type config"
```

---

## Task 6: Remove `ScriptureDataPath`; wire rendering through the resolver

**Files:**
- Modify: `HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs`
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`
- Modify: `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml`
- Modify: `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml.cs`

**Interfaces:**
- Consumes: `ScriptureTranslationResolver.ResolveDirectory(string?, IEnumerable<LibraryConfig.LibraryDefinition>)` (Task 4).
- Produces: nothing new — this task retires `AppPreferences.ScriptureDataPath` and its UI.

This task has no new automated test of its own (it's wiring a previously-tested pure function into a class whose existing tests all use the `_injectedStore` seam and are therefore unaffected — see spec's Testing section). Verify manually per Step 4.

- [ ] **Step 1: Remove `ScriptureDataPath` from `AppPreferencesViewModel.cs`**

Delete the property and its backing field at `HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs:167-172` (the `_scriptureDataPath` field, the `[DataMember]` attribute, and the `ScriptureDataPath` property block).

- [ ] **Step 2: Wire `ScriptureItemInstance.GenerateSlidesAsync` through the resolver**

In `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`, change:

```csharp
var store = _injectedStore ?? new ScriptureLocalUsxStore(Globals.Instance.AppPreferences.ScriptureDataPath);
```

to:

```csharp
var configuredTranslations = Globals.Instance.MainViewModel.LibraryViewModel.LibraryConfig.LibraryItems
    .Where(d => d.Type == LibraryType.Scripture);
var store = _injectedStore ?? new ScriptureLocalUsxStore(ScriptureTranslationResolver.ResolveDirectory(Translation, configuredTranslations));
```

Add `using HandsLiftedApp.Core.Models.Library;` and `using HandsLiftedApp.Core.Models.Library.Config;` to this file's usings if not already present (check first — `HandsLiftedApp.Core.Models.RuntimeData.Items` is a different namespace than `HandsLiftedApp.Core.Models.Library`, so both are needed for `ScriptureTranslationResolver` and `LibraryType` respectively).

Also update the two other message strings in this file that reference the old preference path, in `MakeMissingDataPlaceholder`:

```csharp
private List<ScriptureVerseRef> MakeMissingDataPlaceholder()
{
    var text =
        $"Scripture data not found: {Book} {StartChapter}:{StartVerse}-{EndChapter}:{EndVerse} ({Translation})\n" +
        "Check Setup > Library > Scripture Libraries";
    return new List<ScriptureVerseRef> { new ScriptureVerseRef(StartChapter, StartVerse, text) };
}
```

(Only the second string literal changes, from "Check Setup > Library > Scripture Data Path" to "Check Setup > Library > Scripture Libraries", matching the renamed section from Task 7.)

- [ ] **Step 3: Remove the "Scripture Data" section from Setup**

In `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml`, delete the entire block from the "Scripture Data" `TextBlock` through the `ScriptureDownloadStatusText` `TextBlock` (currently lines 289-315 — the section header, description, the `TextBox` bound to `AppPreferences.ScriptureDataPath`, the "Download Bible Data" button, and the status text block). Nothing should remain between the Scripture Libraries section's "+ Add Scripture Library" button (renamed in Task 7) and the end of the outer `StackPanel`/`ScrollViewer`.

In `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml.cs`, delete the `DownloadScriptureDataButton_OnClick` method entirely (currently around lines 126-166 — the whole method, including its `Progress<(int,int)>` and `Task.Run` body).

- [ ] **Step 4: Manual verification**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: Build succeeds, 0 errors (confirms no other file still references `ScriptureDataPath` or `DownloadScriptureDataButton_OnClick` — Task 7/8/9 haven't run yet, but nothing before this task referenced them either per the earlier 5-file grep).

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureItemInstanceTests|FullyQualifiedName~PlaylistInstanceScriptureNavigationTests"`
Expected: PASS — these all use `_injectedStore`, confirming the constructor-injection seam still isolates them from this change.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml.cs
git commit -m "feat: resolve scripture rendering from configured translations, retire ScriptureDataPath"
```

---

## Task 7: "Add Translation" dialog + Setup UI wiring

**Files:**
- Create: `HandsLiftedApp.Core/Views/Setup/AddScriptureTranslationDialog.axaml`
- Create: `HandsLiftedApp.Core/Views/Setup/AddScriptureTranslationDialog.axaml.cs`
- Modify: `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml`
- Modify: `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml.cs`

**Interfaces:**
- Consumes: `ScriptureTranslationCatalog` (Task 3), `ScriptureUsxDownloader.DownloadAllBooksAsync` (Task 2's new signature).
- Produces: `AddScriptureTranslationDialog.Result` (`(string Label, string Directory, string TranslationCode)?`, settable only internally, readable after `ShowDialog`).

This is a new UI dialog with no meaningful pure logic to unit-test in isolation (it's a thin code-behind wrapper around `ScriptureTranslationCatalog` and `ScriptureUsxDownloader`, both already tested in Tasks 2-3) — verify manually per Step 4, matching how `ScriptureAddDialog` itself has no dedicated test file today.

- [ ] **Step 1: Create the dialog's XAML**

Create `HandsLiftedApp.Core/Views/Setup/AddScriptureTranslationDialog.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Width="420"
        Height="280"
        WindowStartupLocation="CenterOwner"
        ExtendClientAreaToDecorationsHint="True"
        Background="Transparent"
        TransparencyLevelHint="Transparent"
        WindowDecorations="None"
        ShowInTaskbar="False"
        CanResize="False"
        x:Class="HandsLiftedApp.Core.Views.Setup.AddScriptureTranslationDialog"
        Icon="/Assets/app.ico"
        Title="Add Translation">
    <Border CornerRadius="8"
            Background="{DynamicResource BackgroundBrush}"
            BorderBrush="{DynamicResource WindowBorderBrush}"
            BorderThickness="1">
        <DockPanel Margin="15">
            <StackPanel Margin="0 10 0 0" DockPanel.Dock="Bottom"
                        Orientation="Horizontal" HorizontalAlignment="Right" Spacing="5">
                <Button Content="Download" x:Name="DownloadButton" IsDefault="True" Click="OnDownloadClick" />
                <Button Content="Cancel" x:Name="CancelButton" IsCancel="True" Click="OnCancelClick" />
            </StackPanel>

            <StackPanel Spacing="8">
                <TextBlock Text="Add Translation" FontWeight="SemiBold" FontSize="14" Margin="0 4 0 0" />

                <TextBlock Text="Translation" />
                <ComboBox x:Name="TranslationComboBox" HorizontalAlignment="Stretch"
                          SelectionChanged="OnTranslationSelectionChanged" />
                <TextBlock x:Name="LoadingText" Text="Loading translations…" FontSize="11" />

                <TextBlock Text="Folder" Margin="0 8 0 0" />
                <DockPanel>
                    <Button Content="Browse…" DockPanel.Dock="Right" Margin="8 0 0 0" Click="OnBrowseClick" />
                    <TextBox x:Name="DirectoryTextBox" TextChanged="OnDirectoryTextChanged" />
                </DockPanel>

                <TextBlock x:Name="StatusText" FontSize="11" TextWrapping="Wrap" MinHeight="28" Margin="0 8 0 0" />
            </StackPanel>
        </DockPanel>
    </Border>
</Window>
```

- [ ] **Step 2: Create the dialog's code-behind**

Create `HandsLiftedApp.Core/Views/Setup/AddScriptureTranslationDialog.axaml.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Core.Views.Setup
{
    public partial class AddScriptureTranslationDialog : Window
    {
        public (string Label, string Directory, string TranslationCode)? Result { get; private set; }

        private ScriptureTranslationCatalog.Translation[] _translations = Array.Empty<ScriptureTranslationCatalog.Translation>();
        private bool _directoryManuallyEdited;

        public AddScriptureTranslationDialog()
        {
            InitializeComponent();
            DownloadButton.IsEnabled = false;

            _ = LoadTranslationsAsync();
        }

        private async Task LoadTranslationsAsync()
        {
            try
            {
                var catalog = new ScriptureTranslationCatalog();
                var translations = await catalog.GetPublicDomainEnglishTranslationsAsync();

                _translations = translations.ToArray();
                TranslationComboBox.ItemsSource = _translations.Select(t => t.Name).ToList();
                if (_translations.Length > 0)
                {
                    TranslationComboBox.SelectedIndex = 0;
                }

                LoadingText.IsVisible = false;
            }
            catch (Exception ex)
            {
                LoadingText.Text = $"Couldn't load translation list: {ex.Message}";
            }
        }

        private void OnTranslationSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_directoryManuallyEdited) return;
            if (TranslationComboBox.SelectedIndex < 0) return;

            var selected = _translations[TranslationComboBox.SelectedIndex];
            DirectoryTextBox.Text = Path.Combine(Constants.APP_DATA_DIR, "ScriptureData", selected.Abbrev);
        }

        private void OnDirectoryTextChanged(object? sender, TextChangedEventArgs e)
        {
            // Any programmatic set below is followed immediately by this handler firing too -
            // there's no way to distinguish "user typed" from "we just set it" via this event
            // alone, so OnTranslationSelectionChanged only auto-fills when nothing has been typed
            // since the dialog opened, and this flag latches true on the very first change,
            // whichever caused it. That means picking a different translation immediately after
            // opening the dialog (before touching the folder box) still auto-fills correctly -
            // the first TextChanged is this class's own initial assignment - but AFTER the user's
            // very first manual edit, subsequent translation picks stop overwriting their choice.
            _directoryManuallyEdited = true;
        }

        private async void OnBrowseClick(object? sender, RoutedEventArgs e)
        {
            var startFolder = !string.IsNullOrWhiteSpace(DirectoryTextBox.Text)
                ? await StorageProvider.TryGetFolderFromPathAsync(DirectoryTextBox.Text)
                : null;

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Translation Folder",
                AllowMultiple = false,
                SuggestedStartLocation = startFolder
            });

            if (folders.Count > 0)
            {
                DirectoryTextBox.Text = folders[0].TryGetLocalPath();
            }
        }

        private async void OnDownloadClick(object? sender, RoutedEventArgs e)
        {
            if (TranslationComboBox.SelectedIndex < 0) return;
            if (string.IsNullOrWhiteSpace(DirectoryTextBox.Text)) return;

            var selected = _translations[TranslationComboBox.SelectedIndex];
            var directory = DirectoryTextBox.Text;

            DownloadButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            TranslationComboBox.IsEnabled = false;
            var totalBooks = ScriptureUsxDownloader.AllBookCodes.Count;
            StatusText.Text = $"Downloading... 0/{totalBooks} books";

            var progress = new Progress<(int done, int total)>(p =>
            {
                StatusText.Text = $"Downloading... {p.done}/{p.total} books";
            });

            try
            {
                var downloader = new ScriptureUsxDownloader();
                var failedCount = await Task.Run(() => downloader.DownloadAllBooksAsync(directory, selected.Code, progress));

                if (failedCount > 0)
                {
                    StatusText.Text = $"Downloaded with {failedCount} book(s) failed (see log). You can still use this translation.";
                }

                Result = (selected.Name, directory, selected.Code);
                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Download failed: {ex.Message}";
                DownloadButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
                TranslationComboBox.IsEnabled = true;
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
    }
}
```

Note: `DownloadButton.IsEnabled = false;` at construction, but nothing re-enables it once translations load — add that at the end of the `try` block in `LoadTranslationsAsync`, right after `LoadingText.IsVisible = false;`:

```csharp
                LoadingText.IsVisible = false;
                DownloadButton.IsEnabled = _translations.Length > 0;
```

- [ ] **Step 3: Wire the Setup window's Scripture section to open this dialog**

In `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml`, change the Scripture Libraries section's add button text and keep its existing `Click="AddScriptureLibraryRowButton_OnClick"` handler name (the handler's *behavior* changes in Step 4, not its XAML wiring):

```xml
<TextBlock FontWeight="SemiBold" Margin="0,16,0,4" Text="Scripture Libraries" />
<ItemsControl ItemsSource="{Binding ScriptureLibraryRows}" ItemTemplate="{StaticResource LibraryRowTemplate}" />
<Button
    Click="AddScriptureLibraryRowButton_OnClick"
    HorizontalAlignment="Left"
    Margin="0,4,0,0">
    + Add Translation
</Button>
```

(Only the button's text content changes, from "+ Add Scripture Library" to "+ Add Translation".)

- [ ] **Step 4: Replace the blank-row add with the dialog flow**

In `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml.cs`, change:

```csharp
private void AddScriptureLibraryRowButton_OnClick(object? sender, RoutedEventArgs e)
{
    _setupWindowViewModel.AddScriptureLibraryRow();
}
```

to:

```csharp
private async void AddScriptureLibraryRowButton_OnClick(object? sender, RoutedEventArgs e)
{
    var dialog = new AddScriptureTranslationDialog();
    await dialog.ShowDialog(this);

    if (dialog.Result is { } result)
    {
        _setupWindowViewModel.AddScriptureLibraryRow(result.Label, result.Directory, result.TranslationCode);
    }
}
```

In `HandsLiftedApp.Core/ViewModels/SetupWindowViewModel.cs`, change:

```csharp
public void AddScriptureLibraryRow() =>
    ScriptureLibraryRows.Add(new LibraryConfig.LibraryDefinition { Label = "New Library", Type = LibraryType.Scripture });
```

to:

```csharp
public void AddScriptureLibraryRow(string label, string directory, string translationCode) =>
    ScriptureLibraryRows.Add(new LibraryConfig.LibraryDefinition
    {
        Label = label, Directory = directory, Type = LibraryType.Scripture, TranslationCode = translationCode
    });
```

- [ ] **Step 5: Manual verification**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: Build succeeds, 0 errors.

Launch the app (or the Setup window specifically, if there's a faster path already used earlier in this project's history), open Setup → Library, click "+ Add Translation" under Scripture Libraries, confirm the translation dropdown populates from the live network call, pick one, confirm the folder auto-fills, click Download, confirm a progress readout appears and a new row shows up in the Scripture Libraries table once it completes, and confirm `library.yml` on disk now has that entry with a `translationcode` field set.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Views/Setup/AddScriptureTranslationDialog.axaml HandsLiftedApp.Core/Views/Setup/AddScriptureTranslationDialog.axaml.cs HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml.cs HandsLiftedApp.Core/ViewModels/SetupWindowViewModel.cs
git commit -m "feat: add translation-picker download dialog for Scripture Libraries"
```

---

## Task 8: Translation picker in `ScriptureAddDialog`

**Files:**
- Modify: `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml`
- Modify: `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs`

**Interfaces:**
- Consumes: `ScriptureTranslationResolver.ResolveDirectory` (Task 4).
- Produces: `ScriptureAddDialog.Result` gains a `Translation` field: `(string BookCode, string BookName, int StartChapter, int StartVerse, int EndChapter, int EndVerse, string Translation)?`. New constructor parameter `string? currentTranslation = null` on both existing constructors, for preselecting when editing an existing item.

No new automated test — this dialog has no existing test file (code-behind, heavily UI-event-driven, matching this codebase's existing pattern of verifying it by hand per its own file's extensive comments about click-through verification). Verify manually per Step 5.

- [ ] **Step 1: Add the translation `ComboBox` to the XAML**

In `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml`, add a `TranslationComboBox` right after the heading, before the Type/Pick mode radio buttons:

```xml
<StackPanel Spacing="8">
    <TextBlock x:Name="HeadingText" Text="Add Scripture" FontWeight="SemiBold" FontSize="14" Margin="0 4 0 0" />

    <TextBlock Text="Translation" />
    <ComboBox x:Name="TranslationComboBox" HorizontalAlignment="Stretch" SelectionChanged="OnTranslationSelectionChanged" />

    <StackPanel Orientation="Horizontal" Spacing="4">
        <RadioButton x:Name="TypeModeRadio" GroupName="ScriptureEntryMode" Content="Type" IsCheckedChanged="OnModeChanged" />
        <RadioButton x:Name="PickModeRadio" GroupName="ScriptureEntryMode" Content="Pick" IsCheckedChanged="OnModeChanged" />
    </StackPanel>
```

(Everything below `</StackPanel>` for the radio buttons is unchanged.)

- [ ] **Step 2: Wire the ComboBox and make `_store` re-buildable in the code-behind**

In `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs`, add the needed usings and change the `_store` field from `readonly`:

```csharp
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
```

```csharp
private ScriptureLocalUsxStore _store;
```

Replace the primary constructor:

```csharp
public ScriptureAddDialog(ScriptureLocalUsxStore? store = null, string? currentTranslation = null)
{
    InitializeComponent();

    var configuredTranslations = Globals.Instance.MainViewModel.LibraryViewModel.LibraryConfig.LibraryItems
        .Where(d => d.Type == LibraryType.Scripture)
        .ToList();
    TranslationComboBox.ItemsSource = configuredTranslations.Select(d => d.Label).ToList();

    var selectedIndex = currentTranslation != null
        ? configuredTranslations.FindIndex(d => d.Label == currentTranslation)
        : -1;
    TranslationComboBox.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (configuredTranslations.Count > 0 ? 0 : -1);

    _store = store ?? new ScriptureLocalUsxStore(
        ScriptureTranslationResolver.ResolveDirectory(SelectedTranslationLabel(), configuredTranslations));

    BookComboBox.ItemsSource = ScriptureBookCatalog.AllBooks.Select(b => b.Name).ToList();
    BookComboBox.SelectedIndex = 0;

    if (s_preferPickMode)
    {
        PickModeRadio.IsChecked = true;
    }
    else
    {
        TypeModeRadio.IsChecked = true;
    }

    _initializing = false;
}

private string? SelectedTranslationLabel() =>
    TranslationComboBox.SelectedIndex >= 0 ? (string)TranslationComboBox.SelectedItem! : null;

private void OnTranslationSelectionChanged(object? sender, SelectionChangedEventArgs e)
{
    if (_initializing) return;

    var configuredTranslations = Globals.Instance.MainViewModel.LibraryViewModel.LibraryConfig.LibraryItems
        .Where(d => d.Type == LibraryType.Scripture);
    _store = new ScriptureLocalUsxStore(ScriptureTranslationResolver.ResolveDirectory(SelectedTranslationLabel(), configuredTranslations));

    // Re-validate whatever's currently typed against the newly selected translation's data -
    // switching translations mid-entry must not leave a stale validation result (e.g. a verse
    // range valid in one translation's versification but not another's) silently in place.
    if (TypeModeRadio.IsChecked == true)
    {
        OnReferenceTextChanged(ReferenceTextBox, null!);
    }
}
```

The second constructor (used for editing an existing item) already calls `: this(store)` — change it to pass the current translation through too:

```csharp
public ScriptureAddDialog(string bookCode, int startChapter, int startVerse, int endChapter, int endVerse, string? currentTranslation = null, ScriptureLocalUsxStore? store = null)
    : this(store, currentTranslation)
{
```

(Only the signature and base-constructor-call line change; the rest of that constructor's body is unchanged.)

`OnReferenceTextChanged`'s signature is `(object? sender, TextChangedEventArgs e)` — calling it directly with `null!` for `e` works because the method body never reads `e` (confirm by re-reading the existing method: it only reads `ReferenceTextBox.Text`, not the event args). This avoids duplicating the debounce/validate logic in a second location.

- [ ] **Step 3: Thread `Translation` into `Result`**

In `OnConfirmInsert`, change both `Result = (...)` assignments to include the selected translation:

```csharp
private void OnConfirmInsert(object? sender, RoutedEventArgs e)
{
    var translation = SelectedTranslationLabel() ?? "";

    if (PickModeRadio.IsChecked == true)
    {
        if (!TryReadPickModeValues(out var bookName, out var startChapter, out var startVerse, out var endChapter, out var endVerse)) return;
        var selected = ScriptureBookCatalog.AllBooks[BookComboBox.SelectedIndex];
        Result = (selected.Code, bookName, startChapter, startVerse, endChapter, endVerse, translation);
        Close();
        return;
    }

    if (!_state.IsValid) return;
    Result = (_state.BookCode!, _state.BookName!, _state.StartChapter, _state.StartVerse, _state.EndChapter, _state.EndVerse, translation);
    Close();
}
```

And update the `Result` property's declared type at the top of the class:

```csharp
public (string BookCode, string BookName, int StartChapter, int StartVerse, int EndChapter, int EndVerse, string Translation)? Result { get; private set; }
```

- [ ] **Step 4: Run the build (this file has no dedicated tests, but its two call sites do compile-check the tuple shape)**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: Build FAILS at this point — `AddItemFlyoutResourceDictionary.axaml.cs` and `ItemEditDockRoot.axaml.cs` (Task 9) still construct the dialog with the old constructor signatures and read `result.BookCode` etc. without the new 7th tuple element. This is expected and gets fixed in Task 9 — do not attempt to make those two files compile from within this task, since threading `Translation` through them is Task 9's own TDD cycle.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs
git commit -m "feat: add translation picker to ScriptureAddDialog"
```

(This commit will not build in isolation — Task 9 completes the chain. If your workflow requires every commit to build, squash Tasks 8 and 9 together instead; they're presented separately here because they're two distinct concerns — a self-contained dialog change vs. plumbing a new field through three call sites — not because either is independently shippable.)

---

## Task 9: Thread `Translation` through `AddItemMessage` and its call sites

**Files:**
- Modify: `HandsLiftedApp.Controls/Messages/AddItemMessage.cs`
- Modify: `HandsLiftedApp.Core/Assets/AddItemFlyoutResourceDictionary.axaml.cs`
- Modify: `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`
- Modify: `HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml.cs`

**Interfaces:**
- Consumes: `ScriptureAddDialog.Result` (Task 8's new 7-element tuple shape).
- Produces: `AddItemMessage.ScriptureTranslation` (`string?`).

No new automated test — this is message-plumbing through existing, already-manually-verified UI event handlers (none of these four files have dedicated unit tests today; they're exercised via the same manual click-through this task's Step 5 covers).

- [ ] **Step 1: Add the field to `AddItemMessage`**

In `HandsLiftedApp.Controls/Messages/AddItemMessage.cs`, add alongside the other `Scripture*` properties:

```csharp
public string? ScriptureTranslation { get; init; }
```

- [ ] **Step 2: Pass it through from the Add flow**

In `HandsLiftedApp.Core/Assets/AddItemFlyoutResourceDictionary.axaml.cs`, change:

```csharp
var dialog = new ScriptureAddDialog();
```

to:

```csharp
var dialog = new ScriptureAddDialog();
```

(unchanged — the no-args overload already resolves to `ScriptureAddDialog(store: null, currentTranslation: null)` via Task 8's default parameters), and change the `AddItemMessage` construction to include:

```csharp
MessageBus.Current.SendMessage(new AddItemMessage
{
    Type = type,
    ItemToInsertAfter = nearestItem,
    InsertIndex = itemInsertIndex,
    ScriptureBookCode = result.BookCode,
    ScriptureBookName = result.BookName,
    ScriptureStartChapter = result.StartChapter,
    ScriptureStartVerse = result.StartVerse,
    ScriptureEndChapter = result.EndChapter,
    ScriptureEndVerse = result.EndVerse,
    ScriptureTranslation = result.Translation
});
```

- [ ] **Step 3: Consume it in `MainViewModel.cs`**

Change:

```csharp
var scripture = new ScriptureItemInstance(Playlist)
{
    Translation = ScriptureUsxDownloader.FixedTranslation,
    Book = addItemMessage.ScriptureBookCode!,
```

to:

```csharp
var scripture = new ScriptureItemInstance(Playlist)
{
    Translation = addItemMessage.ScriptureTranslation ?? "",
    Book = addItemMessage.ScriptureBookCode!,
```

(If `using HandsLiftedApp.Importer.Scripture;` is no longer referenced anywhere else in this file after removing the `ScriptureUsxDownloader.FixedTranslation` reference, leave the `using` in place regardless — `ScriptureUsxDownloader` may still be referenced elsewhere in this large file; do not remove usings speculatively. Check with `grep -n "ScriptureUsxDownloader" HandsLiftedApp.Core/ViewModels/MainViewModel.cs` after this edit — if zero matches remain, remove the now-unused `using`.)

- [ ] **Step 4: Thread it through the edit flow in `ItemEditDockRoot.axaml.cs`**

Change:

```csharp
var dialog = new ScriptureAddDialog(scripture.Book, scripture.StartChapter, scripture.StartVerse,
    scripture.EndChapter, scripture.EndVerse);
await dialog.ShowDialog(parentWindow);
if (dialog.Result == null) return;

var result = dialog.Result.Value;
scripture.Book = result.BookCode;
scripture.StartChapter = result.StartChapter;
scripture.StartVerse = result.StartVerse;
scripture.EndChapter = result.EndChapter;
scripture.EndVerse = result.EndVerse;
scripture.Title = ScriptureTitleFormatter.Format(result.BookName, result.StartChapter,
    result.StartVerse, result.EndChapter, result.EndVerse);
```

to:

```csharp
var dialog = new ScriptureAddDialog(scripture.Book, scripture.StartChapter, scripture.StartVerse,
    scripture.EndChapter, scripture.EndVerse, scripture.Translation);
await dialog.ShowDialog(parentWindow);
if (dialog.Result == null) return;

var result = dialog.Result.Value;
scripture.Book = result.BookCode;
scripture.StartChapter = result.StartChapter;
scripture.StartVerse = result.StartVerse;
scripture.EndChapter = result.EndChapter;
scripture.EndVerse = result.EndVerse;
scripture.Translation = result.Translation;
scripture.Title = ScriptureTitleFormatter.Format(result.BookName, result.StartChapter,
    result.StartVerse, result.EndChapter, result.EndVerse);
```

- [ ] **Step 5: Full build and manual verification**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: Build succeeds, 0 errors — this completes the chain Task 8 left unbuildable.

Run the full test suite: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj`
Expected: All tests pass (this is the first point where the whole plan's changes are simultaneously present — a good checkpoint to run everything, not just the tests touched by this task).

Manual click-through: with at least two Scripture Library translations configured (from Task 7's dialog), open the Add Scripture flow from a playlist, confirm the Translation dropdown lists both, pick the second one, add a verse, confirm the resulting slide renders that translation's text (a KJV verse should visibly differ from a BSB one for the same reference). Then edit that same item and confirm the Translation dropdown reopens pre-selected to the one you picked.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Controls/Messages/AddItemMessage.cs HandsLiftedApp.Core/Assets/AddItemFlyoutResourceDictionary.axaml.cs HandsLiftedApp.Core/ViewModels/MainViewModel.cs HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml.cs
git commit -m "feat: thread selected scripture translation through add/edit flows"
```

---

## Plan self-review notes

- **Spec coverage:** Data model (Task 1), downloader parameterization (Task 2), translation catalog (Task 3), sidebar exclusion + `ScriptureLibrary` deletion (Task 5), `ScriptureDataPath` removal + rendering resolution (Task 6), Add Translation dialog (Task 7), `ScriptureAddDialog` picker (Task 8) — every Decisions-section item in the spec maps to a task. `ScriptureTranslationResolver` (Task 4) wasn't named in the spec's Architecture section verbatim, but implements exactly the "resolution at render/add time is by Label match" decision as a shared, independently-testable helper instead of duplicating the lookup in two places — a refinement discovered while reading the actual call sites (`AddItemMessage` didn't carry a `Translation` field at all, unlike the spec's simplified claim that `MainViewModel.cs` reads `dialog.Result` directly), not a deviation from intent.
- **Type consistency:** `ScriptureAddDialog.Result`'s tuple shape is defined once in Task 8 and consumed with matching field names (`BookCode`, `BookName`, `StartChapter`, `StartVerse`, `EndChapter`, `EndVerse`, `Translation`) in Task 9's two call sites. `ScriptureTranslationResolver.ResolveDirectory`'s signature (Task 4) matches every call site in Tasks 6 and 8. `AddScriptureTranslationDialog.Result`'s shape (Task 7) matches its one call site in the same task.
- **Ordering:** Task 8 deliberately leaves the build broken (documented in its Step 4) because splitting the dialog change from its three call sites keeps each task's diff reviewable on its own; Task 9 restores a green build. If strict per-task buildability is required, execute Tasks 8-9 as one unit.
- **Caught during review:** Task 5's first draft tested through a real `new LibraryViewModel()` + `PersistLibraries()`, which — since no existing test constructs `LibraryViewModel` and `Design.IsDesignMode` is false in a test process — would have read and overwritten the real `library.yml` on whatever machine runs the suite. Replaced with an extracted `internal static LibraryViewModel.BuildLibraryOrNull(...)`, tested directly with no `Globals`/disk involvement at all.
