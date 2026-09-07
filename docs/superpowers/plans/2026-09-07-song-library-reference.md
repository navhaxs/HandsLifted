# Playlist Songs As Library References — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Playlist song items hold a reference (by `UUID`) to a shared library `SongItem` resolved live, instead of a deep copy — so editing a song from any playlist writes through to the library and every other playlist using it.

**Architecture:** `SongItemInstance` becomes a proxy/facade — it keeps its current public property surface but forwards reads/writes to a resolved `SongItem` looked up by `UUID` in a new app-wide `SongLibraryIndex` cache, instead of owning the data locally. Playlist XML shrinks from a full inline song block to a lightweight `SongItemReference` (just the `UUID`, inherited from `Item`). Missing references render a placeholder rather than crashing.

**Tech Stack:** C# / .NET, Avalonia 11, ReactiveUI (`ReactiveObject`, `WhenAnyValue`, `RaiseAndSetIfChanged`), `System.Xml.Serialization.XmlSerializer` for persistence, MSTest (`HandsLiftedApp.Tests`, run via `dotnet test`).

**Spec:** [docs/superpowers/specs/2026-09-07-song-library-reference-design.md](../specs/2026-09-07-song-library-reference-design.md)

## Global Constraints

- Edit semantics are write-through: editing a song's content from a playlist edits the shared library song directly (spec §Decisions 1).
- Song resolution is an in-memory `Dictionary`/`ConcurrentDictionary` keyed by `Guid` (the song's `UUID`), not file path (spec §Decisions 2).
- A missing reference renders a "missing song" placeholder and never throws; no shadow/fallback snapshot is carried in playlist XML (spec §Decisions 3).
- `SongItemInstance` keeps its current public property names/types (`Title`, `Stanzas`, `Arrangement`, `Design`, etc.) — every existing XAML binding, `SongSlideSpecBuilder`, and the song editor VM must keep compiling and working unchanged (spec §Decisions 4).
- A brand-new song drafted in the Song Editor gets its library file created on first "Save to Library" or "Add to Playlist" action — not eagerly on editor open. Closing/cancelling without either leaves no trace (design-fork resolved during planning; not in the original spec text).
- Never shell out to `find`/`grep` — use Glob/Grep tools only (CLAUDE.md tooling rule).
- Never store a disposed measurement `SKTypeface` in a returned render element — not touched by this plan, but keep the existing pattern intact when editing `SongSlideSpecBuilder`/`SongTitleSlideSpecBuilder` (CLAUDE.md SkiaSharp gotcha).
- `SongItemInstance`'s reactive re-render sweep gates on `Cached == null` — any code path that changes a song's rendering-relevant content on an already-cached slide must explicitly reset `Cached = null` for that slide to be picked up (CLAUDE.md reactive-instance gotcha).

---

## File Structure

**New files:**
- `HandsLiftedApp.Data/Models/Items/SongItemReference.cs` — empty `Item` subclass, distinct XML root, carries only the inherited `UUID`.
- `HandsLiftedApp.Core/Models/Library/SongLibraryIndex.cs` — the UUID-keyed cache/resolution/write-through-save service.
- `HandsLiftedApp.Tests/Models/Library/SongLibraryIndexTests.cs`
- `HandsLiftedApp.Tests/Models/Items/SongItemReferenceTests.cs`

**Modified files:**
- `HandsLiftedApp.Data/Models/Items/Item.cs` — `UUID` no longer `[XmlIgnore]`; `Title` becomes `virtual`.
- `HandsLiftedApp.Data/Models/Items/SongItem.cs` — `Design`, `Copyright`, `Stanzas`, `Arrangement`, `Arrangements`, `SelectedArrangementId`, `MotionBackgroundVideoPath`, `StartOnTitleSlide`, `EndOnBlankSlide` become `virtual`.
- `HandsLiftedApp.Data/Models/Playlist.cs` — add `[XmlInclude(typeof(SongItemReference))]`.
- `HandsLiftedApp.Core/Models/Library/SongLibrary.cs` — registers each scanned song into `SongLibraryIndex` (now keyed by the real, persisted `UUID`).
- `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs` — facade rewrite: `ResolvedSong`, overridden forwarding properties, missing-placeholder values, `SongChanged` subscription.
- `HandsLiftedApp.Core/ItemInstanceFactory.cs` — `SongItemReference` and `SongItem` branches rewritten to reference instead of copy.
- `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs` — `SerializeItem`'s song branch emits `SongItemReference` instead of a full `SongItem`.
- `HandsLiftedApp.Core/Views/Editors/SongEditorWindow.axaml.cs` — `DoSaveToLibrary` registers into `SongLibraryIndex`; `AddToPlaylist_OnClick` ensures registration before inserting a reference.
- `HandsLiftedApp.Core/Globals.cs` — expose `SongLibraryIndex` singleton.
- `HandsLiftedApp.Tests/Models/Items/ItemTests.cs`, `HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs`, `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs` — extended with new-behavior tests.

---

### Task 1: Persist `Item.UUID` through XML serialization

**Files:**
- Modify: `HandsLiftedApp.Data/Models/Items/Item.cs:12-13`
- Test: `HandsLiftedApp.Tests/Models/Items/ItemTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `Item.UUID` now round-trips through `XmlSerializer`. Every later task that resolves a song by `UUID` depends on this.

- [ ] **Step 1: Write the failing test**

```csharp
// HandsLiftedApp.Tests/Models/Items/ItemTests.cs — add to the existing test class
[TestMethod]
public void SongItem_UUID_RoundTripsThroughXmlSerialization()
{
    var original = new SongItem { Title = "Amazing Grace" };
    var originalUuid = original.UUID;

    var serializer = new XmlSerializer(typeof(SongItem));
    using var ms = new MemoryStream();
    serializer.Serialize(ms, original);
    ms.Position = 0;
    var roundTripped = (SongItem)serializer.Deserialize(ms)!;

    Assert.AreEqual(originalUuid, roundTripped.UUID);
}

[TestMethod]
public void Item_Clone_StillAssignsFreshUUID()
{
    var original = new SongItem { Title = "Amazing Grace" };
    var clone = (SongItem)original.Clone();

    Assert.AreNotEqual(original.UUID, clone.UUID);
}
```

If `HandsLiftedApp.Tests/Models/Items/ItemTests.cs` does not yet have `using System.IO;` / `using System.Xml.Serialization;` / `using HandsLiftedApp.Data.Models.Items;`, add them.

- [ ] **Step 2: Run tests to verify the first one fails**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SongItem_UUID_RoundTripsThroughXmlSerialization"`
Expected: FAIL — `roundTripped.UUID` is a freshly-generated `Guid` (constructor default), not `originalUuid`. `Item_Clone_StillAssignsFreshUUID` passes already (documents current behavior, must keep passing).

- [ ] **Step 3: Remove `[XmlIgnore]` from `Item.UUID`**

```csharp
// HandsLiftedApp.Data/Models/Items/Item.cs:12-13 — before:
[XmlIgnore]
public Guid UUID { get; set; }

// after:
public Guid UUID { get; set; }
```

- [ ] **Step 4: Run tests to verify both pass**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~ItemTests"`
Expected: PASS. `Item.Clone()` (`Item.cs:43-54`) still explicitly overwrites `UUID` with `Guid.NewGuid()` after the round-trip (line 51), so duplicate-item behavior (`MainViewModel.cs:538-544`) is unaffected by this change.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Data/Models/Items/Item.cs HandsLiftedApp.Tests/Models/Items/ItemTests.cs
git commit -m "$(cat <<'EOF'
fix: persist Item.UUID through XML serialization

UUID was [XmlIgnore], so every deserialize (including every
SongLibrary index rebuild) generated a fresh random UUID. Song
identity needs to survive a reload for the upcoming library-reference
feature to resolve reliably. Item.Clone() already reassigns UUID
after round-tripping, so duplicate-item identity is unaffected.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Add `SongItemReference` XML type

**Files:**
- Create: `HandsLiftedApp.Data/Models/Items/SongItemReference.cs`
- Modify: `HandsLiftedApp.Data/Models/Playlist.cs:17` (add include near the existing `SongItem` include)
- Test: `HandsLiftedApp.Tests/Models/Items/SongItemReferenceTests.cs`

**Interfaces:**
- Consumes: `Item.UUID` (persisted, from Task 1).
- Produces: `SongItemReference` — an `Item` subclass with no fields of its own beyond the inherited `UUID`. Later tasks (`HandsLiftedDocXmlSerializer.SerializeItem`, `ItemInstanceFactory.ToItemInstance`) branch on this type.

- [ ] **Step 1: Write the failing test**

```csharp
// HandsLiftedApp.Tests/Models/Items/SongItemReferenceTests.cs
using System;
using System.IO;
using System.Xml.Serialization;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.Items
{
    [TestClass]
    public class SongItemReferenceTests
    {
        [TestMethod]
        public void RoundTrips_UUID_ThroughXml()
        {
            var reference = new SongItemReference { UUID = Guid.NewGuid() };

            var serializer = new XmlSerializer(typeof(SongItemReference));
            using var ms = new MemoryStream();
            serializer.Serialize(ms, reference);
            ms.Position = 0;
            var roundTripped = (SongItemReference)serializer.Deserialize(ms)!;

            Assert.AreEqual(reference.UUID, roundTripped.UUID);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SongItemReferenceTests"`
Expected: FAIL to compile — `SongItemReference` doesn't exist yet.

- [ ] **Step 3: Create `SongItemReference`**

```csharp
// HandsLiftedApp.Data/Models/Items/SongItemReference.cs
using System;

namespace HandsLiftedApp.Data.Models.Items
{
    /// <summary>
    /// A playlist item that references a library SongItem by UUID rather than embedding
    /// its content. Only UUID (inherited from Item, persisted since Item.UUID stopped being
    /// [XmlIgnore]) carries meaning — resolution happens against SongLibraryIndex at runtime.
    /// </summary>
    [XmlRoot("SongReference", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class SongItemReference : Item
    {
    }
}
```

Add `using System.Xml.Serialization;` to the file for `[XmlRoot]`.

- [ ] **Step 4: Register it as a known Playlist item type**

```csharp
// HandsLiftedApp.Data/Models/Playlist.cs:17 — add directly after the existing SongItem include
[XmlInclude(typeof(SongItem))]
[XmlInclude(typeof(SongItemReference))]
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SongItemReferenceTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Data/Models/Items/SongItemReference.cs HandsLiftedApp.Data/Models/Playlist.cs HandsLiftedApp.Tests/Models/Items/SongItemReferenceTests.cs
git commit -m "$(cat <<'EOF'
feat: add SongItemReference XML type for playlist song references

Empty Item subclass with its own XML root element, carrying only the
inherited UUID. Distinct root name lets DeserializePlaylist tell a
new-format reference apart from an old-format inline SongItem block
by type alone.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Make forwarded `SongItem`/`Item` properties virtual

**Files:**
- Modify: `HandsLiftedApp.Data/Models/Items/Item.cs:26-33` (`Title`)
- Modify: `HandsLiftedApp.Data/Models/Items/SongItem.cs:26-86` (`Design`, `Copyright`, `Stanzas`, `Arrangements`, `SelectedArrangementId`, `MotionBackgroundVideoPath`, `Arrangement`, `EndOnBlankSlide`, `StartOnTitleSlide`)
- Test: none new — this task only changes accessibility modifiers; Task 6's tests exercise the resulting overrides.

**Interfaces:**
- Consumes: nothing new.
- Produces: virtual properties that `SongItemInstance` (Task 6) can override. `SlideTransitionDurationMs` and `Index` are deliberately NOT made virtual — they stay per-playlist-item local state (Global Constraints), not forwarded to the shared song.

- [ ] **Step 1: Mark `Item.Title` virtual**

```csharp
// HandsLiftedApp.Data/Models/Items/Item.cs:24-33 — before:
private string _title = "";
[DataField]
public string Title
{
    get => _title; set
    {
        this.RaiseAndSetIfChanged(ref _title, value);
        this.RaisePropertyChanged(nameof(Slides));
    }
}

// after:
private string _title = "";
[DataField]
public virtual string Title
{
    get => _title; set
    {
        this.RaiseAndSetIfChanged(ref _title, value);
        this.RaisePropertyChanged(nameof(Slides));
    }
}
```

- [ ] **Step 2: Mark the nine forwarded `SongItem` properties virtual**

```csharp
// HandsLiftedApp.Data/Models/Items/SongItem.cs — add `virtual` to each of these nine
// property declarations (leave backing fields and IsEmpty/_isEmpty untouched):

public virtual Guid Design { get => _design; set => this.RaiseAndSetIfChanged(ref _design, value); }

public virtual string Copyright { get => _copyright; set => this.RaiseAndSetIfChanged(ref _copyright, value); }

public virtual TrulyObservableCollection<SongStanza> Stanzas
{
    get => _stanzas;
    set
    {
        this.RaiseAndSetIfChanged(ref _stanzas, value);
    }
}

public virtual SerializableDictionary<string, List<Guid>> Arrangements { get => _arrangements; set => this.RaiseAndSetIfChanged(ref _arrangements, value); }

public virtual string? SelectedArrangementId { get => _selectedArrangementId; set => this.RaiseAndSetIfChanged(ref _selectedArrangementId, value); }

public virtual string? MotionBackgroundVideoPath { get => _motionBackgroundVideoPath; set => this.RaiseAndSetIfChanged(ref _motionBackgroundVideoPath, value); }

public virtual ObservableCollection<Guid> Arrangement
{
    get => _arrangement;
    set => this.RaiseAndSetIfChanged(ref _arrangement, value);
}

public virtual Boolean EndOnBlankSlide { get => _endOnBlankSlide; set => this.RaiseAndSetIfChanged(ref _endOnBlankSlide, value); }

public virtual Boolean StartOnTitleSlide { get => _startOnTitleSlide; set => this.RaiseAndSetIfChanged(ref _startOnTitleSlide, value); }
```

- [ ] **Step 3: Build and run the full existing test suite to confirm nothing broke**

Run: `dotnet build HandsLiftedApp.Data HandsLiftedApp.Core`
Expected: builds clean — `SongItemInstance` currently redeclares `EndOnBlankSlide` with `new` semantics implicitly via shadowing (`SongItemInstance.cs:364-370` declares its own `_endOnBlankSlide`/`EndOnBlankSlide` without `override`); marking the base virtual does not break that shadowing, but Task 6 replaces that shadowed copy with a true `override`, so treat any shadow-related warning here as expected to be resolved by Task 6, not this one.

Run: `dotnet test HandsLiftedApp.Tests`
Expected: PASS (no behavior changed yet, only modifiers).

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Data/Models/Items/Item.cs HandsLiftedApp.Data/Models/Items/SongItem.cs
git commit -m "$(cat <<'EOF'
refactor: mark song-content properties virtual for facade override

Item.Title and nine SongItem properties (Design, Copyright, Stanzas,
Arrangement, Arrangements, SelectedArrangementId,
MotionBackgroundVideoPath, StartOnTitleSlide, EndOnBlankSlide) become
virtual so SongItemInstance can override them to forward to a shared
library song instead of owning the data locally. No behavior change
yet — SongItemInstance still shadows rather than overrides until the
next task.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: `SongLibraryIndex` service

**Files:**
- Create: `HandsLiftedApp.Core/Models/Library/SongLibraryIndex.cs`
- Modify: `HandsLiftedApp.Core/Globals.cs` (expose singleton)
- Test: `HandsLiftedApp.Tests/Models/Library/SongLibraryIndexTests.cs`

**Interfaces:**
- Consumes: `HandsLiftedApp.Core.Utils.RelativeFilePathResolver.ToAbsolutePath(string?, string?)` / `.ToRelativePath(string?, string?)` (existing, `HandsLiftedApp.Core/Utils/RelativeFilePathResolver.cs:8,25`); `HandsLiftedApp.Utils.FilenameUtils.ReplaceInvalidChars(string)` (existing).
- Produces (used by Tasks 5, 6, 7, 9):
  - `SongItem? Resolve(Guid id)`
  - `void Register(SongItem song, string filePath, string libraryDirectory)`
  - `void ImportAndCache(SongItem song, string libraryDirectory)`
  - `void NotifyChanged(Guid id)`
  - `IObservable<Guid> SongChanged`

- [ ] **Step 1: Write the failing tests**

```csharp
// HandsLiftedApp.Tests/Models/Library/SongLibraryIndexTests.cs
using System;
using System.IO;
using System.Reactive.Linq;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.Library
{
    [TestClass]
    public class SongLibraryIndexTests
    {
        private string _libraryDir = "";

        [TestInitialize]
        public void Setup()
        {
            _libraryDir = Path.Combine(Path.GetTempPath(), "SongLibraryIndexTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_libraryDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_libraryDir))
                Directory.Delete(_libraryDir, recursive: true);
        }

        [TestMethod]
        public void Resolve_UnknownId_ReturnsNull()
        {
            var index = new SongLibraryIndex();
            Assert.IsNull(index.Resolve(Guid.NewGuid()));
        }

        [TestMethod]
        public void Register_ThenResolve_ReturnsSameObject()
        {
            var index = new SongLibraryIndex();
            var song = new SongItem { Title = "Amazing Grace" };
            var filePath = Path.Combine(_libraryDir, "Amazing Grace.xml");

            index.Register(song, filePath, _libraryDir);

            Assert.AreSame(song, index.Resolve(song.UUID));
        }

        [TestMethod]
        public void ImportAndCache_WritesFileAndMakesItResolvable()
        {
            var index = new SongLibraryIndex();
            var song = new SongItem { Title = "New Song" };

            index.ImportAndCache(song, _libraryDir);

            Assert.AreSame(song, index.Resolve(song.UUID));
            var expectedPath = Path.Combine(_libraryDir, "New Song.xml");
            Assert.IsTrue(File.Exists(expectedPath), $"Expected file at {expectedPath}");
        }

        [TestMethod]
        public void Register_ExistingUUID_KeepsOriginalObjectIdentity()
        {
            var index = new SongLibraryIndex();
            var first = new SongItem { Title = "First" };
            index.Register(first, Path.Combine(_libraryDir, "song.xml"), _libraryDir);

            var second = new SongItem { UUID = first.UUID, Title = "Second (re-parsed copy)" };
            index.Register(second, Path.Combine(_libraryDir, "song.xml"), _libraryDir);

            Assert.AreSame(first, index.Resolve(first.UUID));
        }

        [TestMethod]
        public void NotifyChanged_EmitsOnSongChanged()
        {
            var index = new SongLibraryIndex();
            var song = new SongItem { Title = "Amazing Grace" };
            index.Register(song, Path.Combine(_libraryDir, "Amazing Grace.xml"), _libraryDir);

            Guid? observed = null;
            using var subscription = index.SongChanged.Subscribe(id => observed = id);

            index.NotifyChanged(song.UUID);

            Assert.AreEqual(song.UUID, observed);
        }

        [TestMethod]
        public void SecondInstance_Referencing_SameSong_SeesWriteThroughEdit()
        {
            var index = new SongLibraryIndex();
            var song = new SongItem { Title = "Original Title" };
            index.Register(song, Path.Combine(_libraryDir, "song.xml"), _libraryDir);

            var resolvedElsewhere = index.Resolve(song.UUID)!;
            resolvedElsewhere.Title = "Edited Title";

            Assert.AreEqual("Edited Title", index.Resolve(song.UUID)!.Title);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SongLibraryIndexTests"`
Expected: FAIL to compile — `SongLibraryIndex` doesn't exist yet.

- [ ] **Step 3: Implement `SongLibraryIndex`**

```csharp
// HandsLiftedApp.Core/Models/Library/SongLibraryIndex.cs
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reactive.Subjects;
using System.Xml.Serialization;
using DebounceThrottle;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Utils;
using Serilog;

namespace HandsLiftedApp.Core.Models.Library
{
    /// <summary>
    /// App-wide, UUID-keyed cache of library SongItems, shared across every configured
    /// SongLibrary. Playlist SongItemInstance facades resolve against this instead of
    /// holding song content locally, so an edit made from one playlist is immediately
    /// visible from any other playlist referencing the same song (write-through).
    /// </summary>
    public class SongLibraryIndex
    {
        private sealed record Entry(SongItem Song, string FilePath, string LibraryDirectory);

        private readonly ConcurrentDictionary<Guid, Entry> _byId = new();
        private readonly ConcurrentDictionary<Guid, DebounceDispatcher> _saveDebouncers = new();
        private readonly Subject<Guid> _songChanged = new();
        private readonly Subject<(Guid Id, Exception Error)> _saveFailed = new();

        public IObservable<Guid> SongChanged => _songChanged;

        /// <summary>
        /// Fires when a debounced write-through save to disk fails. No generic app-wide
        /// error-toast mechanism exists in this codebase today (confirmed: the closest
        /// precedent, commit f5d8e99, is a bespoke Google Slides reauth dialog, not
        /// reusable infra) — this hook exists so UI code can subscribe and surface it
        /// later without SongLibraryIndex itself taking a UI dependency. Until something
        /// subscribes, failures are still visible via Log.Error.
        /// </summary>
        public IObservable<(Guid Id, Exception Error)> SaveFailed => _saveFailed;

        public SongItem? Resolve(Guid id) => _byId.TryGetValue(id, out var entry) ? entry.Song : null;

        /// <summary>
        /// Registers a SongItem already known to have a file at filePath (e.g. from a
        /// SongLibrary scan, or SongItemInstance.AttachToLibrary). Resolves
        /// MotionBackgroundVideoPath to an absolute path (it's stored relative to the
        /// library directory on disk).
        ///
        /// If this UUID already has a live cached object (e.g. attached moments earlier by
        /// the Song Editor, and this call is a subsequent SongLibrary rescan re-parsing the
        /// same file from disk), the existing object identity wins — only its file/directory
        /// bookkeeping is refreshed. This keeps in-flight edits pointed at the one object
        /// actually being edited/rendered, instead of silently swapping them onto a
        /// content-equal but distinct re-parsed copy the next time a library scan runs.
        /// </summary>
        public void Register(SongItem song, string filePath, string libraryDirectory)
        {
            if (_byId.TryGetValue(song.UUID, out var existing))
            {
                _byId[song.UUID] = existing with { FilePath = filePath, LibraryDirectory = libraryDirectory };
                return;
            }

            if (!string.IsNullOrEmpty(song.MotionBackgroundVideoPath))
            {
                song.MotionBackgroundVideoPath =
                    RelativeFilePathResolver.ToAbsolutePath(libraryDirectory, song.MotionBackgroundVideoPath);
            }

            _byId[song.UUID] = new Entry(song, filePath, libraryDirectory);
        }

        /// <summary>
        /// Registers content that has no known library file yet — a brand-new song, or an
        /// old-format playlist's inline song data being migrated — by writing it to a new
        /// file under libraryDirectory and then registering it normally.
        /// </summary>
        public void ImportAndCache(SongItem song, string libraryDirectory)
        {
            var fileName = FilenameUtils.ReplaceInvalidChars(
                string.IsNullOrWhiteSpace(song.Title) ? song.UUID.ToString() : song.Title) + ".xml";
            var filePath = Path.Combine(libraryDirectory, fileName);

            SaveToDisk(song.UUID, song, filePath, libraryDirectory);
            _byId[song.UUID] = new Entry(song, filePath, libraryDirectory);
        }

        /// <summary>
        /// Call after mutating a resolved SongItem's content. Notifies every
        /// SongItemInstance referencing this UUID to re-raise PropertyChanged and
        /// re-render, and debounces a save of the new content to disk.
        /// </summary>
        public void NotifyChanged(Guid id)
        {
            _songChanged.OnNext(id);

            if (!_byId.TryGetValue(id, out var entry))
                return;

            var debouncer = _saveDebouncers.GetOrAdd(id, _ => new DebounceDispatcher(500));
            debouncer.Debounce(() => SaveToDisk(id, entry.Song, entry.FilePath, entry.LibraryDirectory));
        }

        private void SaveToDisk(Guid id, SongItem song, string filePath, string libraryDirectory)
        {
            try
            {
                // MotionBackgroundVideoPath is held in-memory as an absolute path (see
                // Register); store it relative to the library directory so the file stays
                // portable, then restore the absolute path afterward for continued use.
                var absoluteMotionBgPath = song.MotionBackgroundVideoPath;
                if (!string.IsNullOrEmpty(absoluteMotionBgPath) && Path.IsPathFullyQualified(absoluteMotionBgPath))
                {
                    song.MotionBackgroundVideoPath =
                        RelativeFilePathResolver.ToRelativePath(libraryDirectory, absoluteMotionBgPath);
                }

                var serializer = new XmlSerializer(typeof(SongItem));
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    serializer.Serialize(stream, song);
                }

                song.MotionBackgroundVideoPath = absoluteMotionBgPath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SongLibraryIndex: failed to save {FilePath}", filePath);
                _saveFailed.OnNext((id, ex));
            }
        }
    }
}
```

- [ ] **Step 4: Expose the singleton on `Globals`**

```csharp
// HandsLiftedApp.Core/Globals.cs — add alongside the existing SlideRenderQueue property
public HandsLiftedApp.Core.Models.Library.SongLibraryIndex SongLibraryIndex { get; } = new();
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SongLibraryIndexTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/Library/SongLibraryIndex.cs HandsLiftedApp.Core/Globals.cs HandsLiftedApp.Tests/Models/Library/SongLibraryIndexTests.cs
git commit -m "$(cat <<'EOF'
feat: add SongLibraryIndex, a UUID-keyed shared song cache

App-wide cache resolving a song's UUID to its live SongItem object,
shared across every configured song library. Backs the write-through
edit model: NotifyChanged fans out to every playlist referencing the
song and debounce-saves the new content to its library file.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Wire `SongLibrary` scans into `SongLibraryIndex`

**Files:**
- Modify: `HandsLiftedApp.Core/Models/Library/SongLibrary.cs:54-81` (`BuildIndexAsync`)
- Test: extend `HandsLiftedApp.Tests` for `SongLibrary` (find/confirm existing test location via `HandsLiftedApp.Tests/Models/Library/LibraryTests.cs`; add a new `SongLibraryTests.cs` alongside it if `SongLibrary` isn't already covered there)

**Interfaces:**
- Consumes: `SongLibraryIndex.Register(SongItem, string, string)` (Task 4); `Globals.Instance.SongLibraryIndex` (Task 4).
- Produces: every song a `SongLibrary` scans is now resolvable via `Globals.Instance.SongLibraryIndex.Resolve(uuid)` once its async index build completes.

- [ ] **Step 1: Write the failing test**

```csharp
// HandsLiftedApp.Tests/Models/Library/SongLibraryTests.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.Library
{
    [TestClass]
    public class SongLibraryTests
    {
        private string _libraryDir = "";

        [TestInitialize]
        public void Setup()
        {
            _libraryDir = Path.Combine(Path.GetTempPath(), "SongLibraryTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_libraryDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_libraryDir))
                Directory.Delete(_libraryDir, recursive: true);
        }

        [TestMethod]
        public async Task Scan_RegistersEachSongInto_GlobalSongLibraryIndex()
        {
            var song = new SongItem { Title = "Amazing Grace" };
            var filePath = Path.Combine(_libraryDir, "Amazing Grace.xml");
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(SongItem));
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                serializer.Serialize(stream, song);
            }

            var config = new LibraryConfig.LibraryDefinition { Label = "Test", Directory = _libraryDir };
            var library = new SongLibrary(config, new FileSystemSongLibrarySource(_libraryDir));

            // BuildIndexAsync runs fire-and-forget from the constructor; poll briefly.
            for (var i = 0; i < 50 && !library.IsIndexReady; i++)
            {
                await Task.Delay(20);
            }

            Assert.IsTrue(library.IsIndexReady, "Library index did not become ready in time");
            var resolved = Globals.Instance.SongLibraryIndex.Resolve(song.UUID);
            Assert.IsNotNull(resolved);
            Assert.AreEqual("Amazing Grace", resolved!.Title);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SongLibraryTests"`
Expected: FAIL — `Globals.Instance.SongLibraryIndex.Resolve(song.UUID)` returns `null` because `BuildIndexAsync` doesn't register anything into it yet.

- [ ] **Step 3: Register each scanned song into the shared index**

```csharp
// HandsLiftedApp.Core/Models/Library/SongLibrary.cs:54-81 — replace BuildIndexAsync with:
private async Task BuildIndexAsync(List<string> paths)
{
    await Task.Run(() =>
    {
        var idx = new Dictionary<string, SongIndexEntry>(paths.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            try
            {
                var song = CreateItem.GenerateItem(path) as SongItem;
                if (song == null) continue;
                idx[path] = new SongIndexEntry(
                    FilePath:  path,
                    Title:     song.Title ?? Path.GetFileName(path),
                    Copyright: song.Copyright ?? "",
                    LyricText: string.Join(" ", song.Stanzas.Select(s => s.Lyrics ?? ""))
                );
                HandsLiftedApp.Core.Globals.Instance.SongLibraryIndex.Register(song, path, Config.Directory);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SongLibrary: failed to index {Path}", path);
            }
        }
        _index = idx;
    });
    await Dispatcher.UIThread.InvokeAsync(() => IsIndexReady = true);
    Log.Information("SongLibrary [{Label}] index ready — {Count} entries", Config.Label, _index?.Count ?? 0);
}
```

`Config.Directory` is the `LibraryConfig.LibraryDefinition.Directory` this `SongLibrary` was constructed with (`SongLibrary.cs:27-33`), matching the `libraryDirectory` parameter `SongLibraryIndex.Register` expects.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SongLibraryTests"`
Expected: PASS.

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SongLibraryIndexTests|FullyQualifiedName~SongLibraryTests"`
Expected: PASS (no regressions in the pieces built so far).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Models/Library/SongLibrary.cs HandsLiftedApp.Tests/Models/Library/SongLibraryTests.cs
git commit -m "$(cat <<'EOF'
feat: register scanned library songs into SongLibraryIndex

SongLibrary's existing search-index build already deserializes every
song file — register each one's UUID into the shared
SongLibraryIndex at the same time, so playlist references can resolve
against it.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: `SongItemInstance` facade rewrite

This is the largest task. It replaces `SongItemInstance`'s locally-owned song data with forwarding overrides onto a resolved `SongItem`, plus a local-draft fallback for brand-new unsaved songs (Global Constraints).

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs`
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/SongItemInstanceTests.cs` (new file — no existing tests for this class today)

**Interfaces:**
- Consumes: `SongLibraryIndex.Resolve(Guid)`, `.NotifyChanged(Guid)`, `.SongChanged` (Task 4); `Globals.Instance.SongLibraryIndex` (Task 4); the nine virtual `SongItem` properties + virtual `Item.Title` (Task 3).
- Produces:
  - `SongItemInstance.ResolvedSong` (`SongItem?`) — the shared object this instance forwards to, or the local draft, or `null` if truly missing.
  - `SongItemInstance.IsMissing` (`bool`) — true when `ResolvedSong` is `null`.
  - `public static SongItemInstance NewDraft(PlaylistInstance? playlist)` — brand-new, not-yet-library-backed song for the Song Editor's "New Song" flow (used by Task 9).
  - `public void AttachToLibrary(string filePath, string libraryDirectory)` — called once a draft's file exists (Task 9), clears draft state and registers into `SongLibraryIndex`.

- [ ] **Step 1: Write the failing tests**

```csharp
// HandsLiftedApp.Tests/Models/RuntimeData/Items/SongItemInstanceTests.cs
using System;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items
{
    [TestClass]
    public class SongItemInstanceTests
    {
        [TestMethod]
        public void UUID_NotYetRegistered_ResolvesAsMissing()
        {
            var instance = new SongItemInstance(null) { UUID = Guid.NewGuid() };

            Assert.IsTrue(instance.IsMissing);
            Assert.AreEqual("(Missing Song)", instance.Title);
        }

        [TestMethod]
        public void UUID_Registered_ForwardsPropertiesFromSharedSong()
        {
            var song = new HandsLiftedApp.Data.Models.Items.SongItem { Title = "Amazing Grace", Copyright = "PD" };
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");

            var instance = new SongItemInstance(null) { UUID = song.UUID };

            Assert.IsFalse(instance.IsMissing);
            Assert.AreEqual("Amazing Grace", instance.Title);
            Assert.AreEqual("PD", instance.Copyright);
        }

        [TestMethod]
        public void SettingTitle_WritesThroughToSharedSong()
        {
            var song = new HandsLiftedApp.Data.Models.Items.SongItem { Title = "Original" };
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
            var instance = new SongItemInstance(null) { UUID = song.UUID };

            instance.Title = "Edited";

            Assert.AreEqual("Edited", song.Title);
        }

        [TestMethod]
        public void EditingFromOneInstance_IsVisibleFromAnotherInstance_SameUUID()
        {
            var song = new HandsLiftedApp.Data.Models.Items.SongItem { Title = "Original" };
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
            var a = new SongItemInstance(null) { UUID = song.UUID };
            var b = new SongItemInstance(null) { UUID = song.UUID };

            a.Title = "Edited By A";

            Assert.AreEqual("Edited By A", b.Title);
        }

        [TestMethod]
        public void NewDraft_IsNotMissing_AndForwardsToLocalDraft()
        {
            var draft = SongItemInstance.NewDraft(null);
            draft.Title = "Draft Title";

            Assert.IsFalse(draft.IsMissing);
            Assert.AreEqual("Draft Title", draft.Title);
            Assert.IsNull(Globals.Instance.SongLibraryIndex.Resolve(draft.UUID),
                "A draft must not be registered in the shared index until AttachToLibrary is called");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SongItemInstanceTests"`
Expected: FAIL — `IsMissing`, `NewDraft`, `AttachToLibrary` don't exist; `Title`/`Copyright` still read/write local fields, not the shared song.

- [ ] **Step 3: Rewrite the top of `SongItemInstance` — add resolution + forwarding**

```csharp
// HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs
// Add near the top of the class, replacing nothing yet:

public class SongItemInstance : SongItem, IItemInstance, IItemDirtyBit
{
    public PlaylistInstance? ParentPlaylist { get; set; }

    public bool HasThemeSelection => true;

    // A brand-new song authored in the editor that has no library file yet. Non-null only
    // between NewDraft() and AttachToLibrary(); once attached, resolution goes through
    // Globals.Instance.SongLibraryIndex like every other SongItemInstance.
    private SongItem? _localDraft;

    public SongItem? ResolvedSong => Globals.Instance.SongLibraryIndex.Resolve(UUID) ?? _localDraft;

    public bool IsMissing => ResolvedSong == null;

    public static SongItemInstance NewDraft(PlaylistInstance? parentPlaylist)
    {
        var draftSong = new SongItem();
        var instance = new SongItemInstance(parentPlaylist) { UUID = draftSong.UUID };
        instance._localDraft = draftSong;
        return instance;
    }

    /// <summary>
    /// Called once a draft's library file has been written (SongEditorWindow's
    /// save/add-to-playlist flow). Registers the draft's content into the shared index
    /// under this instance's UUID and clears local-draft state — from this point on,
    /// resolution goes through SongLibraryIndex like any other reference.
    /// </summary>
    public void AttachToLibrary(string filePath, string libraryDirectory)
    {
        if (_localDraft == null)
            return; // already attached, or was never a draft — no-op

        Globals.Instance.SongLibraryIndex.Register(_localDraft, filePath, libraryDirectory);
        _localDraft = null;
        this.RaisePropertyChanged(nameof(IsMissing));
    }

    public override string Title
    {
        get => ResolvedSong?.Title ?? "(Missing Song)";
        set { if (ResolvedSong is { } s) { s.Title = value; NotifySharedSongChanged(); } }
    }

    public override Guid Design
    {
        get => ResolvedSong?.Design ?? Guid.Empty;
        set { if (ResolvedSong is { } s) { s.Design = value; NotifySharedSongChanged(); } }
    }

    public override string Copyright
    {
        get => ResolvedSong?.Copyright ?? "";
        set { if (ResolvedSong is { } s) { s.Copyright = value; NotifySharedSongChanged(); } }
    }

    public override TrulyObservableCollection<SongStanza> Stanzas
    {
        get => ResolvedSong?.Stanzas ?? new TrulyObservableCollection<SongStanza>();
        set { if (ResolvedSong is { } s) { s.Stanzas = value; NotifySharedSongChanged(); } }
    }

    public override SerializableDictionary<string, List<Guid>> Arrangements
    {
        get => ResolvedSong?.Arrangements ?? new SerializableDictionary<string, List<Guid>>();
        set { if (ResolvedSong is { } s) { s.Arrangements = value; NotifySharedSongChanged(); } }
    }

    public override string? SelectedArrangementId
    {
        get => ResolvedSong?.SelectedArrangementId;
        set { if (ResolvedSong is { } s) { s.SelectedArrangementId = value; NotifySharedSongChanged(); } }
    }

    public override string? MotionBackgroundVideoPath
    {
        get => ResolvedSong?.MotionBackgroundVideoPath;
        set { if (ResolvedSong is { } s) { s.MotionBackgroundVideoPath = value; NotifySharedSongChanged(); } }
    }

    public override ObservableCollection<Guid> Arrangement
    {
        get => ResolvedSong?.Arrangement ?? new ObservableCollection<Guid>();
        set { if (ResolvedSong is { } s) { s.Arrangement = value; NotifySharedSongChanged(); } }
    }

    public override Boolean EndOnBlankSlide
    {
        get => ResolvedSong?.EndOnBlankSlide ?? true;
        set { if (ResolvedSong is { } s) { s.EndOnBlankSlide = value; NotifySharedSongChanged(); } }
    }

    public override Boolean StartOnTitleSlide
    {
        get => ResolvedSong?.StartOnTitleSlide ?? true;
        set { if (ResolvedSong is { } s) { s.StartOnTitleSlide = value; NotifySharedSongChanged(); } }
    }

    private void NotifySharedSongChanged()
    {
        if (_localDraft != null)
        {
            // Not yet attached to the library — nothing else can be watching this UUID yet,
            // just re-render this instance's own slides.
            RaiseForwardedPropertiesChanged();
            debounceDispatcher.Debounce(() => UpdateStanzaSlides());
            return;
        }

        Globals.Instance.SongLibraryIndex.NotifyChanged(UUID);
    }

    private void RaiseForwardedPropertiesChanged()
    {
        this.RaisePropertyChanged(nameof(Title));
        this.RaisePropertyChanged(nameof(Design));
        this.RaisePropertyChanged(nameof(ResolvedDesignTheme));
        this.RaisePropertyChanged(nameof(Copyright));
        this.RaisePropertyChanged(nameof(Stanzas));
        this.RaisePropertyChanged(nameof(Arrangements));
        this.RaisePropertyChanged(nameof(SelectedArrangementId));
        this.RaisePropertyChanged(nameof(MotionBackgroundVideoPath));
        this.RaisePropertyChanged(nameof(Arrangement));
        this.RaisePropertyChanged(nameof(EndOnBlankSlide));
        this.RaisePropertyChanged(nameof(StartOnTitleSlide));
        this.RaisePropertyChanged(nameof(IsMissing));
    }
    // ... rest of class continues below (constructor, GenerateArrangementViews, etc.)
```

- [ ] **Step 4: Subscribe to `SongChanged` in the constructor**

```csharp
// HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs — inside the existing
// constructor (currently starting at line 61), add near the top, before the existing
// this.WhenAnyValue(x => x.Design)... subscription:

public SongItemInstance(PlaylistInstance? parentPlaylist) : base()
{
    ParentPlaylist = parentPlaylist;

    Globals.Instance.SongLibraryIndex.SongChanged
        .Where(changedId => changedId == UUID)
        .ObserveOn(RxSchedulers.MainThreadScheduler)
        .Subscribe(_ =>
        {
            RaiseForwardedPropertiesChanged();
            // Content changed on the shared song — force the Cached == null re-render sweep
            // (existing gotcha: reassigning a watched property alone doesn't guarantee a
            // re-render if the subscription chain doesn't happen to fire for it).
            foreach (var slide in Slides.OfType<SongSlideInstance>())
                slide.Cached = null;
            if (TitleSlide is SongTitleSlideInstance titleInst)
                titleInst.Cached = null;
            debounceDispatcher.Debounce(() => UpdateStanzaSlides());
        });

    this.WhenAnyValue(x => x.Design)
        .Subscribe(_ => this.RaisePropertyChanged(nameof(ResolvedDesignTheme)));

    // ... existing constructor body unchanged from here
```

Note: `UUID` must already be set before this subscription filter is meaningful. Every construction site in this plan (`ItemInstanceFactory`, `NewDraft`, XAML/editor) sets `UUID` via object-initializer syntax immediately after calling this constructor — the filter re-evaluates on every `SongChanged` emission via the closure reading `UUID` live, so setting it after construction (as the existing call sites already do) works correctly.

- [ ] **Step 5: Remove the now-redundant shadowed `EndOnBlankSlide` field/property**

```csharp
// HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs:364-370 — delete this
// block entirely (the override added in Step 3 replaces it):
[XmlIgnore] private Boolean _endOnBlankSlide = true;

public Boolean EndOnBlankSlide
{
    get => _endOnBlankSlide;
    set => this.RaiseAndSetIfChanged(ref _endOnBlankSlide, value);
}
```

- [ ] **Step 6: Add `using System.Reactive.Linq;` if not already present**

Already present at `SongItemInstance.cs:7` — confirm, no change needed.

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SongItemInstanceTests"`
Expected: PASS.

Run: `dotnet test HandsLiftedApp.Tests`
Expected: PASS. Pay particular attention to any test touching `SongSlideSpecBuilder`/`SongTitleSlideSpecBuilder` (`HandsLiftedApp.Tests/Render/Skia/Builders/`) — they read the generated `Slide` objects, not `SongItemInstance` fields directly, and should be unaffected, but this is the first point they'd catch a forwarding mistake.

- [ ] **Step 8: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/SongItemInstanceTests.cs
git commit -m "$(cat <<'EOF'
feat: SongItemInstance forwards song content to a resolved library song

SongItemInstance no longer owns Title/Stanzas/Design/etc. locally —
it resolves its UUID against SongLibraryIndex and forwards reads and
writes there (write-through), falling back to a local draft for
brand-new songs that haven't been saved to the library yet. Missing
references (UUID not resolvable, no draft) read back placeholder
values instead of throwing. Existing XAML bindings and spec builders
are unaffected since the public property surface is unchanged.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: `ItemInstanceFactory` — reference instead of copy

**Files:**
- Modify: `HandsLiftedApp.Core/ItemInstanceFactory.cs:23-49`
- Test: `HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs` (extend existing file)

**Interfaces:**
- Consumes: `SongItemReference` (Task 2), `SongLibraryIndex.Resolve`/`.ImportAndCache` (Task 4), `SongItemInstance` facade (Task 6).
- Produces: `ItemInstanceFactory.ToItemInstance` now handles both `SongItemReference` (new-format playlist load, or fresh add-from-library) and `SongItem` (legacy inline playlist data, or a freshly-parsed library file) without copying song content field-by-field.

- [ ] **Step 1: Write the failing tests**

```csharp
// HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs — add to the existing test class
[TestMethod]
public void ToItemInstance_SongItemReference_ResolvesAgainstLibraryIndex()
{
    var song = new SongItem { Title = "Amazing Grace" };
    Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
    var reference = new SongItemReference { UUID = song.UUID };

    var result = ItemInstanceFactory.ToItemInstance(reference, null);

    var instance = Assert.That.IsInstanceOfType<SongItemInstance>(result);
    Assert.AreEqual("Amazing Grace", instance.Title);
    Assert.AreEqual(song.UUID, instance.UUID);
}

[TestMethod]
public void ToItemInstance_LegacyInlineSongItem_ImportsIntoLibraryIndex_WhenNotAlreadyKnown()
{
    var inlineSong = new SongItem { Title = "Legacy Inline Song", Copyright = "PD" };

    var result = ItemInstanceFactory.ToItemInstance(inlineSong, null);

    var instance = Assert.That.IsInstanceOfType<SongItemInstance>(result);
    Assert.AreEqual("Legacy Inline Song", instance.Title);
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
```

If MSTest's `Assert.That.IsInstanceOfType<T>` isn't available in this project's MSTest version, use the pattern already established elsewhere in `ItemInstanceFactoryTests.cs` (check the file's existing assertions for the idiom in use, e.g. `Assert.IsInstanceOfType(result, typeof(SongItemInstance)); var instance = (SongItemInstance)result;`) and match it.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~ItemInstanceFactoryTests"`
Expected: FAIL — no `SongItemReference` branch exists yet, and the `SongItem` branch still copies fields instead of importing/resolving.

- [ ] **Step 3: Rewrite the song branches**

```csharp
// HandsLiftedApp.Core/ItemInstanceFactory.cs:23-49 — replace the existing
// "else if (deserializedItem is SongItem songItem)" branch with two branches:

else if (deserializedItem is SongItemReference songReference)
{
    return new SongItemInstance(playlist) { UUID = songReference.UUID };
}
else if (deserializedItem is SongItem songItem)
{
    // Either a freshly-parsed library file (add-from-library flow — CreateItem.GenerateItem
    // just deserialized it) or legacy inline playlist content (old-format playlist file,
    // pre-dating SongItemReference). Both cases: if this UUID isn't already known to the
    // shared index, this object is the best available content for it — import it so the
    // reference resolves. If it's already known (the common add-from-library case, since
    // SongLibrary's scan already registered it), leave the existing cached entry alone
    // rather than overwriting it with what may be a stale re-parse.
    if (Globals.Instance.SongLibraryIndex.Resolve(songItem.UUID) == null)
    {
        var libraryDirectory = playlistDirectoryPath ?? Path.GetTempPath();
        Globals.Instance.SongLibraryIndex.ImportAndCache(songItem, libraryDirectory);
    }

    return new SongItemInstance(playlist) { UUID = songItem.UUID };
}
```

Add `using System.IO;` to `HandsLiftedApp.Core/ItemInstanceFactory.cs` if not already present.

Note on `libraryDirectory` for the legacy-migration case: this plan does not have a configured song library directory available at this call site (only `playlist?.PlaylistWorkingDirectory`). Falling back to writing the migrated song file into the playlist's own working directory means it becomes a de facto one-song "library" that only `SongLibraryIndex`'s in-memory cache knows about for this process — it will NOT show up in `SongLibrary`'s scanned `Items` (browsable library list) until a user manually files it into a real configured library directory. This is an accepted limitation for legacy migration; flag it to the user in the manual verification pass (Task 10) rather than solving library-directory selection here — YAGNI unless real legacy playlists surface this in practice.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~ItemInstanceFactoryTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/ItemInstanceFactory.cs HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs
git commit -m "$(cat <<'EOF'
feat: ItemInstanceFactory builds song references instead of copies

SongItemReference resolves directly against SongLibraryIndex. A plain
SongItem (add-from-library, or legacy inline playlist content) is
imported into the index only when its UUID isn't already known there,
so a fresh library scan's entry is never clobbered by a stale re-parse.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: `HandsLiftedDocXmlSerializer` — serialize songs as references

**Files:**
- Modify: `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs:128-162`
- Test: `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs` (extend existing file)

**Interfaces:**
- Consumes: `SongItemReference` (Task 2).
- Produces: `SerializeItem` for a `SongItemInstance` now returns a `SongItemReference` (just `UUID`) instead of a full inline `SongItem`. `DeserializePlaylist` needs no changes — `XmlSerializer` already resolves `SongItemReference` vs `SongItem` polymorphically via the `[XmlInclude]` list from Task 2.

- [ ] **Step 1: Write the failing test**

```csharp
// HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs — add to the existing test class
[TestMethod]
public void SerializeItem_SongItemInstance_ReturnsReferenceNotFullContent()
{
    var song = new SongItem { Title = "Amazing Grace", Copyright = "PD" };
    Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
    var instance = new SongItemInstance(null) { UUID = song.UUID };

    var serialized = HandsLiftedDocXmlSerializer.SerializeItem(instance, "irrelevant-playlist-dir");

    var reference = Assert.That.IsInstanceOfType<SongItemReference>(serialized);
    Assert.AreEqual(song.UUID, reference.UUID);
}
```

(Match this project's existing `IsInstanceOfType` idiom as in Task 7 Step 1 if `Assert.That.IsInstanceOfType<T>` isn't available.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SerializeItem_SongItemInstance_ReturnsReferenceNotFullContent"`
Expected: FAIL — current code returns a full `SongItem`, not a `SongItemReference`.

- [ ] **Step 3: Replace the song serialization branch**

```csharp
// HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs:128-162 — replace the entire
// "else if (item is SongItemInstance songItemInstance)" block with:
else if (item is SongItemInstance songItemInstance)
{
    return new SongItemReference { UUID = songItemInstance.UUID };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~HandsLiftedDocXmlSerializerTests"`
Expected: PASS.

- [ ] **Step 5: Add a round-trip regression test through the full playlist serializer**

```csharp
// HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs — add
[TestMethod]
public void SerializePlaylist_ThenDeserialize_SongItem_RoundTripsAsReference()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "PlaylistSerializerTests_" + Guid.NewGuid());
    Directory.CreateDirectory(tempDir);
    try
    {
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var playlist = new PlaylistInstance { Title = "Test Playlist" };
        playlist.Items.Add(new SongItemInstance(playlist) { UUID = song.UUID });

        var filePath = Path.Combine(tempDir, "playlist.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, filePath);
        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(filePath);

        var reference = Assert.That.IsInstanceOfType<SongItemReference>(deserialized.Items.Single());
        Assert.AreEqual(song.UUID, reference.UUID);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}
```

Add `using System.Linq;` if not already present in the test file.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SerializePlaylist_ThenDeserialize_SongItem_RoundTripsAsReference"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs
git commit -m "$(cat <<'EOF'
feat: serialize playlist songs as references, not inline content

SerializeItem now writes a SongItemReference (just the song's UUID)
for a SongItemInstance instead of the full lyrics/arrangement/theme
block. DeserializePlaylist needs no change — XmlSerializer already
distinguishes SongItemReference from a legacy inline SongItem block
by its distinct XML root element.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: Song Editor — draft bootstrap and save flow

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Editors/SongEditorWindow.axaml.cs`
- Test: manual (Song Editor is a `Window`/XAML code-behind with `DataContext` wiring — not practically unit-testable without an Avalonia headless test harness this project doesn't already have for this file; cover via Task 10's manual verification pass instead). If `HandsLiftedApp.Tests` already has an Avalonia headless test setup used elsewhere for windows, check for it first and use it; otherwise this task is manual-only by design, not an oversight.

**Interfaces:**
- Consumes: `SongItemInstance.NewDraft(PlaylistInstance?)`, `.AttachToLibrary(string, string)` (Task 6).
- Produces: "New Song" now starts as a draft (`SongItemInstance.NewDraft`) rather than a normal locally-populated instance; both `DoSaveToLibrary` and `AddToPlaylist_OnClick` call `AttachToLibrary` so the draft becomes a real, resolvable reference before it's ever inserted into a playlist.

- [ ] **Step 1: Locate the "New Song" construction site**

`DoSaveToLibrary` itself (`SongEditorWindow.axaml.cs:210-262`, quoted in full below) is already read and covered by Step 2. Separately, find where a brand-new (not-yet-saved) `SongItemInstance` gets constructed for the "New Song" editor flow — search `HandsLiftedApp.Core/ViewModels/Editor/SongEditorViewModel.cs` and `HandsLiftedApp.Core/ViewModels/AddItem/Pages/ResultsViewModel.cs` (around `OnCreateNewSongCommand`) for a bare `new SongItemInstance(...)` construction (as opposed to one built via `ItemInstanceFactory` when opening an *existing* song for editing). Change that specific call site from `new SongItemInstance(playlist)` to `SongItemInstance.NewDraft(playlist)`. Leave every other `SongItemInstance` construction site (editing an existing song) untouched — those already resolve correctly via `ItemInstanceFactory`/`UUID` from Task 7.

- [ ] **Step 2: Update `DoSaveToLibrary` to attach the draft**

```csharp
// HandsLiftedApp.Core/Views/Editors/SongEditorWindow.axaml.cs:210-262 — before:
private void DoSaveToLibrary(SongEditorViewModel vm)
{
    var dir = vm.SongLibrary?.Config.Directory;
    if (dir == null) return;

    var title = vm.Song.Title;
    if (string.IsNullOrWhiteSpace(title)) title = "Untitled";

    var invalidChars = Path.GetInvalidFileNameChars();
    var safeName = string.Concat(title.Select(c => invalidChars.Contains(c) ? '_' : c));
    var path = Path.Combine(dir, safeName + ".xml");

    using var memoryStream = new MemoryStream();
    using var writer = new StreamWriter(memoryStream, leaveOpen: true);
    var settings = new XmlWriterSettings
    {
        NewLineChars = "\n",
        NewLineHandling = NewLineHandling.Replace,
        Indent = true,
    };
    using (var xmlWriter = XmlWriter.Create(writer, settings))
    {
        var serializer = new XmlSerializer(typeof(SongItem));
        SongItemInstance existing = vm.Song;
        var x = new SongItem
        {
            UUID = existing.UUID,
            Title = existing.Title,
            Arrangement = existing.Arrangement,
            Arrangements = existing.Arrangements,
            SelectedArrangementId = existing.SelectedArrangementId,
            Stanzas = existing.Stanzas,
            Copyright = existing.Copyright,
            Design = existing.Design,
            StartOnTitleSlide = existing.StartOnTitleSlide,
            EndOnBlankSlide = existing.EndOnBlankSlide
        };
        serializer.Serialize(xmlWriter, x);
    }

    File.WriteAllBytes(path, memoryStream.ToArray());
    vm.SongLibrary!.TriggerRefresh();

    if (vm.ItemInsertIndex.HasValue && !vm.ItemInserted)
    {
        Globals.Instance.MainViewModel.Playlist.Items.Insert(vm.ItemInsertIndex.Value, vm.Song);
        vm.ItemInserted = true;
        MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = vm.ItemInsertIndex.Value });
    }

    _closeConfirmed = true;
    Close();
}

// after: attach the draft (if it still is one) to the library right after its file is
// written, before TriggerRefresh's async rescan gets a chance to race it (SongLibraryIndex
// .Register's existing-UUID branch, added alongside this task, makes that race harmless
// either way, but attaching explicitly here means the UUID resolves immediately rather than
// waiting on the async rescan to complete).
private void DoSaveToLibrary(SongEditorViewModel vm)
{
    var dir = vm.SongLibrary?.Config.Directory;
    if (dir == null) return;

    var title = vm.Song.Title;
    if (string.IsNullOrWhiteSpace(title)) title = "Untitled";

    var invalidChars = Path.GetInvalidFileNameChars();
    var safeName = string.Concat(title.Select(c => invalidChars.Contains(c) ? '_' : c));
    var path = Path.Combine(dir, safeName + ".xml");

    using var memoryStream = new MemoryStream();
    using var writer = new StreamWriter(memoryStream, leaveOpen: true);
    var settings = new XmlWriterSettings
    {
        NewLineChars = "\n",
        NewLineHandling = NewLineHandling.Replace,
        Indent = true,
    };
    using (var xmlWriter = XmlWriter.Create(writer, settings))
    {
        var serializer = new XmlSerializer(typeof(SongItem));
        SongItemInstance existing = vm.Song;
        var x = new SongItem
        {
            UUID = existing.UUID,
            Title = existing.Title,
            Arrangement = existing.Arrangement,
            Arrangements = existing.Arrangements,
            SelectedArrangementId = existing.SelectedArrangementId,
            Stanzas = existing.Stanzas,
            Copyright = existing.Copyright,
            Design = existing.Design,
            StartOnTitleSlide = existing.StartOnTitleSlide,
            EndOnBlankSlide = existing.EndOnBlankSlide
        };
        serializer.Serialize(xmlWriter, x);
    }

    File.WriteAllBytes(path, memoryStream.ToArray());
    vm.Song.AttachToLibrary(path, dir);
    vm.SongLibrary!.TriggerRefresh();

    if (vm.ItemInsertIndex.HasValue && !vm.ItemInserted)
    {
        Globals.Instance.MainViewModel.Playlist.Items.Insert(vm.ItemInsertIndex.Value, vm.Song);
        vm.ItemInserted = true;
        MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = vm.ItemInsertIndex.Value });
    }

    _closeConfirmed = true;
    Close();
}
```

The only functional change is the single added line, `vm.Song.AttachToLibrary(path, dir);`, placed after the file write succeeds and before `TriggerRefresh()`. Everything else is unchanged from the existing method (quoted in full above only so the diff is unambiguous against the real file, not because the rest of the method needs editing).

- [ ] **Step 3: Update `AddToPlaylist_OnClick` to ensure the draft is attached first**

```csharp
// HandsLiftedApp.Core/Views/Editors/SongEditorWindow.axaml.cs:48-57 — before:
public void AddToPlaylist_OnClick(object? sender, RoutedEventArgs args)
{
    if (DataContext is SongEditorViewModel { ItemInsertIndex: not null, ItemInserted: false } songEditorViewModel)
    {
        Globals.Instance.MainViewModel.Playlist.Items.Insert(songEditorViewModel.ItemInsertIndex.Value, songEditorViewModel.Song);
        songEditorViewModel.ItemInserted = true;
        MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = songEditorViewModel.ItemInsertIndex.Value });
        Close();
    }
}

// after: ensure the song is library-backed (saving it if it's still a draft) before
// inserting it, since a playlist item is now purely a UUID reference — inserting an
// unattached draft would show as "missing" immediately. DoSaveToLibrary (Step 2) already
// performs its own insert-into-playlist + navigate + close sequence once the file write
// succeeds, so the draft case delegates to it entirely rather than duplicating that tail
// (which would otherwise double-insert and double-close).
public void AddToPlaylist_OnClick(object? sender, RoutedEventArgs args)
{
    if (DataContext is SongEditorViewModel { ItemInsertIndex: not null, ItemInserted: false } songEditorViewModel)
    {
        if (songEditorViewModel.Song is SongItemInstance instance
            && Globals.Instance.SongLibraryIndex.Resolve(instance.UUID) == null)
        {
            DoSaveToLibrary(songEditorViewModel);
            return;
        }

        Globals.Instance.MainViewModel.Playlist.Items.Insert(songEditorViewModel.ItemInsertIndex.Value, songEditorViewModel.Song);
        songEditorViewModel.ItemInserted = true;
        MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = songEditorViewModel.ItemInsertIndex.Value });
        Close();
    }
}
```

`DoSaveToLibrary` is `private void DoSaveToLibrary(SongEditorViewModel vm)` (confirmed by the full read in Step 2) — matches the call above.

- [ ] **Step 4: Build**

Run: `dotnet build HandsLiftedApp.Core`
Expected: builds clean.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Views/Editors/SongEditorWindow.axaml.cs
git commit -m "$(cat <<'EOF'
feat: Song Editor bootstraps drafts into the library before referencing

New Song starts as a SongItemInstance draft (no library file yet).
Both Save-to-Library and Add-to-Playlist now attach the draft to the
library (writing its file, registering it into SongLibraryIndex)
before it can be referenced from a playlist — a playlist item is a
pure UUID reference now, so inserting an unattached draft would
otherwise show as missing immediately.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 10: Missing-song placeholder slide + manual verification

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs` (`UpdateStanzaSlides`, `SongItemInstance.cs:186-336`)
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/SongItemInstanceTests.cs` (extend, Task 6's file) + manual run

**Interfaces:**
- Consumes: `SongItemInstance.IsMissing` (Task 6).
- Produces: a playlist item whose reference can't resolve shows a single clear placeholder slide instead of an empty/crashing slide list.

- [ ] **Step 1: Write the failing test**

```csharp
// HandsLiftedApp.Tests/Models/RuntimeData/Items/SongItemInstanceTests.cs — add
[TestMethod]
public void MissingSong_GeneratesSinglePlaceholderSlide()
{
    var instance = new SongItemInstance(null) { UUID = Guid.NewGuid() };

    instance.GenerateSlides();

    Assert.AreEqual(1, instance.Slides.Count);
    Assert.IsInstanceOfType(instance.Slides[0], typeof(HandsLiftedApp.Core.Models.RuntimeData.Slides.SongSlideInstance));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~MissingSong_GeneratesSinglePlaceholderSlide"`
Expected: FAIL — today `UpdateStanzaSlides` with an empty `Arrangement`/`Stanzas` (which is what a missing song's forwarding getters return, per Task 6) produces zero stanza slides (only the optional title/blank slides), not a placeholder.

- [ ] **Step 3: Short-circuit `UpdateStanzaSlides` for the missing case**

```csharp
// HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs — inside
// UpdateStanzaSlides (SongItemInstance.cs:186), immediately after the
// `lock (stantaSlidesLock) { try {` opening, before the existing `var newSlides = ...` line:
if (IsMissing)
{
    var missingSlides = new TrulyObservableCollection<Slide>
    {
        new SongSlideInstance(this, new SongStanza(), "MISSING", text: "(Missing Song)", label: null)
    };
    StanzaSlides = missingSlides;
    this.RaisePropertyChanged("Slides");
    Globals.Instance.SlideRenderQueue.EnqueueBatch(missingSlides.OfType<IRenderable>().ToList());
    return;
}
```

Place this as the first statement inside the existing `try` block (i.e. right after `try` (`{`), before `var newSlides = new TrulyObservableCollection<Slide>();`), so the normal path is completely untouched when a song does resolve.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~MissingSong_GeneratesSinglePlaceholderSlide"`
Expected: PASS.

Run: `dotnet test HandsLiftedApp.Tests`
Expected: full suite PASS.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/SongItemInstanceTests.cs
git commit -m "$(cat <<'EOF'
feat: render a placeholder slide for an unresolvable song reference

A SongItemInstance whose UUID resolves to nothing (deleted from the
library, or a playlist opened without that library present) now shows
a single "(Missing Song)" slide instead of silently rendering zero
stanza slides.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 6: Manual end-to-end verification in the running app**

Per CLAUDE.md's SkiaSharp/render-pipeline guidance, structural tests don't prove the render pipeline actually picks this up — run the real app and click through:

1. Launch the app (`dotnet run` on the main executable project, or via the existing `run` skill if configured for this repo).
2. Add an existing library song to a playlist. Confirm it renders identically to before this change (title slide, stanza slides, theme).
3. Edit the song's lyrics from within the playlist (song editor opened on the playlist item). Confirm the edit appears immediately.
4. Add the *same* song to a second playlist (or open a second `PlaylistInstance` if the app supports multiple open playlists — otherwise, add it twice to the same playlist at two different positions). Confirm the edit from step 3 is visible on the second reference.
5. Save the playlist, close and reopen the app (or reload the playlist file), confirm the song still resolves correctly (this exercises Task 1's UUID persistence end-to-end).
6. In the library folder on disk, rename or delete the song's XML file, then reload the playlist. Confirm the item shows the "(Missing Song)" placeholder rather than crashing.
7. Create a brand-new song via the Song Editor's "New Song" flow, click "Add to Playlist" without ever clicking "Save to Library" first. Confirm: (a) it's added and renders correctly, (b) a library file now exists for it (the draft was attached), (c) editing it afterward from the playlist still write-throughs correctly.
8. Open an old-format playlist file saved before this change (if one exists in the repo/test fixtures, or construct one by hand from the pre-change inline-song XML shape). Confirm it loads without error and the song(s) get migrated (test per Step 7's checks, or per Task 7's `ToItemInstance_LegacyInlineSongItem_ImportsIntoLibraryIndex_WhenNotAlreadyKnown` reasoning, manually confirmed once in the running app).

Report the outcome of each numbered check back before considering this plan complete. Do not claim the feature works based on the unit test suite alone.

---

## Notes For The Executor

- Tasks 1–8 are strictly ordered — each depends on the previous. Task 9 depends on Task 6. Task 10 depends on Task 6 and should run last since it's the integration/manual-verification pass.
- Every `Assert.That.IsInstanceOfType<T>(...)` in this plan's test code is written against the newer MSTest fluent-assertion style; if this project's pinned MSTest version doesn't have it, fall back to the classic `Assert.IsInstanceOfType(obj, typeof(T))` plus a cast, matching whatever idiom `HandsLiftedApp.Tests`'s existing files already use — check one existing test file before writing the first new assertion, not after.
- `Globals.Instance.SongLibraryIndex` is a shared, process-wide singleton — tests that register songs into it (most of Tasks 4–10's tests) are not isolated from each other by default. Each test in this plan uses a freshly-generated `Guid` per song, so cross-test collisions in the shared dictionary are not expected, but if flakiness appears, that shared global state is the first place to look (see [[project_scripture_test_flake]]-style precedent in this codebase's memory for a prior Dispatcher-thread flake root-caused the same way).
