# Scripture Library + Persistence (Phase 4a) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a `ScriptureItem` a real, first-class library item type — creatable as an XML file on disk, discoverable by a `ScriptureLibrary`, and loadable back into a running `ScriptureItemInstance` — with zero editor UI yet (that's Phase 4b). This phase is fully testable end-to-end without any UI: write a `ScriptureItem` XML file, load it through the same code path the app uses, assert the resulting `ScriptureItemInstance` is correct.

**Architecture:** Three additive pieces, each mirroring an existing `Song*` equivalent: (1) a bug fix to `CreateItem.GenerateItem`'s `.xml` handling, which currently hardcodes `SongItem` for every XML file regardless of its actual root element — this must be fixed before `ScriptureItem` XML files can be loaded at all, Song or not; (2) `ScriptureLibrary : Library` + `LibraryType.Scripture`, wired into `LibraryViewModel`'s library-construction dispatch; (3) a `ScriptureItem` branch in `ItemInstanceFactory.ToItemInstance`.

**Tech Stack:** .NET 8, MSTest, `System.Xml.Serialization`/`System.Xml` (XmlReader for root-element peeking).

## Global Constraints

- net8.0, MSTest, matches Phases 1-3.
- **`CreateItem.GenerateItem`'s `.xml` branch is genuinely broken today for anything other than Song** — it does `new XmlSerializer(typeof(SongItem))` unconditionally (`HandsLiftedApp.Core/CreateItem.cs:124`). This phase fixes it to peek the file's root XML element name and choose the matching type, defaulting to a clear failure (log + `null`) for anything unrecognized — not a silent misparse. No existing test covers this method today (confirmed — zero test files reference `CreateItem`), so Task 1 is this method's first-ever test coverage, including a regression test proving Song `.xml` files still load correctly after the fix.
- **No fuzzy search index for `ScriptureLibrary`** (unlike `SongLibrary`'s `BuildIndexAsync`/lyric-text fuzzy matcher) — YAGNI for a first pass; the inherited base `Library.Search(term)` (substring match on `Title`) is sufficient. `SongLibrary`'s two-tier source/index complexity exists because song search needs to match on lyric *content*, not just filename; scripture items don't have an analogous "free text" search need yet.
- **No "add library" UI is being built** — none exists for ANY library type today (confirmed: the only way to configure a library is hand-editing `library.yml` and clicking "Reload" in `SetupWindow`). A user gets a Scripture library the same way they get any other library today: add a `Type: Scripture` entry to `library.yml` by hand, then reload. This is a pre-existing limitation of the whole app, not something this phase needs to fix.
- **No library-config migration heuristic** for `LibraryType.Scripture` (unlike the existing Song migration that promotes `Media`-typed entries whose label contains "song"/"lyric") — there is no pre-existing user config anywhere with a scripture-like label to migrate, since this is a brand-new feature; a fresh config entry with explicit `Type: Scripture` is how anyone adopts it.
- **`ItemInstanceFactory.ToItemInstance` stays synchronous** — it calls `ScriptureItemInstance.GenerateSlidesAsync()` fire-and-forget (`_ = instance.GenerateSlidesAsync();`), consistent with the fact that this same method's `SongItem` branch also has its `GenerateSlides()` call commented out today (`ItemInstanceFactory.cs:45`) rather than reliably invoked — this phase does not change that established (if inconsistent) convention or attempt to make the whole factory async.
- **`UUID` does not round-trip through saved XML for ANY item type** — `Item.UUID` is `[XmlIgnore]` (`Item.cs:12-13`) and reassigned fresh in `Item`'s parameterless constructor on every deserialize. This is a pre-existing, cross-cutting behavior (not scripture-specific); the round-trip test in this plan does not assert `UUID` equality across save/load, since no item type actually preserves it today.
- **`CreateItem` is `internal` to `HandsLiftedApp.Core`, and no `InternalsVisibleTo` exists anywhere in this repo today** (confirmed — grepped the whole codebase). This is why `CreateItem.GenerateItem` has zero existing test coverage: it's never been reachable from the `HandsLiftedApp.Tests` assembly. Task 1's first step adds a standard `InternalsVisibleTo` attribute so this (and `Constants`, similarly internal and untested) becomes testable — a one-line, additive, zero-risk change, not a visibility redesign.

---

### Task 1: Fix `CreateItem.GenerateItem`'s XML type dispatch

**Files:**
- Create: `HandsLiftedApp.Core/AssemblyInfo.cs`
- Modify: `HandsLiftedApp.Core/CreateItem.cs`
- Test: `HandsLiftedApp.Tests/CreateItemTests.cs`

**Interfaces:**
- Produces: `CreateItem.GenerateItem(string filePath)` now returns the correct concrete `Item` subtype (`SongItem` or `ScriptureItem`) based on the file's actual XML root element, instead of always attempting `SongItem`. Task 3 (`ItemInstanceFactory` round-trip test) depends on this working correctly for `ScriptureItem`.

- [ ] **Step 1: Make `HandsLiftedApp.Core`'s internals visible to the test assembly**

`HandsLiftedApp.Core/AssemblyInfo.cs` (new file):

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("HandsLiftedApp.Tests")]
```

- [ ] **Step 2: Write the failing tests**

`HandsLiftedApp.Tests/CreateItemTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~CreateItemTests"`
Expected: `GenerateItem_SongXml_ReturnsSongItem` PASSES already (current hardcoded behavior happens to handle this case); `GenerateItem_ScriptureXml_ReturnsScriptureItem` FAILS (a `ScriptureItem` XML gets forced through `XmlSerializer(typeof(SongItem))`, which either throws — caught, returns `null` — or produces a `SongItem` with none of the Scripture fields, either way not a `ScriptureItem`); `GenerateItem_UnrecognizedXmlRoot_ReturnsNull` behavior is coincidental today, not verified reliable.

