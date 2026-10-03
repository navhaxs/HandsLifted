# Global Slide Themes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Slide themes become app-wide: one XML file per theme in a configurable folder, with playlists' embedded themes migrated into it on open.

**Architecture:** A new `SlideThemeLibrary` service (modeled on `SongLibraryIndex`) owns an `ObservableCollection<BaseSlideTheme>` backed by XML files and is exposed on `Globals`. In production `PlaylistInstance.Designs` is assigned that same collection instance, so every existing `Designs` binding and lookup keeps working with no XAML churn. Default theme Ids move to `AppPreferences` with optional per-playlist overrides. `SlideThemeMigration` imports legacy embedded `Designs` on playlist open.

**Tech Stack:** C# / .NET, Avalonia 11, ReactiveUI 20.1.1, MSTest, `DebounceThrottle`, Newtonsoft.Json (appstate), `XmlSerializer`.

**Spec:** `docs/superpowers/specs/2026-10-02-global-slide-themes-design.md`

## Global Constraints

- Repo rule: never run `find`/`grep` via shell; use the Glob and Grep tools, scoped to the repo.
- `HandsLiftedApp.Models/` is a stale duplicate project; never edit it. Live model project is `HandsLiftedApp.Data/`.
- Theme identity is `BaseSlideTheme.Id` (Guid). Ids are never rewritten by migration.
- Theme asset paths (`BackgroundGraphicFilePath`, `DefaultMotionBackgroundVideoPath`): absolute in memory; on disk relative to the Media Library when under it; `avares://` paths are never touched.
- `AppPreferences.DefaultTheme` (built-in default) stays a non-file, last-resort fallback and is never written to the themes folder.
- Serilog `Log.*` messages: use "-" not an em dash.
- Test setup convention: `Globals.Instance.AppPreferences = new AppPreferencesViewModel();` in `[TestInitialize]` (see `HandsLiftedApp.Tests/Models/PlaylistInstanceTests.cs:18-25`). MSTest, namespace `HandsLiftedApp.Tests...`.
- Test command: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~<ClassName>"`.
- The working tree already has uncommitted user edits in `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml` and `HandsLiftedApp.Core/Controls/SongArrangementControl.axaml`. Commit only files you changed, by explicit path. For `SlideThemeDesigner.axaml` use `git add -p` and stage only your hunks, or ask the user first. Never `git add -A`. Only commit if the user wants commits.
- No new theme off the UI thread: `BaseSlideTheme` builds `ObservableAsPropertyHelper`s on `RxSchedulers.MainThreadScheduler`.

## Review Focus

- Themes folder is invalid or unwritable (path is an existing file, or cannot be created): app must start with just the built-in default theme, no crash. Pinned in Task 3.
- Playlist with embedded themes opened while no Media Library is configured: themes still import (absolute asset paths kept), no exception. Pinned in Task 6.
- Embedded theme references an asset file that no longer exists: theme still imports. Pinned in Task 6.
- Same playlist opened twice, or a library theme edited after first import: second open imports 0 and never overwrites the library's version. Pinned in Task 6.
- Theme named with invalid filename characters or an empty name: still saved to a valid file. Pinned in Task 3.

## File Structure

- Create `HandsLiftedApp.Core/Models/Library/SlideThemeLibrary.cs`: file-backed theme collection.
- Create `HandsLiftedApp.Core/Services/SlideThemeMigration.cs`: legacy embedded-theme import.
- Modify `HandsLiftedApp.Core/Utils/SlideThemeXmlSerializer.cs`: add `TrySerializeToBytes`.
- Modify `HandsLiftedApp.Core/Constants.cs`: `SLIDE_THEMES_DIR`.
- Modify `HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs`: `SlideThemesPath`, three default Id properties.
- Modify `HandsLiftedApp.Core/Globals.cs`: library instance, reload, save prefs, startup/shutdown wiring.
- Modify `HandsLiftedApp.Core/App.axaml.cs`: exit uses `SaveAppPreferences`.
- Modify `HandsLiftedApp.Core/Models/PlaylistInstance.cs`: resolution order, change stream, stop dirtying on theme edits.
- Modify `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs` and `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`: stop writing `Designs`; migrate on load.
- Modify `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml(.cs)`: app-level defaults, asset copy.
- Modify `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml(.cs)`: themes folder row.
- Tests: `HandsLiftedApp.Tests/Services/SlideThemeLibraryTests.cs` (create; use `Models/Library/` if that folder exists), `HandsLiftedApp.Tests/Services/SlideThemeMigrationTests.cs`, `HandsLiftedApp.Tests/ViewModels/AppPreferencesViewModelTests.cs`, `HandsLiftedApp.Tests/Utils/SlideThemeXmlSerializerTests.cs`, modify `HandsLiftedApp.Tests/Models/PlaylistInstanceTests.cs` and `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs`.

---

### Task 1: App preferences and constants

**Files:**
- Modify: `HandsLiftedApp.Core/Constants.cs`
- Modify: `HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs` (after `MediaLibraryPath`, ~line 173)
- Modify: `HandsLiftedApp.Core/Globals.cs`, `HandsLiftedApp.Core/App.axaml.cs:61-65`
- Test: `HandsLiftedApp.Tests/ViewModels/AppPreferencesViewModelTests.cs`

**Interfaces:**
- Produces: `Constants.SLIDE_THEMES_DIR` (string); `AppPreferencesViewModel.SlideThemesPath` (string, default `Constants.SLIDE_THEMES_DIR`); `AppPreferencesViewModel.DefaultSongThemeId`, `DefaultSongMotionThemeId`, `DefaultScriptureThemeId` (`Guid?`, all `[DataMember]`); `Globals.SaveAppPreferences()` (void); `static string Globals.ResolveSlideThemesFolder(string? configured)`.

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace HandsLiftedApp.Tests.ViewModels;

[TestClass]
public class AppPreferencesViewModelTests
{
    [TestMethod]
    public void ThemeSettings_RoundTripThroughJson()
    {
        var song = Guid.NewGuid();
        var motion = Guid.NewGuid();
        var scripture = Guid.NewGuid();
        var prefs = new AppPreferencesViewModel
        {
            SlideThemesPath = @"C:\Some\Themes",
            DefaultSongThemeId = song,
            DefaultSongMotionThemeId = motion,
            DefaultScriptureThemeId = scripture
        };

        var json = JsonConvert.SerializeObject(prefs);
        var loaded = JsonConvert.DeserializeObject<AppPreferencesViewModel>(json)!;

        Assert.AreEqual(@"C:\Some\Themes", loaded.SlideThemesPath);
        Assert.AreEqual(song, loaded.DefaultSongThemeId);
        Assert.AreEqual(motion, loaded.DefaultSongMotionThemeId);
        Assert.AreEqual(scripture, loaded.DefaultScriptureThemeId);
    }

    [TestMethod]
    public void OldAppState_WithoutThemeSettings_GetsDefaults()
    {
        var loaded = JsonConvert.DeserializeObject<AppPreferencesViewModel>("{}")!;

        Assert.IsFalse(string.IsNullOrWhiteSpace(loaded.SlideThemesPath));
        Assert.IsNull(loaded.DefaultSongThemeId);
    }