- [ ] **Step 4: Fix `CreateItem.GenerateItem`**

In `HandsLiftedApp.Core/CreateItem.cs`, the current `.xml` branch (lines 120-135) is:

```csharp
            if (filePath.ToLower().EndsWith(".xml"))
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(SongItem));
                    using (FileStream stream = new FileStream(filePath, FileMode.Open))
                    {
                        return (SongItem)serializer.Deserialize(stream);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to parse XML as Song");
                    return null;
                }
            }
```

Replace it with:

```csharp
            if (filePath.ToLower().EndsWith(".xml"))
            {
                try
                {
                    var rootElementName = PeekRootElementName(filePath);
                    Type? itemType = rootElementName switch
                    {
                        "Song" => typeof(SongItem),
                        "Scripture" => typeof(ScriptureItem),
                        _ => null
                    };

                    if (itemType == null)
                    {
                        Log.Error("Unrecognized XML root element '{RootElementName}' in {FilePath}", rootElementName, filePath);
                        return null;
                    }

                    XmlSerializer serializer = new XmlSerializer(itemType);
                    using (FileStream stream = new FileStream(filePath, FileMode.Open))
                    {
                        return (Item)serializer.Deserialize(stream);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to parse XML as Item");
                    return null;
                }
            }
```

Add this private helper method to the same class (`CreateItem`):

```csharp
        private static string? PeekRootElementName(string filePath)
        {
            using var reader = XmlReader.Create(filePath);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    return reader.LocalName;
                }
            }
            return null;
        }
```

`CreateItem.cs`'s current imports (lines 1-10) already include `using System.Xml.Serialization;` (line 7) and `using HandsLiftedApp.Data.Models.Items;` (line 3, covers both `SongItem` and `ScriptureItem` — both live in that namespace). The only new import needed is `using System.Xml;` (for `XmlReader`/`XmlNodeType`) — add it alongside the existing `using System.IO;` (line 5).

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~CreateItemTests"`
Expected: PASS (4 tests).