    [TestMethod]
    public void ResolveSlideThemesFolder_BlankFallsBackToDefault()
    {
        var def = Globals.ResolveSlideThemesFolder(null);

        Assert.IsFalse(string.IsNullOrWhiteSpace(def));
        Assert.AreEqual(def, Globals.ResolveSlideThemesFolder("  "));
        Assert.AreEqual(@"C:\X", Globals.ResolveSlideThemesFolder(@"C:\X"));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~AppPreferencesViewModelTests"`
Expected: build FAIL (`SlideThemesPath`, `ResolveSlideThemesFolder` not defined).

- [ ] **Step 3: Implement**

`Constants.cs`, after `LOGGING_CONFIG_FILEPATH`:

```csharp
        public static readonly string SLIDE_THEMES_DIR = Path.Combine(APP_DATA_DIR, "SlideThemes");
```

`AppPreferencesViewModel.cs`, after the `MediaLibraryPath` property:

```csharp
        private string _slideThemesPath = Constants.SLIDE_THEMES_DIR;
        [DataMember]
        public string SlideThemesPath
        {
            get => _slideThemesPath;
            set => this.RaiseAndSetIfChanged(ref _slideThemesPath, value);
        }

        private Guid? _defaultSongThemeId;
        [DataMember]
        public Guid? DefaultSongThemeId
        {
            get => _defaultSongThemeId;
            set => this.RaiseAndSetIfChanged(ref _defaultSongThemeId, value);
        }

        private Guid? _defaultSongMotionThemeId;
        [DataMember]
        public Guid? DefaultSongMotionThemeId
        {
            get => _defaultSongMotionThemeId;
            set => this.RaiseAndSetIfChanged(ref _defaultSongMotionThemeId, value);
        }

        private Guid? _defaultScriptureThemeId;
        [DataMember]
        public Guid? DefaultScriptureThemeId
        {
            get => _defaultScriptureThemeId;
            set => this.RaiseAndSetIfChanged(ref _defaultScriptureThemeId, value);
        }
```

`Globals.cs`, add inside the class (after `OnShutdown`):

```csharp
        public static string ResolveSlideThemesFolder(string? configured) =>
            string.IsNullOrWhiteSpace(configured) ? Constants.SLIDE_THEMES_DIR : configured;

        /// <summary>
        /// Persists app preferences immediately. Preferences are otherwise only written on app
        /// exit, which would lose a changed setting on a crash.
        /// </summary>
        public void SaveAppPreferences()
        {
            try
            {
                Directory.CreateDirectory(Constants.APP_DATA_DIR);
                File.WriteAllText(Constants.APP_STATE_FILEPATH, JsonConvert.SerializeObject(AppPreferences));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save app preferences to {Path}", Constants.APP_STATE_FILEPATH);
            }
        }
```

`App.axaml.cs`, replace the two lines inside `desktop.Exit` (lines 63-64) with:

```csharp
                Globals.Instance.SaveAppPreferences();
```

- [ ] **Step 4: Run to verify it passes**

Run the Step 2 command. Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Constants.cs HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs HandsLiftedApp.Core/Globals.cs HandsLiftedApp.Core/App.axaml.cs HandsLiftedApp.Tests/ViewModels/AppPreferencesViewModelTests.cs
git commit -m "feat: add slide themes path and app-level default theme ids to preferences"
```

---

### Task 2: Synchronous theme serialization to bytes

**Files:**
- Modify: `HandsLiftedApp.Core/Utils/SlideThemeXmlSerializer.cs`
- Test: `HandsLiftedApp.Tests/Utils/SlideThemeXmlSerializerTests.cs`

**Interfaces:**
- Produces: `static bool SlideThemeXmlSerializer.TrySerializeToBytes(BaseSlideTheme theme, out byte[] bytes)` (UTF-8, no BOM, indented, synchronous). Consumed by Task 3.

- [ ] **Step 1: Write the failing test**

```csharp
using System.IO;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.SlideTheme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Utils;

[TestClass]
public class SlideThemeXmlSerializerTests
{
    [TestMethod]
    public void TrySerializeToBytes_RoundTripsThroughTryDeserialize()
    {
        var theme = new BaseSlideTheme { Name = "Bytes Theme" };

        Assert.IsTrue(SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var bytes));
        using var ms = new MemoryStream(bytes);
        Assert.IsTrue(SlideThemeXmlSerializer.TryDeserialize(ms, out var loaded));

        Assert.AreEqual(theme.Id, loaded!.Id);
        Assert.AreEqual("Bytes Theme", loaded.Name);
    }

    [TestMethod]
    public void TrySerializeToBytes_IsDeterministicForUnchangedTheme()
    {
        var theme = new BaseSlideTheme { Name = "Same" };

        SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var a);
        SlideThemeXmlSerializer.TrySerializeToBytes(theme, out var b);

        CollectionAssert.AreEqual(a, b);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SlideThemeXmlSerializerTests"`
Expected: build FAIL (`TrySerializeToBytes` not defined).

- [ ] **Step 3: Implement**

In `SlideThemeXmlSerializer.cs` add `using System.Text;` and this method inside the class:

```csharp
        /// <summary>
        /// Synchronously serialize a theme to UTF-8 (no BOM) XML bytes. Used by the theme library,
        /// which compares bytes to skip no-op saves.
        /// </summary>
        public static bool TrySerializeToBytes(BaseSlideTheme theme, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            try
            {
                using var ms = new MemoryStream();
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    Encoding = new UTF8Encoding(false)
                };
                using (var writer = XmlWriter.Create(ms, settings))
                {
                    ThemeSerializer.Serialize(writer, theme);
                }
                bytes = ms.ToArray();
                return true;
            }
            catch (InvalidOperationException ex)
            {
                Log.Error(ex, "Failed to serialize BaseSlideTheme to bytes");
                return false;
            }
            catch (XmlException ex)
            {
                Log.Error(ex, "XML error during BaseSlideTheme serialization to bytes");
                return false;
            }
        }
```

- [ ] **Step 4: Run to verify it passes**

Run the Step 2 command. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Utils/SlideThemeXmlSerializer.cs HandsLiftedApp.Tests/Utils/SlideThemeXmlSerializerTests.cs
git commit -m "feat: add synchronous theme XML serialization to bytes"
```

---

### Task 3: SlideThemeLibrary

**Files:**
- Create: `HandsLiftedApp.Core/Models/Library/SlideThemeLibrary.cs`
- Test: `HandsLiftedApp.Tests/Services/SlideThemeLibraryTests.cs`

**Interfaces:**
- Consumes: `SlideThemeXmlSerializer.TryDeserialize(Stream, out BaseSlideTheme?)`, `TrySerializeToBytes` (Task 2); `FilenameUtils.ReplaceInvalidChars(string)` (`HandsLiftedApp.Utils`); `RelativeFilePathResolver.ToAbsolutePath/ToRelativePath/IsUnderDirectory`; `DebounceDispatcher(int)` with `.Debounce(Action)` (`DebounceThrottle`).
- Produces (namespace `HandsLiftedApp.Core.Models.Library`):
  - `public sealed class SlideThemeLibrary : IDisposable`
  - ctor `SlideThemeLibrary(Func<string?>? mediaLibraryPath = null, Action<Action>? postToSaveThread = null)`. Defaults: `() => Globals.Instance.AppPreferences?.MediaLibraryPath` and `a => Dispatcher.UIThread.Post(a)`. Tests pass `a => a()`.
  - `ObservableCollection<BaseSlideTheme> Themes { get; }`
  - `string? Folder { get; }` (null if the folder could not be created, in which case saving is disabled)
  - `void Initialize(string folder, BaseSlideTheme? builtInDefault)`
  - `bool Contains(Guid id)`
  - `void SaveNow(Guid id)`
  - `void FlushPendingSaves()`
  - `IObservable<(Guid Id, Exception Error)> SaveFailed { get; }`
  - `void Dispose()`

**Behavior:** `Themes[0]` is `builtInDefault` when given (never written or deleted). Adding to `Themes` (after Initialize) makes the library persist it; editing a theme schedules a 500ms debounced save; removing from `Themes` deletes its file. Saves skip when the XML bytes and name are unchanged.

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.IO;
using System.Linq;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class SlideThemeLibraryTests
{
    private string _root = null!;
    private string _dir = null!;
    private string _media = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "SlideThemeLibraryTests_" + Guid.NewGuid().ToString("N"));
        _dir = Path.Combine(_root, "themes");
        _media = Path.Combine(_root, "media");
        Directory.CreateDirectory(_media);
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private SlideThemeLibrary NewLibrary() => new(() => _media, a => a());

    private SlideThemeLibrary NewInitialized(BaseSlideTheme? builtIn = null)
    {
        var lib = NewLibrary();
        lib.Initialize(_dir, builtIn);
        return lib;
    }

    private string[] ThemeFiles() => Directory.Exists(_dir)
        ? Directory.GetFiles(_dir, "*.xml").Select(Path.GetFileName).OrderBy(n => n).ToArray()!
        : Array.Empty<string>();

    [TestMethod]
    public void Initialize_CreatesFolder_AndPutsBuiltInFirst()
    {
        var builtIn = new BaseSlideTheme { Name = "Built-in" };
        var lib = NewInitialized(builtIn);

        Assert.IsTrue(Directory.Exists(_dir));
        Assert.AreSame(builtIn, lib.Themes[0]);
        Assert.AreEqual(1, lib.Themes.Count);
    }

    [TestMethod]
    public void AddedTheme_IsWrittenAndReloadedWithSameId()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Sunday" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);

        CollectionAssert.AreEqual(new[] { "Sunday.xml" }, ThemeFiles());

        var lib2 = NewInitialized();
        var loaded = lib2.Themes.Single();
        Assert.AreEqual(theme.Id, loaded.Id);
        Assert.AreEqual("Sunday", loaded.Name);
    }

    [TestMethod]
    public void BuiltIn_IsNeverWritten_AndRemovingItDoesNotTouchDisk()
    {
        var builtIn = new BaseSlideTheme { Name = "Built-in" };
        var lib = NewInitialized(builtIn);

        lib.SaveNow(builtIn.Id);
        lib.Themes.Remove(builtIn);

        Assert.AreEqual(0, ThemeFiles().Length);
    }

    [TestMethod]
    public void SameNameTwice_SecondGetsNumericSuffix()
    {
        var lib = NewInitialized();
        var a = new BaseSlideTheme { Name = "Dup" };
        var b = new BaseSlideTheme { Name = "Dup" };
        lib.Themes.Add(a);
        lib.Themes.Add(b);
        lib.SaveNow(a.Id);
        lib.SaveNow(b.Id);

        CollectionAssert.AreEqual(new[] { "Dup (2).xml", "Dup.xml" }, ThemeFiles());
    }

    [TestMethod]
    public void Rename_MovesFile()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Old" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);

        theme.Name = "New";
        lib.SaveNow(theme.Id);

        CollectionAssert.AreEqual(new[] { "New.xml" }, ThemeFiles());
    }

    [TestMethod]
    public void Remove_DeletesFile()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Gone" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);

        lib.Themes.Remove(theme);

        Assert.AreEqual(0, ThemeFiles().Length);
    }

    [TestMethod]
    public void SaveNow_SkipsWriteWhenContentUnchanged()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Stable" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);
        var path = Path.Combine(_dir, "Stable.xml");

        File.WriteAllText(path, "SENTINEL");
        lib.SaveNow(theme.Id);

        Assert.AreEqual("SENTINEL", File.ReadAllText(path));
    }

    [TestMethod]
    public void FlushPendingSaves_PersistsEdits()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Edit" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);

        theme.FontSize = theme.FontSize + 7;
        lib.FlushPendingSaves();

        var loaded = NewInitialized().Themes.Single();
        Assert.AreEqual(theme.FontSize, loaded.FontSize);
    }

    [TestMethod]
    public void CorruptFile_IsSkipped_ValidFileStillLoads()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Good" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);
        File.WriteAllText(Path.Combine(_dir, "Bad.xml"), "this is not xml");

        var lib2 = NewInitialized();

        Assert.AreEqual(theme.Id, lib2.Themes.Single().Id);
    }

    [TestMethod]
    public void DuplicateIdAcrossFiles_FirstByFilenameWins()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Original" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);
        File.Copy(Path.Combine(_dir, "Original.xml"), Path.Combine(_dir, "Zcopy.xml"));

        var lib2 = NewInitialized();

        Assert.AreEqual(1, lib2.Themes.Count);
    }

    [TestMethod]
    public void AssetUnderMediaLibrary_StoredRelative_LoadedAbsolute()
    {
        var bg = Path.Combine(_media, "Images", "bg.png");
        Directory.CreateDirectory(Path.GetDirectoryName(bg)!);
        File.WriteAllBytes(bg, new byte[] { 1 });
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Assets", BackgroundGraphicFilePath = bg };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);

        var xml = File.ReadAllText(Path.Combine(_dir, "Assets.xml"));
        Assert.IsFalse(xml.Contains(_media, StringComparison.OrdinalIgnoreCase), "stored path must be relative");
        Assert.AreEqual(bg, NewInitialized().Themes.Single().BackgroundGraphicFilePath);
        Assert.AreEqual(bg, theme.BackgroundGraphicFilePath, "in-memory theme must keep its absolute path");
    }

    [TestMethod]
    public void AssetOutsideMediaLibrary_AndAvares_AreLeftAlone()
    {
        var outside = Path.Combine(_root, "elsewhere", "bg.png");
        var lib = NewInitialized();
        var a = new BaseSlideTheme { Name = "Outside", BackgroundGraphicFilePath = outside };
        var b = new BaseSlideTheme
        {
            Name = "Avares",
            BackgroundGraphicFilePath = "avares://HandsLiftedApp.Core/Assets/DefaultTheme/default-bg.png"
        };
        lib.Themes.Add(a);
        lib.Themes.Add(b);
        lib.SaveNow(a.Id);
        lib.SaveNow(b.Id);

        var loaded = NewInitialized().Themes.ToDictionary(t => t.Name);
        Assert.AreEqual(outside, loaded["Outside"].BackgroundGraphicFilePath);
        Assert.AreEqual(b.BackgroundGraphicFilePath, loaded["Avares"].BackgroundGraphicFilePath);
    }

    [TestMethod]
    public void UnusableFolder_DoesNotThrow_AndLeavesBuiltInOnly()
    {
        Directory.CreateDirectory(_root);
        var asFile = Path.Combine(_root, "iam-a-file");
        File.WriteAllText(asFile, "x");
        var builtIn = new BaseSlideTheme { Name = "Built-in" };
        var lib = NewLibrary();

        lib.Initialize(asFile, builtIn);

        Assert.IsNull(lib.Folder);
        Assert.AreSame(builtIn, lib.Themes.Single());
        var extra = new BaseSlideTheme { Name = "Nowhere" };
        lib.Themes.Add(extra);
        lib.SaveNow(extra.Id); // must not throw
    }

    [TestMethod]
    public void InvalidFilenameCharsAndEmptyName_StillProduceValidFiles()
    {
        var lib = NewInitialized();
        var weird = new BaseSlideTheme { Name = "a/b:c?" };
        var empty = new BaseSlideTheme { Name = "" };
        lib.Themes.Add(weird);
        lib.Themes.Add(empty);

        lib.SaveNow(weird.Id);
        lib.SaveNow(empty.Id);

        Assert.AreEqual(2, ThemeFiles().Length);
        Assert.AreEqual(2, NewInitialized().Themes.Count);
    }

    [TestMethod]
    public void Contains_ReflectsThemes()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "C" };
        lib.Themes.Add(theme);

        Assert.IsTrue(lib.Contains(theme.Id));
        Assert.IsFalse(lib.Contains(Guid.NewGuid()));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SlideThemeLibraryTests"`
Expected: build FAIL (`SlideThemeLibrary` not defined).

- [ ] **Step 3: Implement**

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using Avalonia.Threading;
using DebounceThrottle;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Utils;
using Serilog;
using System.Collections.ObjectModel;

namespace HandsLiftedApp.Core.Models.Library
{
    /// <summary>
    /// App-wide set of slide themes, one XML file per theme in a single folder. Themes is the
    /// single source of truth: adding to it persists the theme, editing a theme debounces a
    /// save, removing from it deletes the file. Slot 0 holds the built-in default theme (not
    /// file-backed) when one is supplied to Initialize.
    /// All members are UI-thread only.
    /// </summary>
    public sealed class SlideThemeLibrary : IDisposable
    {
        private sealed class Entry
        {
            public string FilePath = "";
            public string LastName = "";
            public byte[] LastBytes = Array.Empty<byte>();
            public IDisposable? ChangedSubscription;
            public readonly DebounceDispatcher Debouncer = new(500);
        }

        private readonly Dictionary<Guid, Entry> _entries = new();
        private readonly Func<string?> _mediaLibraryPath;
        private readonly Action<Action> _postToSaveThread;
        private readonly Subject<(Guid Id, Exception Error)> _saveFailed = new();
        private Guid? _builtInId;
        private bool _syncing;

        public ObservableCollection<BaseSlideTheme> Themes { get; } = new();
        public string? Folder { get; private set; }
        public IObservable<(Guid Id, Exception Error)> SaveFailed => _saveFailed;

        public SlideThemeLibrary(Func<string?>? mediaLibraryPath = null, Action<Action>? postToSaveThread = null)
        {
            _mediaLibraryPath = mediaLibraryPath ?? (() => Globals.Instance.AppPreferences?.MediaLibraryPath);
            _postToSaveThread = postToSaveThread ?? (a => Dispatcher.UIThread.Post(a));
            Themes.CollectionChanged += OnThemesChanged;
        }

        public bool Contains(Guid id) => Themes.Any(t => t.Id == id);

        public void Initialize(string folder, BaseSlideTheme? builtInDefault)
        {
            // Write out any edit still inside its debounce window to the OLD folder first.
            FlushPendingSaves();

            _syncing = true;
            try
            {
                DetachAll();
                Themes.Clear();
                _builtInId = builtInDefault?.Id;
                if (builtInDefault != null) Themes.Add(builtInDefault);

                try
                {
                    Directory.CreateDirectory(folder);
                    Folder = folder;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "SlideThemeLibrary: cannot use themes folder {Folder} - themes will not be saved", folder);
                    Folder = null;
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(folder, "*.xml", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    LoadFile(file);
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private void LoadFile(string file)
        {
            BaseSlideTheme? theme;
            try
            {
                using var stream = File.OpenRead(file);
                if (!SlideThemeXmlSerializer.TryDeserialize(stream, out theme) || theme == null)
                {
                    Log.Warning("SlideThemeLibrary: skipping unreadable theme file {File}", file);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SlideThemeLibrary: skipping unreadable theme file {File}", file);
                return;
            }

            if (Contains(theme.Id))
            {
                Log.Warning("SlideThemeLibrary: ignoring {File} - theme Id {Id} already loaded", file, theme.Id);
                return;
            }

            var media = _mediaLibraryPath();
            theme.BackgroundGraphicFilePath = ToAbsolute(theme.BackgroundGraphicFilePath, media);
            theme.DefaultMotionBackgroundVideoPath = ToAbsolute(theme.DefaultMotionBackgroundVideoPath, media);

            Themes.Add(theme);
            Attach(theme, file);
        }

        private void Attach(BaseSlideTheme theme, string filePath)
        {
            var entry = new Entry { FilePath = filePath };
            if (filePath != "")
            {
                entry.LastName = theme.Name;
                entry.LastBytes = RenderBytes(theme) ?? Array.Empty<byte>();
            }

            var id = theme.Id;
            entry.ChangedSubscription = theme.Changed.Subscribe(_ => ScheduleSave(id));
            _entries[id] = entry;
        }

        private void OnThemesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_syncing) return;

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                Log.Warning("SlideThemeLibrary: Themes was reset outside Initialize - files on disk are unchanged");
                return;
            }

            if (e.OldItems != null)
            {
                foreach (BaseSlideTheme theme in e.OldItems) Detach(theme, deleteFile: true);
            }

            if (e.NewItems != null)
            {
                foreach (BaseSlideTheme theme in e.NewItems)
                {
                    if (theme.Id == _builtInId || Folder == null) continue;
                    if (_entries.ContainsKey(theme.Id))
                    {
                        Log.Warning("SlideThemeLibrary: theme Id {Id} added twice - second copy is not persisted", theme.Id);
                        continue;
                    }
                    Attach(theme, "");
                    ScheduleSave(theme.Id);
                }
            }
        }

        private void Detach(BaseSlideTheme theme, bool deleteFile)
        {
            if (!_entries.TryGetValue(theme.Id, out var entry)) return;
            entry.ChangedSubscription?.Dispose();
            _entries.Remove(theme.Id);
            if (!deleteFile || entry.FilePath == "") return;
            try
            {
                if (File.Exists(entry.FilePath)) File.Delete(entry.FilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SlideThemeLibrary: failed to delete {File}", entry.FilePath);
            }
        }

        private void DetachAll()
        {
            foreach (var entry in _entries.Values) entry.ChangedSubscription?.Dispose();
            _entries.Clear();
        }

        private void ScheduleSave(Guid id)
        {
            if (_syncing || !_entries.TryGetValue(id, out var entry)) return;
            entry.Debouncer.Debounce(() => _postToSaveThread(() => SaveNow(id)));
        }

        public void SaveNow(Guid id)
        {
            if (Folder == null || !_entries.TryGetValue(id, out var entry)) return;
            var theme = Themes.FirstOrDefault(t => t.Id == id);
            if (theme == null) return;

            try
            {
                var bytes = RenderBytes(theme) ?? throw new InvalidOperationException("Theme could not be serialized");
                var nameChanged = theme.Name != entry.LastName;
                if (!nameChanged && bytes.AsSpan().SequenceEqual(entry.LastBytes) && File.Exists(entry.FilePath))
                    return;

                var path = nameChanged || entry.FilePath == "" ? UniquePath(theme.Name, id) : entry.FilePath;
                File.WriteAllBytes(path, bytes);
                if (entry.FilePath != "" && !string.Equals(path, entry.FilePath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(entry.FilePath))
                {
                    File.Delete(entry.FilePath);
                }

                entry.FilePath = path;
                entry.LastName = theme.Name;
                entry.LastBytes = bytes;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SlideThemeLibrary: failed to save theme {Id}", id);
                _saveFailed.OnNext((id, ex));
            }
        }

        public void FlushPendingSaves()
        {
            foreach (var id in _entries.Keys.ToList()) SaveNow(id);
        }

        private string UniquePath(string name, Guid id)
        {
            var baseName = FilenameUtils.ReplaceInvalidChars(string.IsNullOrWhiteSpace(name) ? id.ToString() : name);
            for (var n = 1; ; n++)
            {
                var fileName = n == 1 ? $"{baseName}.xml" : $"{baseName} ({n}).xml";
                var candidate = Path.Combine(Folder!, fileName);
                var ownedByOther = _entries.Any(kv => kv.Key != id
                    && string.Equals(kv.Value.FilePath, candidate, StringComparison.OrdinalIgnoreCase));
                var ownedByThis = _entries.TryGetValue(id, out var own)
                    && string.Equals(own.FilePath, candidate, StringComparison.OrdinalIgnoreCase);
                if (!ownedByOther && (ownedByThis || !File.Exists(candidate))) return candidate;
            }
        }

        // Serializes a COPY with asset paths relativized, so the live theme keeps absolute paths
        // and mutating it never raises Changed (which would re-trigger a save).
        private byte[]? RenderBytes(BaseSlideTheme theme)
        {
            var copy = new BaseSlideTheme();
            copy.CopyFrom(theme);
            var media = _mediaLibraryPath();
            copy.BackgroundGraphicFilePath = ToStored(copy.BackgroundGraphicFilePath, media);
            copy.DefaultMotionBackgroundVideoPath = ToStored(copy.DefaultMotionBackgroundVideoPath, media);
            return SlideThemeXmlSerializer.TrySerializeToBytes(copy, out var bytes) ? bytes : null;
        }

        private static bool IsAvares(string path) => path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase);

        private static string? ToAbsolute(string? path, string? media)
        {
            if (string.IsNullOrWhiteSpace(path) || IsAvares(path) || Path.IsPathFullyQualified(path)) return path;
            return string.IsNullOrWhiteSpace(media) ? path : RelativeFilePathResolver.ToAbsolutePath(media, path);
        }

        private static string? ToStored(string? path, string? media)
        {
            if (string.IsNullOrWhiteSpace(path) || IsAvares(path) || !Path.IsPathFullyQualified(path)) return path;
            if (string.IsNullOrWhiteSpace(media) || !RelativeFilePathResolver.IsUnderDirectory(media, path)) return path;
            return RelativeFilePathResolver.ToRelativePath(media, path);
        }

        public void Dispose() => DetachAll();
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run the Step 2 command. Expected: PASS (15 tests). If `FilenameUtils.ReplaceInvalidChars("")` or `("a/b:c?")` yields an invalid name, fix in the library call site (the empty case is already routed to the Id).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Models/Library/SlideThemeLibrary.cs HandsLiftedApp.Tests/Services/SlideThemeLibraryTests.cs
git commit -m "feat: add file-backed SlideThemeLibrary"
```

---

### Task 4: Wire the library into Globals

**Files:**
- Modify: `HandsLiftedApp.Core/Globals.cs` (property near line 55; `OnStartup` after the preferences block ~line 106 and after `MainViewModel = new();` ~line 124; `OnShutdown` ~line 152)
- Test: `HandsLiftedApp.Tests/Services/SlideThemeLibraryTests.cs` (add one test)

**Interfaces:**
- Consumes: Task 1 `Globals.ResolveSlideThemesFolder`, `AppPreferences.SlideThemesPath`; Task 3 `SlideThemeLibrary`.
- Produces: `Globals.Instance.SlideThemeLibrary` (`SlideThemeLibrary`, constructed with defaults); `void Globals.ReloadSlideThemes()`.

- [ ] **Step 1: Write the failing test** (append to `SlideThemeLibraryTests`)

```csharp
    [TestMethod]
    public void Initialize_SecondCall_FlushesPendingEditsToOldFolderBeforeSwitching()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Switch" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);
        theme.FontSize = theme.FontSize + 3;

        var otherDir = Path.Combine(_root, "other");
        lib.Initialize(otherDir, null);

        Assert.AreEqual(0, lib.Themes.Count, "new folder is empty");
        var reloadedOld = NewInitialized().Themes.Single();
        Assert.AreEqual(theme.FontSize, reloadedOld.FontSize, "pending edit must land in the old folder");
    }
```

- [ ] **Step 2: Run to verify it passes already or fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~Initialize_SecondCall"`
Expected: PASS (Task 3's `Initialize` already flushes). If it fails, fix `Initialize` before continuing.

- [ ] **Step 3: Implement wiring in `Globals.cs`**

Add the property beside `SongLibraryIndex`:

```csharp
        public HandsLiftedApp.Core.Models.Library.SlideThemeLibrary SlideThemeLibrary { get; } = new();
```

Add the method (next to `SaveAppPreferences`):

```csharp
        public void ReloadSlideThemes()
        {
            var folder = ResolveSlideThemesFolder(AppPreferences?.SlideThemesPath);
            SlideThemeLibrary.Initialize(folder, AppPreferences?.DefaultTheme);
        }
```

In `OnStartup`, right after the app-preferences `{ ... }` block (before `ThumbnailEngineSettings.UseMpvEngine = true;`):

```csharp
            ReloadSlideThemes();
```

Right after `MainViewModel = new();`:

```csharp
            MainViewModel.Playlist.Designs = SlideThemeLibrary.Themes;
```

In `OnShutdown`, right after `SongLibraryIndex.FlushPendingSaves();`:

```csharp
            SlideThemeLibrary.FlushPendingSaves();
```

- [ ] **Step 4: Build**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Globals.cs HandsLiftedApp.Tests/Services/SlideThemeLibraryTests.cs
git commit -m "feat: expose SlideThemeLibrary on Globals and share it as the playlist's Designs"
```

---

### Task 5: Theme resolution with app-level defaults; stop dirtying playlists on theme edits

**Files:**
- Modify: `HandsLiftedApp.Core/Models/PlaylistInstance.cs` (ctor ~115-120, ~262-285; resolve methods ~305-332; `OnDesignsCollectionChanged`/`SubscribeToDesignChanges` ~351-374)
- Test: `HandsLiftedApp.Tests/Models/PlaylistInstanceTests.cs`

**Interfaces:**
- Consumes: Task 1 `AppPreferences.Default*ThemeId`.
- Produces: `Guid? PlaylistInstance.EffectiveDefaultSongThemeId`, `EffectiveDefaultSongMotionThemeId`, `EffectiveDefaultScriptureThemeId` (playlist override, else app preference); `ResolveSongTheme` / `ResolveScriptureTheme` use them; `DefaultThemeAssignmentsChanged` also fires on app default changes.
- Order: explicit id in Designs, then effective default id in Designs, then `AppPreferences.DefaultTheme`, then `new BaseSlideTheme()`.

- [ ] **Step 1: Write the failing tests** (append to `PlaylistInstanceTests`)

```csharp
    [TestMethod]
    public void ResolveSongTheme_UsesAppDefault_WhenPlaylistHasNoOverride()
    {
        var playlist = new PlaylistInstance();
        var appDefault = MakeTheme("AppDefault");
        playlist.Designs.Add(appDefault);
        Globals.Instance.AppPreferences.DefaultSongThemeId = appDefault.Id;

        var result = playlist.ResolveSongTheme(Guid.Empty, hasMotionBackground: false);

        Assert.AreSame(appDefault, result);
    }

    [TestMethod]
    public void ResolveSongTheme_PlaylistOverrideBeatsAppDefault()
    {
        var playlist = new PlaylistInstance();
        var appDefault = MakeTheme("AppDefault");
        var overrideTheme = MakeTheme("Override");
        playlist.Designs.Add(appDefault);
        playlist.Designs.Add(overrideTheme);
        Globals.Instance.AppPreferences.DefaultSongThemeId = appDefault.Id;
        playlist.DefaultSongThemeId = overrideTheme.Id;

        var result = playlist.ResolveSongTheme(Guid.Empty, hasMotionBackground: false);

        Assert.AreSame(overrideTheme, result);
    }

    [TestMethod]
    public void ResolveSongTheme_MotionUsesAppMotionDefault()
    {
        var playlist = new PlaylistInstance();
        var motion = MakeTheme("Motion");
        playlist.Designs.Add(motion);
        Globals.Instance.AppPreferences.DefaultSongMotionThemeId = motion.Id;

        var result = playlist.ResolveSongTheme(Guid.Empty, hasMotionBackground: true);

        Assert.AreSame(motion, result);
    }

    [TestMethod]
    public void ResolveScriptureTheme_UsesAppDefault_WhenPlaylistHasNoOverride()
    {
        var playlist = new PlaylistInstance();
        var scripture = MakeTheme("Scripture");
        playlist.Designs.Add(scripture);
        Globals.Instance.AppPreferences.DefaultScriptureThemeId = scripture.Id;

        var result = playlist.ResolveScriptureTheme(Guid.Empty);

        Assert.AreSame(scripture, result);
    }

    [TestMethod]
    public void DefaultThemeAssignmentsChanged_FiresWhenAppDefaultChanges()
    {
        var playlist = new PlaylistInstance();
        var fireCount = 0;
        playlist.DefaultThemeAssignmentsChanged.Subscribe(_ => fireCount++);

        Globals.Instance.AppPreferences.DefaultSongThemeId = Guid.NewGuid();

        Assert.AreEqual(1, fireCount);
    }

    [TestMethod]
    public void EditingOrAddingDesigns_DoesNotMarkPlaylistDirty()
    {
        var playlist = new PlaylistInstance();
        var theme = MakeTheme("T");
        playlist.Designs.Add(theme);
        playlist.IsDirty = false;

        playlist.Designs.Add(MakeTheme("U"));
        theme.Name = "Renamed";

        Assert.IsFalse(playlist.IsDirty);
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~PlaylistInstanceTests"`
Expected: the five new default/dirty tests FAIL (app defaults ignored; `IsDirty` true).

- [ ] **Step 3: Implement in `PlaylistInstance.cs`**

Replace the `DefaultThemeAssignmentsChanged = ...` assignment (lines 115-120) with:

```csharp
            var playlistDefaultChanges = this.WhenAnyValue(
                    p => p.DefaultSongThemeId,
                    p => p.DefaultSongMotionThemeId,
                    p => p.DefaultScriptureThemeId)
                .Skip(1)
                .Select(_ => Unit.Default);

            // Deferred so each subscriber picks up the AppPreferences instance current at
            // subscription time (Globals.Instance.AppPreferences is replaced in tests).
            DefaultThemeAssignmentsChanged = Observable.Defer(() =>
            {
                var prefs = Globals.Instance.AppPreferences;
                if (prefs == null) return playlistDefaultChanges;
                var appDefaultChanges = prefs.WhenAnyValue(
                        p => p.DefaultSongThemeId,
                        p => p.DefaultSongMotionThemeId,
                        p => p.DefaultScriptureThemeId)
                    .Skip(1)
                    .Select(_ => Unit.Default);
                return playlistDefaultChanges.Merge(appDefaultChanges);
            });
```

Replace the two resolve methods (lines 307-332) with:

```csharp
        public Guid? EffectiveDefaultSongThemeId =>
            DefaultSongThemeId ?? Globals.Instance.AppPreferences?.DefaultSongThemeId;

        public Guid? EffectiveDefaultSongMotionThemeId =>
            DefaultSongMotionThemeId ?? Globals.Instance.AppPreferences?.DefaultSongMotionThemeId;

        public Guid? EffectiveDefaultScriptureThemeId =>
            DefaultScriptureThemeId ?? Globals.Instance.AppPreferences?.DefaultScriptureThemeId;

        public BaseSlideTheme ResolveSongTheme(Guid explicitDesignId, bool hasMotionBackground)
        {
            if (explicitDesignId != Guid.Empty)
            {
                var explicitTheme = Designs.FirstOrDefault(d => d.Id == explicitDesignId);
                if (explicitTheme != null) return explicitTheme;
            }

            var defaultId = hasMotionBackground ? EffectiveDefaultSongMotionThemeId : EffectiveDefaultSongThemeId;
            var byDefault = defaultId.HasValue ? Designs.FirstOrDefault(d => d.Id == defaultId.Value) : null;
            return byDefault ?? Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
        }

        public BaseSlideTheme ResolveScriptureTheme(Guid explicitDesignId)
        {
            if (explicitDesignId != Guid.Empty)
            {
                var explicitTheme = Designs.FirstOrDefault(d => d.Id == explicitDesignId);
                if (explicitTheme != null) return explicitTheme;
            }

            var defaultId = EffectiveDefaultScriptureThemeId;
            var byDefault = defaultId.HasValue ? Designs.FirstOrDefault(d => d.Id == defaultId.Value) : null;
            return byDefault ?? Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
        }
```

Stop dirtying on theme edits. In the `WhenAnyValue(p => p.Title, p => p.LogoGraphicFile, p => p.Designs, p => p.Items, ...)` block (~272-285) delete the `p => p.Designs,` line. In `OnDesignsCollectionChanged` remove `IsDirty = true;` but keep the `Changed?.Invoke(...)` and the subscribe/unsubscribe loops. In `SubscribeToDesignChanges` change the subscription body to only `Changed?.Invoke(this, EventArgs.Empty);` (no `IsDirty = true`). Check `System.Reactive.Linq` is imported in this file (needed for `Observable.Defer` and `Merge`).

- [ ] **Step 4: Run to verify it passes**

Run the Step 2 command. Expected: PASS. Then Grep `IsDirty` in `HandsLiftedApp.Tests/Models/PlaylistInstanceTests.cs`; if an existing test asserts that changing `Designs` dirties the playlist, update it to assert the new behavior (not dirty).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Models/PlaylistInstance.cs HandsLiftedApp.Tests/Models/PlaylistInstanceTests.cs
git commit -m "feat: resolve default themes from app preferences with playlist override"
```

---

### Task 6: Migration of embedded themes

**Files:**
- Create: `HandsLiftedApp.Core/Services/SlideThemeMigration.cs`
- Test: `HandsLiftedApp.Tests/Services/SlideThemeMigrationTests.cs`

**Interfaces:**
- Consumes: `SlideThemeLibrary` (Task 3: `Contains`, `Themes`, `SaveNow`), `AppPreferencesViewModel.Default*ThemeId` (Task 1), `PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(string, string?)` and `MediaLibraryNotConfiguredException`, `RelativeFilePathResolver.ToAbsolutePath` / `ToAbsoluteMediaPath`.
- Produces (namespace `HandsLiftedApp.Core.Services`):

```csharp
public sealed record SlideThemeMigrationResult(
    int Imported, int SkippedExisting,
    Guid? DefaultSongThemeId, Guid? DefaultSongMotionThemeId, Guid? DefaultScriptureThemeId,
    bool AppDefaultsChanged, bool NeedsResave);

public static class SlideThemeMigration
{
    public static SlideThemeMigrationResult Migrate(
        IEnumerable<BaseSlideTheme> embedded, string playlistDirectory, string? mediaLibraryPath,
        SlideThemeLibrary library, AppPreferencesViewModel prefs,
        Guid? songId, Guid? motionId, Guid? scriptureId);
}
```

The returned `Default*ThemeId` values are what the playlist should keep as overrides. Spec refinement: for each playlist default id, if the theme is not in the library, keep the id; if the app default is unset, adopt it into `prefs` and return null; if it equals the app default, return null; if it differs from a set app default, keep it as the override (the spec said to clear it, but that would silently discard the playlist's choice).

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.IO;
using System.Linq;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class SlideThemeMigrationTests
{
    private string _root = null!;
    private string _playlistDir = null!;
    private string _media = null!;
    private SlideThemeLibrary _lib = null!;
    private AppPreferencesViewModel _prefs = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "SlideThemeMigrationTests_" + Guid.NewGuid().ToString("N"));
        _playlistDir = Path.Combine(_root, "playlist");
        _media = Path.Combine(_root, "media");
        Directory.CreateDirectory(_playlistDir);
        Directory.CreateDirectory(_media);
        _prefs = new AppPreferencesViewModel();
        Globals.Instance.AppPreferences = _prefs;
        _lib = new SlideThemeLibrary(() => _media, a => a());
        _lib.Initialize(Path.Combine(_root, "themes"), null);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private SlideThemeMigrationResult Run(BaseSlideTheme[] themes, string? media = "", Guid? song = null,
        Guid? motion = null, Guid? scripture = null) =>
        SlideThemeMigration.Migrate(themes, _playlistDir, media == "" ? _media : media, _lib, _prefs,
            song, motion, scripture);

    [TestMethod]
    public void NewThemes_AreImportedWithTheirIdsPreserved()
    {
        var a = new BaseSlideTheme { Name = "A" };
        var b = new BaseSlideTheme { Name = "B" };

        var result = Run(new[] { a, b });

        Assert.AreEqual(2, result.Imported);
        Assert.IsTrue(_lib.Contains(a.Id));
        Assert.IsTrue(_lib.Contains(b.Id));
        Assert.IsTrue(result.NeedsResave);
    }

    [TestMethod]
    public void ExistingId_IsNotOverwritten_AndReopenImportsNothing()
    {
        var original = new BaseSlideTheme { Name = "Library version" };
        _lib.Themes.Add(original);
        var embedded = new BaseSlideTheme { Name = "Playlist version" };
        embedded.Id = original.Id;

        var first = Run(new[] { embedded });
        var second = Run(new[] { embedded });

        Assert.AreEqual(0, first.Imported);
        Assert.AreEqual(1, first.SkippedExisting);
        Assert.AreEqual(0, second.Imported);
        Assert.AreEqual("Library version", _lib.Themes.Single(t => t.Id == original.Id).Name);
    }

    [TestMethod]
    public void NoEmbeddedThemes_NothingToDo()
    {
        var result = Run(Array.Empty<BaseSlideTheme>());

        Assert.AreEqual(0, result.Imported);
        Assert.IsFalse(result.NeedsResave);
    }

    [TestMethod]
    public void BackgroundImage_IsCopiedIntoMediaLibrary()
    {
        var img = Path.Combine(_playlistDir, "Themes", "Backgrounds", "bg.png");
        Directory.CreateDirectory(Path.GetDirectoryName(img)!);
        File.WriteAllBytes(img, new byte[] { 1, 2, 3 });
        var theme = new BaseSlideTheme
        {
            Name = "Img",
            BackgroundGraphicFilePath = Path.Combine("Themes", "Backgrounds", "bg.png")
        };

        Run(new[] { theme });

        var imported = _lib.Themes.Single();
        StringAssert.StartsWith(imported.BackgroundGraphicFilePath, _media);
        Assert.IsTrue(File.Exists(imported.BackgroundGraphicFilePath));
    }

    [TestMethod]
    public void MediaLibraryNotConfigured_StillImports_KeepingAbsolutePath()
    {
        var img = Path.Combine(_playlistDir, "bg.png");
        File.WriteAllBytes(img, new byte[] { 1 });
        var theme = new BaseSlideTheme { Name = "NoMedia", BackgroundGraphicFilePath = "bg.png" };

        var result = Run(new[] { theme }, media: null);

        Assert.AreEqual(1, result.Imported);
        Assert.AreEqual(img, _lib.Themes.Single().BackgroundGraphicFilePath);
    }

    [TestMethod]
    public void MissingAssetFile_StillImportsTheme()
    {
        var theme = new BaseSlideTheme { Name = "Missing", BackgroundGraphicFilePath = "does-not-exist.png" };

        var result = Run(new[] { theme });

        Assert.AreEqual(1, result.Imported);
        Assert.IsTrue(_lib.Contains(theme.Id));
    }

    [TestMethod]
    public void AvaresBackground_IsLeftUntouched()
    {
        const string avares = "avares://HandsLiftedApp.Core/Assets/DefaultTheme/default-bg.png";
        var theme = new BaseSlideTheme { Name = "Av", BackgroundGraphicFilePath = avares };

        Run(new[] { theme });

        Assert.AreEqual(avares, _lib.Themes.Single().BackgroundGraphicFilePath);
    }

    [TestMethod]
    public void PlaylistDefault_IsAdopted_WhenAppDefaultUnset_AndPlaylistOverrideCleared()
    {
        var theme = new BaseSlideTheme { Name = "D" };

        var result = Run(new[] { theme }, song: theme.Id);

        Assert.AreEqual(theme.Id, _prefs.DefaultSongThemeId);
        Assert.IsNull(result.DefaultSongThemeId);
        Assert.IsTrue(result.AppDefaultsChanged);
    }

    [TestMethod]
    public void PlaylistDefault_DifferingFromSetAppDefault_IsKeptAsOverride()
    {
        var appDefault = new BaseSlideTheme { Name = "App" };
        _lib.Themes.Add(appDefault);
        _prefs.DefaultSongThemeId = appDefault.Id;
        var theme = new BaseSlideTheme { Name = "Mine" };

        var result = Run(new[] { theme }, song: theme.Id);

        Assert.AreEqual(appDefault.Id, _prefs.DefaultSongThemeId);
        Assert.AreEqual(theme.Id, result.DefaultSongThemeId);
        Assert.IsFalse(result.AppDefaultsChanged);
    }

    [TestMethod]
    public void PlaylistDefault_EqualToAppDefault_IsCleared()
    {
        var appDefault = new BaseSlideTheme { Name = "App" };
        _lib.Themes.Add(appDefault);
        _prefs.DefaultScriptureThemeId = appDefault.Id;

        var result = Run(Array.Empty<BaseSlideTheme>(), scripture: appDefault.Id);

        Assert.IsNull(result.DefaultScriptureThemeId);
        Assert.IsTrue(result.NeedsResave);
    }

    [TestMethod]
    public void PlaylistDefault_PointingAtUnknownTheme_IsLeftAlone()
    {
        var dangling = Guid.NewGuid();

        var result = Run(Array.Empty<BaseSlideTheme>(), motion: dangling);

        Assert.AreEqual(dangling, result.DefaultSongMotionThemeId);
        Assert.IsNull(_prefs.DefaultSongMotionThemeId);
        Assert.IsFalse(result.NeedsResave);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SlideThemeMigrationTests"`
Expected: build FAIL (`SlideThemeMigration` not defined).

- [ ] **Step 3: Implement**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;
using Serilog;

namespace HandsLiftedApp.Core.Services
{
    public sealed record SlideThemeMigrationResult(
        int Imported,
        int SkippedExisting,
        Guid? DefaultSongThemeId,
        Guid? DefaultSongMotionThemeId,
        Guid? DefaultScriptureThemeId,
        bool AppDefaultsChanged,
        bool NeedsResave);

    /// <summary>
    /// Copies themes embedded in a legacy playlist into the app-level SlideThemeLibrary.
    /// Never overwrites a library theme that has the same Id.
    /// </summary>
    public static class SlideThemeMigration
    {
        public static SlideThemeMigrationResult Migrate(
            IEnumerable<BaseSlideTheme> embedded,
            string playlistDirectory,
            string? mediaLibraryPath,
            SlideThemeLibrary library,
            AppPreferencesViewModel prefs,
            Guid? songId, Guid? motionId, Guid? scriptureId)
        {
            int imported = 0, skipped = 0;
            foreach (var theme in embedded)
            {
                if (library.Contains(theme.Id))
                {
                    skipped++;
                    Log.Information("Slide theme migration - {Name} ({Id}) already in library, skipping", theme.Name, theme.Id);
                    continue;
                }

                theme.BackgroundGraphicFilePath =
                    ImportAsset(RelativeFilePathResolver.ToAbsolutePath(playlistDirectory, theme.BackgroundGraphicFilePath),
                        mediaLibraryPath);
                theme.DefaultMotionBackgroundVideoPath =
                    ImportAsset(RelativeFilePathResolver.ToAbsoluteMediaPath(mediaLibraryPath, playlistDirectory,
                        theme.DefaultMotionBackgroundVideoPath), mediaLibraryPath);

                library.Themes.Add(theme);
                library.SaveNow(theme.Id);
                imported++;
            }

            var appChanged = false;
            var newSong = Adopt(songId, prefs.DefaultSongThemeId, id => prefs.DefaultSongThemeId = id, library, ref appChanged);
            var newMotion = Adopt(motionId, prefs.DefaultSongMotionThemeId, id => prefs.DefaultSongMotionThemeId = id, library, ref appChanged);
            var newScripture = Adopt(scriptureId, prefs.DefaultScriptureThemeId, id => prefs.DefaultScriptureThemeId = id, library, ref appChanged);

            var needsResave = imported + skipped > 0
                              || newSong != songId || newMotion != motionId || newScripture != scriptureId;

            return new SlideThemeMigrationResult(imported, skipped, newSong, newMotion, newScripture, appChanged, needsResave);
        }

        private static Guid? Adopt(Guid? playlistId, Guid? appId, Action<Guid> setApp, SlideThemeLibrary library,
            ref bool appChanged)
        {
            if (!playlistId.HasValue) return null;
            if (!library.Contains(playlistId.Value)) return playlistId; // dangling: leave as is
            if (!appId.HasValue)
            {
                setApp(playlistId.Value);
                appChanged = true;
                return null;
            }
            return appId == playlistId ? null : playlistId;
        }

        // Copies a theme asset into the Media Library. Any failure keeps the absolute path so the
        // theme still imports (a missing or unconfigured asset must never block opening a playlist).
        private static string? ImportAsset(string? absolutePath, string? mediaLibraryPath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath)
                || absolutePath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                return absolutePath;

            if (!File.Exists(absolutePath))
            {
                Log.Warning("Slide theme migration - asset not found, keeping path: {Path}", absolutePath);
                return absolutePath;
            }

            try
            {
                return PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(absolutePath, mediaLibraryPath);
            }
            catch (MediaLibraryNotConfiguredException)
            {
                Log.Warning("Slide theme migration - no Media Library configured, keeping absolute path: {Path}", absolutePath);
                return absolutePath;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Slide theme migration - failed to copy asset, keeping path: {Path}", absolutePath);
                return absolutePath;
            }
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run the Step 2 command. Expected: PASS (11 tests).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Services/SlideThemeMigration.cs HandsLiftedApp.Tests/Services/SlideThemeMigrationTests.cs
git commit -m "feat: migrate playlist-embedded slide themes into the app-level library"
```

---

### Task 7: Stop writing Designs; migrate on playlist open

**Files:**
- Modify: `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs:43-67`
- Modify: `HandsLiftedApp.Core/ViewModels/MainViewModel.cs:314-352` and `:397`
- Test: `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs`

**Interfaces:**
- Consumes: Task 6 `SlideThemeMigration.Migrate`, Task 4 `Globals.Instance.SlideThemeLibrary` and `SaveAppPreferences()`.
- Produces: `SerializePlaylist` no longer emits any `Designs`; `DeserializePlaylist` still reads legacy `Designs`.

- [ ] **Step 1: Write the failing tests**

In `HandsLiftedDocXmlSerializerTests.cs` the tests that call `playlist.Designs.Add(theme)` and then read `deserialized.Designs` (around lines 115-191: BackgroundMode / DefaultMotionBackgroundVideoPath round-trips and the media-library-relative video path test) assert behavior that no longer exists; delete those tests. Their path-relativization coverage now lives in `SlideThemeLibraryTests` (Task 3). Keep the default-theme-id round-trip tests (~77-111). Add:

```csharp
    [TestMethod]
    public void SerializePlaylist_DoesNotWriteDesigns()
    {
        var playlist = new PlaylistInstance();
        playlist.Designs.Add(new HandsLiftedApp.Data.SlideTheme.BaseSlideTheme { Name = "Global now" });
        var path = Path.Combine(_tempDir, "p.xml");

        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);
        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

        Assert.AreEqual(0, deserialized.Designs.Count);
        Assert.IsFalse(File.ReadAllText(path).Contains("Global now"));
    }

    [TestMethod]
    public void DeserializePlaylist_StillReadsLegacyEmbeddedDesigns()
    {
        var legacy = new HandsLiftedApp.Data.Models.Playlist();
        var theme = new HandsLiftedApp.Data.SlideTheme.BaseSlideTheme { Name = "Legacy" };
        legacy.Designs.Add(theme);
        var path = Path.Combine(_tempDir, "legacy.xml");
        using (var fs = File.Create(path))
        {
            new System.Xml.Serialization.XmlSerializer(typeof(HandsLiftedApp.Data.Models.Playlist))
                .Serialize(fs, legacy);
        }

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

        Assert.AreEqual(theme.Id, deserialized.Designs.Single().Id);
    }
```

(If `Playlist` lives in a different namespace than `HandsLiftedApp.Data.Models`, adjust; it is the data class in `HandsLiftedApp.Data/Models/Playlist.cs`.)

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~HandsLiftedDocXmlSerializerTests"`
Expected: `SerializePlaylist_DoesNotWriteDesigns` FAILS; the legacy-read test passes.

- [ ] **Step 3: Implement**

`HandsLiftedDocXmlSerializer.cs`: delete the whole `Designs = new ObservableCollection<BaseSlideTheme>(...)` initializer (lines 43-67) so the object initializer goes from `DefaultScriptureThemeId = playlist.DefaultScriptureThemeId,` straight to `Items = new TrulyObservableCollection<Item>()`. Remove usings that become unused. Keep `ToRelativePathIfUnderMediaLibrary` (other code uses it).

`MainViewModel.cs`, in the open-playlist handler:

1. Delete the three lines `Playlist.DefaultSongThemeId = x.DefaultSongThemeId;` ... `Playlist.DefaultScriptureThemeId = x.DefaultScriptureThemeId;` (lines 317-319).
2. Replace the whole `var loadedDesigns = ...` through `Playlist.Designs = new ObservableCollection<BaseSlideTheme>(designsWithDefault.ToList());` block (lines 325-352) with:

```csharp
                // Themes are app-wide now. Import any themes embedded in a legacy playlist into the
                // library (never overwriting an existing Id), then point the playlist at the library.
                var prefs = Globals.Instance.AppPreferences;
                var migration = SlideThemeMigration.Migrate(
                    x.Designs, playlistDirectoryPath, prefs?.MediaLibraryPath,
                    Globals.Instance.SlideThemeLibrary, prefs!,
                    x.DefaultSongThemeId, x.DefaultSongMotionThemeId, x.DefaultScriptureThemeId);
                Playlist.DefaultSongThemeId = migration.DefaultSongThemeId;
                Playlist.DefaultSongMotionThemeId = migration.DefaultSongMotionThemeId;
                Playlist.DefaultScriptureThemeId = migration.DefaultScriptureThemeId;
                if (migration.AppDefaultsChanged) Globals.Instance.SaveAppPreferences();
                Playlist.Designs = Globals.Instance.SlideThemeLibrary.Themes;
```

3. Change `bool finalIsDirty = loadFilePath != msg.FilePath;` to `bool finalIsDirty = loadFilePath != msg.FilePath || migration.NeedsResave;`.
4. Add `using HandsLiftedApp.Core.Services;` if missing. `prefs` is non-null in production (set in `OnStartup`).

- [ ] **Step 4: Run to verify it passes**

Run the Step 2 command, then `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`.
Expected: tests PASS, build succeeded. Grep `HandsLiftedApp.Core` for any remaining `loadedDesigns` reference (none expected).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs HandsLiftedApp.Core/ViewModels/MainViewModel.cs HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs
git commit -m "feat: stop embedding themes in playlists and migrate them on open"
```

---

### Task 8: Designer uses app-level defaults and Media Library assets

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs` (lines ~269-327, ~479-490)
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml` (lines ~126, ~139, ~152)

**Interfaces:**
- Consumes: Task 1 `AppPreferences.Default*ThemeId`; `PortableAssetCopier.ResolveOrCopyIntoMediaLibrary`, `MediaLibraryNotConfiguredException`.
- Produces: Designer "set default" actions write the app preference and clear the current playlist's override; removal is blocked for any app or playlist default; background images are copied into the Media Library. Add/Duplicate/Import/Remove keep working on `Playlist.Designs` (the library collection).

No unit-testable surface (Avalonia code-behind); verification is a build plus the manual check in Task 10.

- [ ] **Step 1: Update the three default handlers** (`SetDefaultSongTheme_OnClick`, `SetDefaultSongMotionTheme_OnClick`, `SetDefaultScriptureTheme_OnClick`)

```csharp
        private void SetDefaultSongTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                Globals.Instance.AppPreferences.DefaultSongThemeId = item.Id;
                mainViewModel.Playlist.DefaultSongThemeId = null; // clear this playlist's override
            }
        }

        private void SetDefaultSongMotionTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                Globals.Instance.AppPreferences.DefaultSongMotionThemeId = item.Id;
                mainViewModel.Playlist.DefaultSongMotionThemeId = null;
            }
        }

        private void SetDefaultScriptureTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                Globals.Instance.AppPreferences.DefaultScriptureThemeId = item.Id;
                mainViewModel.Playlist.DefaultScriptureThemeId = null;
            }
        }
```

- [ ] **Step 2: Update the removal guard in `RemoveItem_OnClick`**

Replace the `if (item.Id == Globals.Instance.AppPreferences?.DefaultTheme?.Id || ...)` condition with:

```csharp
                        var prefs = Globals.Instance.AppPreferences;
                        var playlist = mainViewModel.Playlist;
                        if (item.Id == prefs?.DefaultTheme?.Id
                            || item.Id == prefs?.DefaultSongThemeId
                            || item.Id == prefs?.DefaultSongMotionThemeId
                            || item.Id == prefs?.DefaultScriptureThemeId
                            || item.Id == playlist.DefaultSongThemeId
                            || item.Id == playlist.DefaultSongMotionThemeId
                            || item.Id == playlist.DefaultScriptureThemeId)
```

- [ ] **Step 3: Replace the background copy block in `ChangeThemeBgGraphic_OnClick`**

Replace the `if (!isSharedDefaultTheme) { localPath = PortableAssetCopier.CopyIntoSubfolder(...); }` block with:

```csharp
                        if (!isSharedDefaultTheme)
                        {
                            try
                            {
                                localPath = PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(
                                    localPath, Globals.Instance.AppPreferences?.MediaLibraryPath);
                            }
                            catch (MediaLibraryNotConfiguredException)
                            {
                                MessageBus.Current.SendMessage(new MessageWindowViewModel()
                                {
                                    Title = "Media Library not configured",
                                    Content = "Open Set Up and choose a Media Library folder before setting a theme background."
                                });
                                return;
                            }
                        }
```

- [ ] **Step 4: Point the "is default" bindings at app preferences**

Read the first ~35 lines of `SlideThemeDesigner.axaml` and confirm there is an `xmlns` mapping for `HandsLiftedApp.Core` (e.g. `xmlns:app="using:HandsLiftedApp.Core"`, as in `SetupWindow.axaml`); add one if missing. Replace the three bindings:

```xml
<Binding Path="$parent[UserControl].DataContext.Playlist.DefaultSongThemeId" />
<Binding Path="$parent[UserControl].DataContext.Playlist.DefaultSongMotionThemeId" />
<Binding Path="$parent[UserControl].DataContext.Playlist.DefaultScriptureThemeId" />
```

with, respectively:

```xml
<Binding Source="{x:Static app:Globals.Instance}" Path="AppPreferences.DefaultSongThemeId" />
<Binding Source="{x:Static app:Globals.Instance}" Path="AppPreferences.DefaultSongMotionThemeId" />
<Binding Source="{x:Static app:Globals.Instance}" Path="AppPreferences.DefaultScriptureThemeId" />
```

Warning: this file has uncommitted user edits; keep your hunks minimal.

- [ ] **Step 5: Build and commit**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: Build succeeded.

```bash
git add HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs
git add -p HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml
git commit -m "feat: designer sets app-level default themes and stores backgrounds in the Media Library"
```

---

### Task 9: Set Up UI for the themes folder

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml` (Library tab, after the Media Library `DockPanel`, ~line 250)
- Modify: `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml.cs` (add `using System.IO;`; new handler after `BrowseMediaLibraryButton_OnClick`)

**Interfaces:**
- Consumes: Task 1 `AppPreferences.SlideThemesPath`, `Globals.ResolveSlideThemesFolder`, `Globals.SaveAppPreferences()`; Task 4 `Globals.ReloadSlideThemes()`.
- Produces: a "Slide Themes" row on the Library tab. The TextBox is read-only on purpose: typing a path would create a directory per keystroke, so only Browse changes it.

- [ ] **Step 1: Add the XAML row** (insert immediately after the Media Library `</DockPanel>` and before the `Libraries` header `TextBlock`)

```xml
                            <TextBlock
                                FontWeight="SemiBold"
                                Margin="0,24,0,4"
                                Text="Slide Themes" />
                            <TextBlock
                                Foreground="{DynamicResource SystemColorGrayTextBrush}"
                                Margin="0,0,0,4"
                                Text="Folder where slide themes (designs) are stored, one XML file per theme. Themes are shared by every playlist. Changing this folder reloads themes; reopen the current playlist to apply them."
                                TextWrapping="Wrap" />
                            <DockPanel>
                                <Button
                                    Click="BrowseSlideThemesButton_OnClick"
                                    DockPanel.Dock="Right"
                                    Margin="8,0,0,0">
                                    Browse…
                                </Button>
                                <TextBox
                                    IsReadOnly="True"
                                    Text="{Binding Source={x:Static app:Globals.Instance}, Path=AppPreferences.SlideThemesPath, Mode=OneWay}" />
                            </DockPanel>
```

- [ ] **Step 2: Add the handler**

```csharp
        private async void BrowseSlideThemesButton_OnClick(object? sender, RoutedEventArgs e)
        {
            var prefs = Globals.Instance.AppPreferences;
            var currentPath = Globals.ResolveSlideThemesFolder(prefs.SlideThemesPath);
            var startFolder = Directory.Exists(currentPath)
                ? await StorageProvider.TryGetFolderFromPathAsync(currentPath)
                : null;

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Slide Themes Folder",
                AllowMultiple = false,
                SuggestedStartLocation = startFolder
            });

            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            {
                prefs.SlideThemesPath = path;
                Globals.Instance.ReloadSlideThemes();
                Globals.Instance.SaveAppPreferences();
            }
        }
```

- [ ] **Step 3: Build and commit**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: Build succeeded.

```bash
git add HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml.cs
git commit -m "feat: add slide themes folder to Set Up"
```

---

### Task 10: Full verification

**Files:** none (verification only).

- [ ] **Step 1: Run the whole test suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj`
Expected: all PASS. Fix regressions in existing theme-related tests (`ScriptureItemInstanceTests`, `SongItemInstanceMotionBackgroundResolutionTests`, `ItemInstanceFactoryTests`) by adjusting setup, not by weakening assertions.

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build` (solution root).
Expected: Build succeeded, no new warnings from touched files.

- [ ] **Step 3: Manual UI checks (hand off to the user; UI cannot be verified in-session)**

Per CLAUDE.md, this class of change produces no compiler or test failures when it breaks, so ask the user to click through:

1. Set Up, Library tab: the Slide Themes row shows the default `%APPDATA%\HandsLifted\SlideThemes`. Browse to a new folder: themes reload, and `appstate.json` has the new path immediately (check before closing the app).
2. Open a playlist saved before this change (it has embedded themes): each theme appears in the Designer and as an `.xml` file in the themes folder, background images appear under the Media Library, and the playlist shows as modified. Save it and confirm the saved XML has no theme bodies. Open it again: no duplicates.
3. In the Designer: add, rename, duplicate, delete a theme and watch the folder. Use "set as default" for song, motion and scripture themes, then confirm the default badge moves and new songs render with the chosen theme.
4. Edit a theme's font size and confirm open slides/previews update live and the XML updates within a second.
5. Open a second playlist: the same themes are available there.

- [ ] **Step 4: Report**

Report test results and the Task 8/9 build results to the user, and list the manual checks above as not yet verified.