- [ ] **Step 6: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 122 + 4 = 126, no regressions (in particular, no existing Song-loading behavior anywhere else in the app should be affected — this method's signature and return type are unchanged, only its internal `.xml` dispatch logic changed).

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/CreateItem.cs HandsLiftedApp.Tests/CreateItemTests.cs
git commit -m "fix: dispatch CreateItem.GenerateItem's XML parsing by actual root element, not hardcoded SongItem"
```

---

### Task 2: `ScriptureLibrary` + `LibraryType.Scripture`

**Files:**
- Create: `HandsLiftedApp.Core/Models/Library/ScriptureLibrary.cs`
- Modify: `HandsLiftedApp.Core/Models/Library/Config/LibraryConfig.cs`
- Modify: `HandsLiftedApp.Core/ViewModels/LibraryViewModel.cs`
- Test: `HandsLiftedApp.Tests/Models/Library/ScriptureLibraryTests.cs`

**Interfaces:**
- Produces: `public class ScriptureLibrary : Library` (namespace `HandsLiftedApp.Core.Models.Library`), constructed as `new ScriptureLibrary(libDef)`. `LibraryType.Scripture` enum value. Task 3 doesn't depend on this directly, but a real end-to-end Phase 4b editor will.

- [ ] **Step 1: Write the failing test**

`HandsLiftedApp.Tests/Models/Library/ScriptureLibraryTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;

namespace HandsLiftedApp.Tests.Models.Library;

[TestClass]
public class ScriptureLibraryTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureLibraryTests_" + Guid.NewGuid().ToString("N"));
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
    public void Refresh_OnlyPicksUpXmlFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "john-3-16.xml"), "<Scripture><Title>John 3:16</Title></Scripture>");
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "not a scripture item");

        var config = new LibraryConfig.LibraryDefinition { Label = "Scripture", Directory = _tempDir, Type = LibraryType.Scripture };
        var library = new ScriptureLibrary(config);

        Assert.AreEqual(1, library.Items.Count);
        Assert.IsTrue(library.Items[0].FullFilePath.EndsWith("john-3-16.xml"));
    }

    [TestMethod]
    public void Refresh_EmptyDirectory_ReturnsNoItems()
    {
        var config = new LibraryConfig.LibraryDefinition { Label = "Scripture", Directory = _tempDir, Type = LibraryType.Scripture };
        var library = new ScriptureLibrary(config);

        Assert.AreEqual(0, library.Items.Count);
    }

    [TestMethod]
    public void Refresh_NonExistentDirectory_ReturnsNoItemsWithoutThrowing()
    {
        var config = new LibraryConfig.LibraryDefinition
        {
            Label = "Scripture",
            Directory = Path.Combine(_tempDir, "does-not-exist"),
            Type = LibraryType.Scripture
        };

        var library = new ScriptureLibrary(config);

        Assert.AreEqual(0, library.Items.Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureLibraryTests"`
Expected: FAIL — compile error, `ScriptureLibrary` and `LibraryType.Scripture` don't exist yet.

- [ ] **Step 3: Add `LibraryType.Scripture`**

In `HandsLiftedApp.Core/Models/Library/Config/LibraryConfig.cs`, change:

```csharp
    public enum LibraryType { Song, Media }
```

to:

```csharp
    public enum LibraryType { Song, Media, Scripture }
```

- [ ] **Step 4: Implement `ScriptureLibrary`**

`HandsLiftedApp.Core/Models/Library/ScriptureLibrary.cs`:

```csharp
using System.IO;
using System.Linq;
using HandsLiftedApp.Comparer;
using HandsLiftedApp.Core.Models.Library.Config;
using Serilog;

namespace HandsLiftedApp.Core.Models.Library
{
    // No fuzzy-search index (unlike SongLibrary) — scripture items don't have
    // free-text lyric content to search; the base Library.Search's title
    // substring match is sufficient for a first pass.
    public class ScriptureLibrary : Library
    {
        public ScriptureLibrary(LibraryConfig.LibraryDefinition config) : base(config, ConstructorMode.SkipRefresh)
        {
            isMediaBin = false;
            Refresh();
        }

        protected override void Refresh()
        {
            Items.Clear();

            if (!Directory.Exists(Config.Directory))
            {
                Log.Error("ScriptureLibrary [{Label}] fail - directory [{Directory}] does not exist", Config.Label, Config.Directory);
                return;
            }

            var files = new DirectoryInfo(Config.Directory)
                .GetFiles("*.xml", SearchOption.TopDirectoryOnly)
                .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(f => f.FullName, new NaturalSortStringComparer(System.StringComparison.Ordinal));

            foreach (var f in files)
            {
                Items.Add(new LibraryItem { FullFilePath = f.FullName });
            }

            Log.Information("Refreshed ScriptureLibrary [{Label}] — {Count} items", Config.Label, Items.Count);
        }
    }
}
```

- [ ] **Step 5: Wire the dispatch in `LibraryViewModel.ReloadLibraries()`**

In `HandsLiftedApp.Core/ViewModels/LibraryViewModel.cs`, the current dispatch (around line 111-117) is:

```csharp
            foreach (var libDef in LibraryConfig.LibraryItems)
            {
                Library lib = libDef.Type == LibraryType.Song
                    ? new SongLibrary(libDef, new FileSystemSongLibrarySource(libDef.Directory))
                    : new Library(libDef);
                Libraries.Add(lib);
            }
```

Replace with:

```csharp
            foreach (var libDef in LibraryConfig.LibraryItems)
            {
                Library lib = libDef.Type switch
                {
                    LibraryType.Song => new SongLibrary(libDef, new FileSystemSongLibrarySource(libDef.Directory)),
                    LibraryType.Scripture => new ScriptureLibrary(libDef),
                    _ => new Library(libDef)
                };
                Libraries.Add(lib);
            }
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureLibraryTests"`
Expected: PASS (3 tests).

- [ ] **Step 7: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 126 + 3 = 129, no regressions.

- [ ] **Step 8: Commit**

```bash
git add HandsLiftedApp.Core/Models/Library/ScriptureLibrary.cs HandsLiftedApp.Core/Models/Library/Config/LibraryConfig.cs HandsLiftedApp.Core/ViewModels/LibraryViewModel.cs HandsLiftedApp.Tests/Models/Library/ScriptureLibraryTests.cs
git commit -m "feat: add ScriptureLibrary and LibraryType.Scripture"
```

---

### Task 3: `ItemInstanceFactory` branch + end-to-end round-trip

**Files:**
- Modify: `HandsLiftedApp.Core/ItemInstanceFactory.cs`
- Test: `HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs`

**Interfaces:**
- Consumes: `CreateItem.GenerateItem` (Task 1), `ScriptureItemInstance` (Phase 2).
- Produces: `ItemInstanceFactory.ToItemInstance` now handles `ScriptureItem`, completing the save→discover→load round trip for the first time. This is the last file Phase 4a touches — Phase 4b's editor UI is the first thing that will actually call into all three tasks together from a user action.

- [ ] **Step 1: Write the failing test**

`HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ItemInstanceFactoryTests"`
Expected: FAIL — `ItemInstanceFactory.ToItemInstance` falls through to whatever its final `else` branch does for an unrecognized `Item` subtype (likely returns the raw `deserializedItem` unchanged, which is a `ScriptureItem`, not a `ScriptureItemInstance`) — `Assert.IsInstanceOfType(instance, typeof(ScriptureItemInstance))` fails.

- [ ] **Step 3: Add the `ScriptureItem` branch**

In `HandsLiftedApp.Core/ItemInstanceFactory.cs`, add a new `else if` branch. Find the existing `SongItem` branch (lines 22-47) as the insertion anchor and add the new branch immediately after its closing brace:

```csharp
            else if (deserializedItem is ScriptureItem scriptureItem)
            {
                var scripture = new ScriptureItemInstance(playlist)
                {
                    UUID = scriptureItem.UUID,
                    Title = scriptureItem.Title,
                    Translation = scriptureItem.Translation,
                    Book = scriptureItem.Book,
                    StartChapter = scriptureItem.StartChapter,
                    StartVerse = scriptureItem.StartVerse,
                    EndChapter = scriptureItem.EndChapter,
                    EndVerse = scriptureItem.EndVerse
                };
                // Fire-and-forget: GenerateSlidesAsync fetches over the network and
                // this factory method is synchronous. Slides populate reactively
                // once the fetch completes; ToItemInstance's other branches are
                // similarly inconsistent about invoking their own GenerateSlides
                // (e.g. SongItem's is commented out at the time of writing).
                _ = scripture.GenerateSlidesAsync();
                return scripture;
            }
```

No new `using` statements are needed: `ItemInstanceFactory.cs`'s existing imports already cover `ScriptureItem` (`using HandsLiftedApp.Data.Models.Items;`, line 8) and `ScriptureItemInstance` (`using HandsLiftedApp.Core.Models.RuntimeData.Items;`, line 4). The branch doesn't reference `ScriptureSourceLoader` directly — `ScriptureItemInstance`'s constructor takes it as an optional parameter this call omits, defaulting to a real loader internally.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ItemInstanceFactoryTests"`
Expected: PASS (1 test).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 129 + 1 = 130, no regressions.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/ItemInstanceFactory.cs HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs
git commit -m "feat: add ScriptureItem branch to ItemInstanceFactory, completing save/load round trip"
```

---

## What This Phase Does Not Cover

- No editor UI, no "Add Scripture" button, no passage-entry controls — Phase 4b.
- No "add library" dialog for any library type (pre-existing limitation, not scripture-specific) — a Scripture library is configured via hand-editing `library.yml`, same as every other library type today.
- No fuzzy/content search for scripture items (base title-substring search only).
- The end-to-end round trip (Task 3's test) proves the plumbing works; nothing yet lets a user trigger a save from the running app — that first real save action is Phase 4b's job.
