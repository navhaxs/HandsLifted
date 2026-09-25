# Song Item Motion-Background Video Override Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user assign a per-song-item motion-background video that overrides a motion-background theme's default video, editable via a new reusable video-picker control used both from `ItemEditDockRoot` (per playlist item) and `SlideThemeDesigner` (per theme).

**Architecture:** Add an orthogonal `BackgroundMode` (Plain/MotionBackground) + `DefaultMotionBackgroundVideoPath` to `BaseSlideTheme`, and a playlist-item-scoped `MotionBackgroundVideoOverride` to `SongItemInstance`/`SongItemReference`, threaded through serialization exactly like the existing `SlideTransitionDurationMs` per-item-override precedent. A new `ResolvedMotionBackgroundVideoPath` computed property on `SongItemInstance` implements the 3-tier fallback (item override → song's own video → theme default). A new reusable `MotionBackgroundVideoPicker` UserControl (Browse/Clear/path display) is bound from both UI call sites.

**Tech Stack:** Avalonia 11, C#, ReactiveUI, MSTest (`HandsLiftedApp.Tests`).

**Spec:** [docs/superpowers/specs/2026-09-25-song-motion-background-video-override-design.md](../specs/2026-09-25-song-motion-background-video-override-design.md)

## Global Constraints

- Video file filter patterns: `*.mp4, *.mov, *.avi, *.wmv, *.mkv, *.webm` — exact list from `MotionBackgroundService.SupportedVideoExtensions`.
- Resolution order: item override → song's own video (`SongItem.MotionBackgroundVideoPath`) → theme's default video → none (plain).
- The new picker never copies files into the media library — it references the chosen path as-is (decision 6 in the spec).
- The new `ItemEditDockRoot` "Video" button is hidden entirely (not shown disabled) unless the song item's resolved theme has `BackgroundMode == MotionBackground`.
- All file-picker calls in new/edited code go through `Globals.Instance.MainViewModel.ShowOpenFileDialog` (the `Interaction<FilePickerOpenOptions?, IReadOnlyList<IStorageFile>?>` handled in `MainWindow.axaml.cs`), never `TopLevel.GetTopLevel(this)` — both new UI touch points sit inside nested `Flyout`s, where `TopLevel.GetTopLevel` silently resolves to `null` (see this project's `CLAUDE.md`).
- Test framework: MSTest (`[TestClass]`/`[TestMethod]`/`[DataTestMethod]`/`Assert.*`/`StringAssert.*`), matching `HandsLiftedApp.Tests`'s existing convention exactly.

## Review Focus

- Song item with a Plain (or no) theme assigned: the "Video" override button must be completely absent from the Edit-dock UI, not merely disabled — covered by Task 5's converter tests and Task 7's manual click-through.
- Resolution order must be override > song's own video > theme default, in that exact order — a swapped comparison silently changes which video plays for existing playlists — covered by Task 2's 4 resolution-chain tests.
- A MotionBackground-mode theme with no default video set, and an item with neither override nor its own song video: `HasMotionBackground` must be `false`, never crash or show a broken video slide — covered by Task 2's "no override no song video no theme" test.
- An item-level override video living outside the configured Media Library folder must round-trip as an absolute path, not get corrupted into a broken `..` relative path — covered by Task 4's "outside media library" test.
- Clearing an item-level override (or leaving a theme's default video unset) must persist as `null` through a save/reload cycle, not silently retain a stale value — covered by Task 4's "no override stays null" test and Task 3's "Plain mode stays null" test.

---

### Task 1: Theme background mode + default video path

**Files:**
- Create: `HandsLiftedApp.Data/Models/SlideTheme/ThemeBackgroundMode.cs`
- Modify: `HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs:125-132` (near `Type`), `:426-433` (near `BackgroundGraphicFilePath`)
- Test: `HandsLiftedApp.Tests/Models/SlideTheme/BaseSlideThemeBackgroundModeTests.cs`

**Interfaces:**
- Produces: enum `HandsLiftedApp.Data.SlideTheme.ThemeBackgroundMode { Plain, MotionBackground }`; `BaseSlideTheme.BackgroundMode` (`ThemeBackgroundMode`, default `Plain`); `BaseSlideTheme.DefaultMotionBackgroundVideoPath` (`string?`, default `null`).

- [ ] **Step 1: Write the failing test**

```csharp
// HandsLiftedApp.Tests/Models/SlideTheme/BaseSlideThemeBackgroundModeTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Models.SlideTheme;

[TestClass]
public class BaseSlideThemeBackgroundModeTests
{
    [TestMethod]
    public void BackgroundMode_DefaultsToPlain()
    {
        var theme = new BaseSlideTheme();

        Assert.AreEqual(ThemeBackgroundMode.Plain, theme.BackgroundMode);
    }

    [TestMethod]
    public void BackgroundMode_CanBeSetToMotionBackground()
    {
        var theme = new BaseSlideTheme { BackgroundMode = ThemeBackgroundMode.MotionBackground };

        Assert.AreEqual(ThemeBackgroundMode.MotionBackground, theme.BackgroundMode);
    }

    [TestMethod]
    public void DefaultMotionBackgroundVideoPath_DefaultsToNull()
    {
        var theme = new BaseSlideTheme();

        Assert.IsNull(theme.DefaultMotionBackgroundVideoPath);
    }

    [TestMethod]
    public void CopyFrom_CopiesBackgroundModeAndDefaultVideoPath()
    {
        var source = new BaseSlideTheme
        {
            BackgroundMode = ThemeBackgroundMode.MotionBackground,
            DefaultMotionBackgroundVideoPath = @"C:\Videos\bg.mp4"
        };
        var target = new BaseSlideTheme();

        target.CopyFrom(source);

        Assert.AreEqual(ThemeBackgroundMode.MotionBackground, target.BackgroundMode);
        Assert.AreEqual(@"C:\Videos\bg.mp4", target.DefaultMotionBackgroundVideoPath);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests --filter FullyQualifiedName~BaseSlideThemeBackgroundModeTests`
Expected: build FAILS — `BaseSlideTheme` has no `BackgroundMode`/`DefaultMotionBackgroundVideoPath` members and `ThemeBackgroundMode` does not exist yet.

- [ ] **Step 3: Implement**

Create `HandsLiftedApp.Data/Models/SlideTheme/ThemeBackgroundMode.cs`:

```csharp
using System;

namespace HandsLiftedApp.Data.SlideTheme
{
    [Serializable]
    public enum ThemeBackgroundMode
    {
        Plain = 0,
        MotionBackground,
    }
}
```

In `BaseSlideTheme.cs`, immediately after the existing `Type` property (after line 132):

```csharp
        private ThemeBackgroundMode _backgroundMode = ThemeBackgroundMode.Plain;

        [DataMember]
        public ThemeBackgroundMode BackgroundMode
        {
            get => _backgroundMode;
            set => this.RaiseAndSetIfChanged(ref _backgroundMode, value);
        }
```

In `BaseSlideTheme.cs`, immediately after the existing `BackgroundGraphicFilePath` property (after line 433):

```csharp
        private string? _defaultMotionBackgroundVideoPath;

        [DataMember]
        public string? DefaultMotionBackgroundVideoPath
        {
            get => _defaultMotionBackgroundVideoPath;
            set => this.RaiseAndSetIfChanged(ref _defaultMotionBackgroundVideoPath, value);
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests --filter FullyQualifiedName~BaseSlideThemeBackgroundModeTests`
Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Data/Models/SlideTheme/ThemeBackgroundMode.cs HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs HandsLiftedApp.Tests/Models/SlideTheme/BaseSlideThemeBackgroundModeTests.cs
git commit -m "feat: add theme background mode and default motion background video path"
```

---

### Task 2: Song item resolution chain (item override → song video → theme default)

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs:141-145` (add override property + resolved property), `:293-311` (constructor subscription), `:620-623` (`HasMotionBackground`)
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/SongItemInstanceMotionBackgroundResolutionTests.cs`

**Interfaces:**
- Consumes: `BaseSlideTheme.BackgroundMode`, `BaseSlideTheme.DefaultMotionBackgroundVideoPath` (Task 1); existing `MotionBackgroundService.IsValidVideoFile(string?)`; existing `SongItemInstance.ResolvedDesignTheme`, `SongItemInstance.MotionBackgroundVideoPath` (song's own, existing).
- Produces: `SongItemInstance.MotionBackgroundVideoOverride` (`string?`, get/set); `SongItemInstance.ResolvedMotionBackgroundVideoPath` (`string?`, get-only); updated `SongItemInstance.HasMotionBackground` (`bool`).

- [ ] **Step 1: Write the failing test**

```csharp
// HandsLiftedApp.Tests/Models/RuntimeData/Items/SongItemInstanceMotionBackgroundResolutionTests.cs
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class SongItemInstanceMotionBackgroundResolutionTests
{
    private static string TempVideoPath(string fileName) => Path.Combine(Path.GetTempPath(), fileName);

    [TestMethod]
    public void ResolvedMotionBackgroundVideoPath_NoOverrideNoSongVideoNoTheme_ReturnsNull()
    {
        var instance = new SongItemInstance(null) { SongId = Guid.NewGuid() };

        Assert.IsNull(instance.ResolvedMotionBackgroundVideoPath);
        Assert.IsFalse(instance.HasMotionBackground);
    }

    [TestMethod]
    public void ResolvedMotionBackgroundVideoPath_ThemeMotionBackgroundWithDefaultVideo_UsesThemeDefault()
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme
        {
            BackgroundMode = ThemeBackgroundMode.MotionBackground,
            DefaultMotionBackgroundVideoPath = TempVideoPath("theme-default.mp4")
        };
        playlist.Designs.Add(theme);
        var instance = new SongItemInstance(playlist) { SongId = Guid.NewGuid(), Design = theme.Id };

        Assert.AreEqual(TempVideoPath("theme-default.mp4"), instance.ResolvedMotionBackgroundVideoPath);
        Assert.IsTrue(instance.HasMotionBackground);
    }

    [TestMethod]
    public void ResolvedMotionBackgroundVideoPath_SongOwnVideoSet_TakesPriorityOverThemeDefault()
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme
        {
            BackgroundMode = ThemeBackgroundMode.MotionBackground,
            DefaultMotionBackgroundVideoPath = TempVideoPath("theme-default.mp4")
        };
        playlist.Designs.Add(theme);
        var song = new SongItem { Title = "Amazing Grace", MotionBackgroundVideoPath = TempVideoPath("song-video.mp4") };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(playlist) { SongId = song.UUID, Design = theme.Id };

        Assert.AreEqual(TempVideoPath("song-video.mp4"), instance.ResolvedMotionBackgroundVideoPath);
    }

    [TestMethod]
    public void ResolvedMotionBackgroundVideoPath_OverrideSet_TakesPriorityOverSongVideoAndThemeDefault()
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme
        {
            BackgroundMode = ThemeBackgroundMode.MotionBackground,
            DefaultMotionBackgroundVideoPath = TempVideoPath("theme-default.mp4")
        };
        playlist.Designs.Add(theme);
        var song = new SongItem { Title = "Amazing Grace", MotionBackgroundVideoPath = TempVideoPath("song-video.mp4") };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(playlist)
        {
            SongId = song.UUID,
            Design = theme.Id,
            MotionBackgroundVideoOverride = TempVideoPath("item-override.mp4")
        };

        Assert.AreEqual(TempVideoPath("item-override.mp4"), instance.ResolvedMotionBackgroundVideoPath);
    }

    [TestMethod]
    public void ResolvedMotionBackgroundVideoPath_ThemeIsPlain_IgnoresThemeDefaultVideo()
    {
        var playlist = new PlaylistInstance();
        var theme = new BaseSlideTheme
        {
            BackgroundMode = ThemeBackgroundMode.Plain,
            DefaultMotionBackgroundVideoPath = TempVideoPath("theme-default.mp4")
        };
        playlist.Designs.Add(theme);
        var instance = new SongItemInstance(playlist) { SongId = Guid.NewGuid(), Design = theme.Id };

        Assert.IsNull(instance.ResolvedMotionBackgroundVideoPath,
            "A Plain-mode theme's DefaultMotionBackgroundVideoPath must never be used, even if set");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests --filter FullyQualifiedName~SongItemInstanceMotionBackgroundResolutionTests`
Expected: build FAILS — `MotionBackgroundVideoOverride`/`ResolvedMotionBackgroundVideoPath` do not exist yet.

- [ ] **Step 3: Implement**

In `SongItemInstance.cs`, immediately after the existing `MotionBackgroundVideoPath` override property (after line 145):

```csharp
        private string? _motionBackgroundVideoOverride;

        // Playlist-item-scoped override of the resolved theme's default motion background video.
        // Unlike MotionBackgroundVideoPath above, this does NOT delegate to ResolvedSong - the same
        // song can have a different override video in different playlists.
        public string? MotionBackgroundVideoOverride
        {
            get => _motionBackgroundVideoOverride;
            set
            {
                this.RaiseAndSetIfChanged(ref _motionBackgroundVideoOverride, value);
                this.RaisePropertyChanged(nameof(ResolvedMotionBackgroundVideoPath));
                this.RaisePropertyChanged(nameof(HasMotionBackground));
            }
        }

        [XmlIgnore]
        public string? ResolvedMotionBackgroundVideoPath
        {
            get
            {
                if (MotionBackgroundService.IsValidVideoFile(MotionBackgroundVideoOverride))
                    return MotionBackgroundVideoOverride;

                if (MotionBackgroundService.IsValidVideoFile(MotionBackgroundVideoPath))
                    return MotionBackgroundVideoPath;

                if (ResolvedDesignTheme?.BackgroundMode == ThemeBackgroundMode.MotionBackground
                    && MotionBackgroundService.IsValidVideoFile(ResolvedDesignTheme.DefaultMotionBackgroundVideoPath))
                    return ResolvedDesignTheme.DefaultMotionBackgroundVideoPath;

                return null;
            }
        }
```

Replace the constructor's `MotionBackgroundVideoPath` subscription (lines 293-311) so it also watches the new override and recomputes the resolved value:

```csharp
            this.WhenAnyValue(x => x.MotionBackgroundVideoPath, x => x.MotionBackgroundVideoOverride,
                    (_, _) => ResolvedMotionBackgroundVideoPath)
                .Scan(
                    new { Previous = (string?)null, Current = (string?)null },
                    (acc, newValue) => new { Previous = acc.Current, Current = newValue })
                .Subscribe(pair =>
                {
                    this.RaisePropertyChanged(nameof(HasMotionBackground));

                    var wasValid = MotionBackgroundService.IsValidVideoFile(pair.Previous);
                    var isValid = MotionBackgroundService.IsValidVideoFile(pair.Current);

                    // When path changes from null/empty to a valid path, regenerate all slide bitmaps
                    if (!wasValid && isValid)
                    {
                        RegenerateAllSlideBitmaps();
                    }
                    // When path changes from valid to null/empty, defer regeneration
                    // until next slide transition (do not regenerate immediately)
                });
```

Replace `HasMotionBackground` (lines 620-623):

```csharp
        [XmlIgnore]
        public bool HasMotionBackground => ResolvedMotionBackgroundVideoPath != null;
```

Note: switching `ResolvedDesignTheme` itself (i.e. reassigning `Design`) does not retrigger this bitmap-regeneration subscription — that mirrors this codebase's existing, pre-existing gap where a reassigned `Theme` on a reused slide instance relies on the separate `Cached == null` sweep (see this project's `CLAUDE.md`), not this video-validity subscription. Out of scope for this feature.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests --filter FullyQualifiedName~SongItemInstanceMotionBackgroundResolutionTests`
Expected: PASS (5/5).

- [ ] **Step 5: Run the full existing SongItemInstance test suite to check for regressions**

Run: `dotnet test HandsLiftedApp.Tests --filter FullyQualifiedName~SongItemInstanceTests`
Expected: PASS (all existing tests still green — `HasMotionBackground`'s behavior for song-own-video-only cases is unchanged).

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/SongItemInstanceMotionBackgroundResolutionTests.cs
git commit -m "feat: resolve motion background video through item override, song video, theme default"
```

---

### Task 3: Serialize/deserialize theme background mode + default video path

**Files:**
- Modify: `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs:43-57` (`SerializePlaylist` Designs copy loop)
- Modify: `HandsLiftedApp.Core/ViewModels/MainViewModel.cs:325-335` (`loadedDesigns` absolute-path resolution)
- Test: `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs` (add methods)

**Interfaces:**
- Consumes: `BaseSlideTheme.BackgroundMode`, `BaseSlideTheme.DefaultMotionBackgroundVideoPath` (Task 1); existing `RelativeFilePathResolver.ToRelativePath`/`ToAbsolutePath`.

- [ ] **Step 1: Write the failing test**

Add to `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs`:

```csharp
    [TestMethod]
    public void SerializePlaylist_ThenDeserialize_RoundTripsThemeBackgroundModeAndDefaultVideoPath()
    {
        var playlist = new PlaylistInstance();
        var videoPath = Path.Combine(_tempDir, "theme-video.mp4");
        var theme = new HandsLiftedApp.Data.SlideTheme.BaseSlideTheme
        {
            BackgroundMode = HandsLiftedApp.Data.SlideTheme.ThemeBackgroundMode.MotionBackground,
            DefaultMotionBackgroundVideoPath = videoPath
        };
        playlist.Designs.Add(theme);

        var path = Path.Combine(_tempDir, "playlist-theme-video.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var rawXml = File.ReadAllText(path);
        Assert.IsFalse(rawXml.Contains(_tempDir),
            "Theme's DefaultMotionBackgroundVideoPath must be written relative to the playlist directory, not absolute.");

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);
        var deserializedTheme = deserialized.Designs.Single(d => d.Id == theme.Id);
        Assert.AreEqual(HandsLiftedApp.Data.SlideTheme.ThemeBackgroundMode.MotionBackground, deserializedTheme.BackgroundMode);
        Assert.AreEqual("theme-video.mp4", deserializedTheme.DefaultMotionBackgroundVideoPath);
    }

    [TestMethod]
    public void SerializePlaylist_ThemeBackgroundModePlain_DefaultVideoPathStaysNull()
    {
        var playlist = new PlaylistInstance();
        var theme = new HandsLiftedApp.Data.SlideTheme.BaseSlideTheme();
        playlist.Designs.Add(theme);

        var path = Path.Combine(_tempDir, "playlist-theme-plain.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);
        var deserializedTheme = deserialized.Designs.Single(d => d.Id == theme.Id);
        Assert.AreEqual(HandsLiftedApp.Data.SlideTheme.ThemeBackgroundMode.Plain, deserializedTheme.BackgroundMode);
        Assert.IsNull(deserializedTheme.DefaultMotionBackgroundVideoPath);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests --filter FullyQualifiedName~SerializePlaylist_ThenDeserialize_RoundTripsThemeBackgroundModeAndDefaultVideoPath`
Expected: FAIL — `DefaultMotionBackgroundVideoPath` is written as an absolute path (not yet relativized), so the "must not contain `_tempDir`" assertion fails.

- [ ] **Step 3: Implement**

In `HandsLiftedDocXmlSerializer.cs`, extend the `Designs` `.Select` block (lines 43-57):

```csharp
                Designs = new ObservableCollection<BaseSlideTheme>(playlist.Designs
                    .Where(d => d.Id != Globals.Instance.AppPreferences?.DefaultTheme?.Id)
                    .Select(design =>
                    {
                        var copy = new BaseSlideTheme();
                        copy.CopyFrom(design);
                        if (copy.BackgroundGraphicFilePath != null &&
                            !copy.BackgroundGraphicFilePath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                        {
                            copy.BackgroundGraphicFilePath =
                                RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath,
                                    copy.BackgroundGraphicFilePath);
                        }
                        if (copy.DefaultMotionBackgroundVideoPath != null &&
                            !copy.DefaultMotionBackgroundVideoPath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                        {
                            copy.DefaultMotionBackgroundVideoPath =
                                RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath,
                                    copy.DefaultMotionBackgroundVideoPath);
                        }
                        return copy;
                    }).ToList()),
```

In `MainViewModel.cs`, extend the `loadedDesigns` `.Select` block (lines 325-335):

```csharp
                var loadedDesigns = x.Designs.Select(design =>
                {
                    if (design.BackgroundGraphicFilePath != null &&
                        !design.BackgroundGraphicFilePath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                    {
                        design.BackgroundGraphicFilePath =
                            RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                                design.BackgroundGraphicFilePath);
                    }
                    if (design.DefaultMotionBackgroundVideoPath != null &&
                        !design.DefaultMotionBackgroundVideoPath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                    {
                        design.DefaultMotionBackgroundVideoPath =
                            RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                                design.DefaultMotionBackgroundVideoPath);
                    }
                    return design;
                });
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests --filter FullyQualifiedName~HandsLiftedDocXmlSerializerTests`
Expected: PASS (all tests in the file, including the 2 new ones).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs HandsLiftedApp.Core/ViewModels/MainViewModel.cs HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs
git commit -m "feat: round-trip theme background mode and default video path through playlist XML"
```

---

### Task 4: Serialize/deserialize per-item motion background video override

**Files:**
- Modify: `HandsLiftedApp.Data/Models/Items/SongItemReference.cs`
- Modify: `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs:116-127` (`SerializeItem` SongItemInstance branch)
- Modify: `HandsLiftedApp.Core/ItemInstanceFactory.cs:25-35` (`ToItemInstance` SongItemReference branch)
- Test: `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs`, `HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs` (add methods)

**Interfaces:**
- Consumes: `SongItemInstance.MotionBackgroundVideoOverride` (Task 2); existing `HandsLiftedDocXmlSerializer.ToRelativePathIfUnderMediaLibrary` (private helper, already in this file); existing `RelativeFilePathResolver.ToAbsoluteMediaPath`.
- Produces: `SongItemReference.MotionBackgroundVideoOverridePath` (`string?`).

- [ ] **Step 1: Write the failing tests**

Add to `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs`:

```csharp
    [TestMethod]
    public void SerializePlaylist_ThenDeserialize_RoundTripsSongItemMotionBackgroundOverride_UnderMediaLibrary()
    {
        Globals.Instance.AppPreferences.MediaLibraryPath = _tempDir;
        var videosDir = Path.Combine(_tempDir, "Videos");
        Directory.CreateDirectory(videosDir);
        var videoFile = Path.Combine(videosDir, "override.mp4");
        File.WriteAllText(videoFile, "video-bytes");

        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var playlist = new PlaylistInstance();
        var instance = new SongItemInstance(playlist)
        {
            SongId = song.UUID,
            MotionBackgroundVideoOverride = videoFile
        };
        playlist.Items.Add(instance);

        var path = Path.Combine(_tempDir, "playlist-song-video-override.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var rawXml = File.ReadAllText(path);
        StringAssert.Contains(rawXml, @"Videos\override.mp4");
        Assert.IsFalse(rawXml.Contains(videoFile),
            "Absolute path must not be written; the override should be relative to the media library.");

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);
        var reference = (SongItemReference)deserialized.Items.Single();
        Assert.AreEqual(@"Videos\override.mp4", reference.MotionBackgroundVideoOverridePath);
    }

    [TestMethod]
    public void SerializePlaylist_SongItemMotionBackgroundOverride_OutsideMediaLibrary_KeepsAbsolutePath()
    {
        var libraryDir = Path.Combine(_tempDir, "Library");
        var outsideDir = Path.Combine(_tempDir, "Outside");
        Directory.CreateDirectory(libraryDir);
        Directory.CreateDirectory(outsideDir);
        Globals.Instance.AppPreferences.MediaLibraryPath = libraryDir;
        var videoFile = Path.Combine(outsideDir, "override.mp4");
        File.WriteAllText(videoFile, "video-bytes");

        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var playlist = new PlaylistInstance();
        var instance = new SongItemInstance(playlist)
        {
            SongId = song.UUID,
            MotionBackgroundVideoOverride = videoFile
        };
        playlist.Items.Add(instance);

        var path = Path.Combine(libraryDir, "playlist-song-video-outside.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var rawXml = File.ReadAllText(path);
        StringAssert.Contains(rawXml, videoFile);

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);
        var reference = (SongItemReference)deserialized.Items.Single();
        Assert.AreEqual(videoFile, reference.MotionBackgroundVideoOverridePath);
    }

    [TestMethod]
    public void SerializePlaylist_SongItemNoMotionBackgroundOverride_StaysNull()
    {
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var playlist = new PlaylistInstance();
        var instance = new SongItemInstance(playlist) { SongId = song.UUID };
        playlist.Items.Add(instance);

        var path = Path.Combine(_tempDir, "playlist-song-no-override.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);
        var reference = (SongItemReference)deserialized.Items.Single();
        Assert.IsNull(reference.MotionBackgroundVideoOverridePath);
    }
```

Add to `HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs`:

```csharp
    [TestMethod]
    public void ToItemInstance_SongItemReference_MotionBackgroundOverride_RelativePathUnderLibrary_ResolvesAgainstMediaLibrary()
    {
        var libraryDir = Path.Combine(_tempDir, "Library");
        var playlistDir = Path.Combine(_tempDir, "Playlist");
        Directory.CreateDirectory(Path.Combine(libraryDir, "Videos"));
        Directory.CreateDirectory(playlistDir);
        var libraryFile = Path.Combine(libraryDir, "Videos", "override.mp4");
        File.WriteAllText(libraryFile, "video-bytes");
        Globals.Instance.AppPreferences.MediaLibraryPath = libraryDir;

        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var reference = new SongItemReference
        {
            SongId = song.UUID,
            MotionBackgroundVideoOverridePath = Path.Combine("Videos", "override.mp4")
        };

        var playlist = new PlaylistInstance { PlaylistWorkingDirectory = playlistDir };
        var instance = (SongItemInstance)ItemInstanceFactory.ToItemInstance(reference, playlist);

        Assert.AreEqual(libraryFile, instance.MotionBackgroundVideoOverride);
    }

    [TestMethod]
    public void ToItemInstance_SongItemReference_NoMotionBackgroundOverride_StaysNull()
    {
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var reference = new SongItemReference { SongId = song.UUID };

        var instance = (SongItemInstance)ItemInstanceFactory.ToItemInstance(reference, null);

        Assert.IsNull(instance.MotionBackgroundVideoOverride);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests --filter FullyQualifiedName~MotionBackgroundOverride`
Expected: build FAILS — `SongItemReference.MotionBackgroundVideoOverridePath` and `SongItemInstance.MotionBackgroundVideoOverride` assignment via the factory branch do not exist yet as wired (the property itself exists from Task 2, but nothing sets it from a `SongItemReference` yet).

- [ ] **Step 3: Implement**

In `SongItemReference.cs`, add alongside `SongId`:

```csharp
        public Guid SongId { get; set; }

        /// <summary>
        /// Playlist-item-scoped override of the resolved theme's default motion background video
        /// for this song item. Null means "no override" - see
        /// SongItemInstance.ResolvedMotionBackgroundVideoPath for the full resolution chain.
        /// </summary>
        public string? MotionBackgroundVideoOverridePath { get; set; }
```

In `HandsLiftedDocXmlSerializer.cs`, replace the `SongItemInstance` branch of `SerializeItem` (lines 116-127):

```csharp
            else if (item is SongItemInstance songItemInstance)
            {
                // SlideTransitionDurationMs is a per-item override on the base Item class
                // (not song content) and, like every other branch here (LogoItem, ScriptureItem,
                // MediaGroupItem, ...), must be carried over explicitly. MotionBackgroundVideoOverride
                // is the same kind of per-item override, scoped to this playlist item rather than the
                // shared library song.
                return new SongItemReference
                {
                    UUID = songItemInstance.UUID,
                    SongId = songItemInstance.SongId,
                    SlideTransitionDurationMs = songItemInstance.SlideTransitionDurationMs,
                    MotionBackgroundVideoOverridePath = ToRelativePathIfUnderMediaLibrary(
                        songItemInstance.MotionBackgroundVideoOverride)
                };
            }
```

In `ItemInstanceFactory.cs`, replace the `SongItemReference` branch of `ToItemInstance` (lines 25-35):

```csharp
            else if (deserializedItem is SongItemReference songReference)
            {
                var instance = new SongItemInstance(playlist)
                {
                    UUID = songReference.UUID,
                    SongId = songReference.SongId,
                    SlideTransitionDurationMs = songReference.SlideTransitionDurationMs,
                    MotionBackgroundVideoOverride = RelativeFilePathResolver.ToAbsoluteMediaPath(
                        mediaLibraryPath, playlistDirectoryPath, songReference.MotionBackgroundVideoOverridePath)
                };
                instance.RaiseForwardedPropertiesChanged();
                return instance;
            }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests --filter FullyQualifiedName~HandsLiftedDocXmlSerializerTests|FullyQualifiedName~ItemInstanceFactoryTests`
Expected: PASS (all tests in both files).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Data/Models/Items/SongItemReference.cs HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs HandsLiftedApp.Core/ItemInstanceFactory.cs HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs
git commit -m "feat: round-trip per-item motion background video override through playlist XML"
```

---

### Task 5: Visibility converters

**Files:**
- Create: `HandsLiftedApp.Core/Converters/IsMotionBackgroundModeConverter.cs`
- Create: `HandsLiftedApp.Core/Converters/IsMotionBackgroundThemeConverter.cs`
- Test: `HandsLiftedApp.Tests/Converters/MotionBackgroundThemeConvertersTests.cs`

**Interfaces:**
- Consumes: `ThemeBackgroundMode` (Task 1), `BaseSlideTheme.BackgroundMode` (Task 1).
- Produces: `IsMotionBackgroundModeConverter` (converts a `ThemeBackgroundMode` value to `bool`, used in `SlideThemeDesigner`); `IsMotionBackgroundThemeConverter` (converts a possibly-null `BaseSlideTheme` to `bool`, used in `ItemEditDockRoot` against `ResolvedDesignTheme`).

- [ ] **Step 1: Write the failing test**

```csharp
// HandsLiftedApp.Tests/Converters/MotionBackgroundThemeConvertersTests.cs
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Converters;

[TestClass]
public class MotionBackgroundThemeConvertersTests
{
    [DataTestMethod]
    [DataRow(ThemeBackgroundMode.Plain, false)]
    [DataRow(ThemeBackgroundMode.MotionBackground, true)]
    public void IsMotionBackgroundModeConverter_MatchesOnlyMotionBackground(ThemeBackgroundMode mode, bool expected)
    {
        var converter = new IsMotionBackgroundModeConverter();

        var result = converter.Convert(mode, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void IsMotionBackgroundThemeConverter_Null_ReturnsFalse()
    {
        var converter = new IsMotionBackgroundThemeConverter();

        var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(false, result);
    }

    [TestMethod]
    public void IsMotionBackgroundThemeConverter_PlainTheme_ReturnsFalse()
    {
        var converter = new IsMotionBackgroundThemeConverter();
        var theme = new BaseSlideTheme { BackgroundMode = ThemeBackgroundMode.Plain };

        var result = converter.Convert(theme, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(false, result);
    }

    [TestMethod]
    public void IsMotionBackgroundThemeConverter_MotionBackgroundTheme_ReturnsTrue()
    {
        var converter = new IsMotionBackgroundThemeConverter();
        var theme = new BaseSlideTheme { BackgroundMode = ThemeBackgroundMode.MotionBackground };

        var result = converter.Convert(theme, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(true, result);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests --filter FullyQualifiedName~MotionBackgroundThemeConvertersTests`
Expected: build FAILS — neither converter class exists yet.

- [ ] **Step 3: Implement**

```csharp
// HandsLiftedApp.Core/Converters/IsMotionBackgroundModeConverter.cs
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class IsMotionBackgroundModeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is ThemeBackgroundMode mode && mode == ThemeBackgroundMode.MotionBackground;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
```

```csharp
// HandsLiftedApp.Core/Converters/IsMotionBackgroundThemeConverter.cs
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    // Converts a (possibly null) BaseSlideTheme - e.g. SongItemInstance.ResolvedDesignTheme - to
    // whether it is a motion-background theme. Used to hide the per-item video override button
    // entirely when no motion-background theme is assigned, rather than showing it disabled.
    public class IsMotionBackgroundThemeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is BaseSlideTheme theme && theme.BackgroundMode == ThemeBackgroundMode.MotionBackground;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests --filter FullyQualifiedName~MotionBackgroundThemeConvertersTests`
Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Converters/IsMotionBackgroundModeConverter.cs HandsLiftedApp.Core/Converters/IsMotionBackgroundThemeConverter.cs HandsLiftedApp.Tests/Converters/MotionBackgroundThemeConvertersTests.cs
git commit -m "feat: add motion-background visibility converters"
```

---

### Task 6: Reusable `MotionBackgroundVideoPicker` control

**Files:**
- Create: `HandsLiftedApp.Core/Controls/MotionBackgroundVideoPicker.axaml`
- Create: `HandsLiftedApp.Core/Controls/MotionBackgroundVideoPicker.axaml.cs`

**Interfaces:**
- Consumes: `Globals.Instance.MainViewModel.ShowOpenFileDialog` (existing `Interaction<FilePickerOpenOptions?, IReadOnlyList<IStorageFile>?>`, handled in `MainWindow.axaml.cs`).
- Produces: `MotionBackgroundVideoPicker.VideoPath` (`string?`, two-way `DirectProperty`) — consumed by Tasks 7 and 8.

There is no existing headless/UI test harness in this codebase for Avalonia `UserControl`s (`TextBoxFilePathPicker` and `SongEditorControl`'s equivalent video picker both have zero automated tests). This control is verified manually as part of Tasks 7 and 8's click-through steps instead.

- [ ] **Step 1: Create the control markup**

```xml
<!-- HandsLiftedApp.Core/Controls/MotionBackgroundVideoPicker.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d" d:DesignWidth="260" d:DesignHeight="40"
             x:CompileBindings="False"
             x:Class="HandsLiftedApp.Core.Controls.MotionBackgroundVideoPicker">
    <DockPanel MinWidth="220">
        <Button Click="ClearButton_OnClick" Content="Clear" DockPanel.Dock="Right" Margin="4,0,0,0" />
        <Button Click="BrowseButton_OnClick" Content="..." DockPanel.Dock="Right" Margin="4,0,0,0" />
        <TextBox x:Name="PathTextBox" IsReadOnly="True" Watermark="No video file selected" />
    </DockPanel>
</UserControl>
```

- [ ] **Step 2: Implement the code-behind**

```csharp
// HandsLiftedApp.Core/Controls/MotionBackgroundVideoPicker.axaml.cs
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Serilog;

namespace HandsLiftedApp.Core.Controls
{
    public partial class MotionBackgroundVideoPicker : UserControl
    {
        public string? VideoPath
        {
            get => _videoPath;
            set => SetAndRaise(VideoPathProperty, ref _videoPath, value);
        }

        private string? _videoPath;

        public static readonly DirectProperty<MotionBackgroundVideoPicker, string?> VideoPathProperty =
            AvaloniaProperty.RegisterDirect<MotionBackgroundVideoPicker, string?>(
                nameof(VideoPath), o => o.VideoPath, (o, v) => o.VideoPath = v,
                null,
                BindingMode.TwoWay
            );

        public MotionBackgroundVideoPicker()
        {
            InitializeComponent();

            this.GetObservable(VideoPathProperty).Subscribe(v => PathTextBox.Text = v);
        }

        private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var videoFileType = new FilePickerFileType("Video Files")
                {
                    Patterns = new[] { "*.mp4", "*.mov", "*.avi", "*.wmv", "*.mkv", "*.webm" }
                };

                // Goes through the MainWindow-handled ShowOpenFileDialog interaction rather than
                // TopLevel.GetTopLevel(this) - this control is used inside nested Flyouts
                // (ItemEditDockRoot, SlideThemeDesigner), where GetTopLevel silently resolves to
                // null (see this project's CLAUDE.md).
                var files = await Globals.Instance.MainViewModel.ShowOpenFileDialog.Handle(
                    new FilePickerOpenOptions
                    {
                        AllowMultiple = false,
                        Title = "Select Motion Background Video",
                        FileTypeFilter = new[] { videoFileType }
                    });

                if (files == null || files.Count == 0) return;

                var path = files[0].TryGetLocalPath();
                if (path != null)
                {
                    VideoPath = path;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error picking motion background video");
            }
        }

        private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
        {
            VideoPath = null;
        }
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build HandsLiftedApp.Core`
Expected: build succeeds, no XAML compilation errors.

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Core/Controls/MotionBackgroundVideoPicker.axaml HandsLiftedApp.Core/Controls/MotionBackgroundVideoPicker.axaml.cs
git commit -m "feat: add reusable motion background video picker control"
```

---

### Task 7: Wire the "Video" button into `ItemEditDockRoot`

**Files:**
- Modify: `HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml:1-136` (xmlns, resources, `SongItemInstance` `DataTemplate`)

**Interfaces:**
- Consumes: `MotionBackgroundVideoPicker` (Task 6), `IsMotionBackgroundThemeConverter` (Task 5), `SongItemInstance.ResolvedDesignTheme` (existing), `SongItemInstance.MotionBackgroundVideoOverride` (Task 2).

- [ ] **Step 1: Add the new xmlns and converter resource**

In `ItemEditDockRoot.axaml`, add to the root `<UserControl ...>` element's attributes (alongside the existing `xmlns:common`, `xmlns:converters`, etc.):

```xml
    xmlns:controls="clr-namespace:HandsLiftedApp.Core.Controls"
```

Add to `<UserControl.Resources>` (alongside the existing 3 converters):

```xml
        <converters:IsMotionBackgroundThemeConverter x:Key="IsMotionBackgroundThemeConverter" />
```

- [ ] **Step 2: Wrap the SongItemInstance template's Edit button and add the new Video button**

Replace the `SongItemInstance` `DataTemplate` body (lines 43-136) so its root becomes a horizontal `StackPanel` holding the existing "Edit" button unchanged, plus a new "Video" button:

```xml
                <DataTemplate x:DataType="items1:SongItemInstance" x:Key="SongItemInstance">
                    <StackPanel Orientation="Horizontal">
                        <Button
                            Background="{DynamicResource EditButtonBackgroundBrush}"
                            BorderBrush="#b3aed9"
                            BorderThickness="1"
                            CornerRadius="4"
                            Margin="6,6,0,6"
                            Padding="22,6">
                            <TextBlock>Edit</TextBlock>
                            <Button.Flyout>
                                <Flyout Placement="BottomEdgeAlignedRight">
                                    <StackPanel Margin="4" MinWidth="200">
                                        <Button Classes="menuRow" Click="EditButton_OnClick">
                                            <DockPanel>
                                                <avalonia:MaterialIcon Kind="Pencil" Margin="0,0,8,0" />
                                                <TextBlock>Edit item</TextBlock>
                                            </DockPanel>
                                        </Button>
                                        <Button Classes="menuRow">
                                            <DockPanel>
                                                <avalonia:MaterialIcon Kind="Palette" Margin="0,0,8,0" />
                                                <TextBlock>Theme</TextBlock>
                                            </DockPanel>
                                            <Button.Flyout>
                                                <Flyout Placement="RightEdgeAlignedTop">
                                                    <DockPanel LastChildFill="True" MinWidth="180">
                                                        <Button
                                                            Click="ClearThemeButton_OnClick"
                                                            DockPanel.Dock="Right"
                                                            Margin="4,0,0,0"
                                                            Padding="6,0"
                                                            ToolTip.Tip="Use fallback theme">
                                                            <TextBlock>✕</TextBlock>
                                                        </Button>
                                                        <ComboBox
                                                            HorizontalAlignment="Stretch"
                                                            ItemsSource="{Binding ParentPlaylist.Designs}"
                                                            SelectedItem="{Binding ResolvedDesignTheme}">
                                                            <ComboBox.Styles>
                                                                <Style Selector="ComboBoxItem" x:DataType="slideTheme:BaseSlideTheme">
                                                                    <Setter Property="IsVisible"
                                                                            Value="{Binding Converter={StaticResource AppliesToSongsVisibilityConverter}}" />
                                                                </Style>
                                                            </ComboBox.Styles>
                                                            <ComboBox.ItemTemplate>
                                                                <DataTemplate>
                                                                    <TextBlock Text="{Binding Name}" />
                                                                </DataTemplate>
                                                            </ComboBox.ItemTemplate>
                                                        </ComboBox>
                                                    </DockPanel>
                                                </Flyout>
                                            </Button.Flyout>
                                        </Button>
                                        <Button Classes="menuRow">
                                            <DockPanel>
                                                <avalonia:MaterialIcon Kind="Blur" Margin="0,0,8,0" />
                                                <TextBlock>Fade transition</TextBlock>
                                            </DockPanel>
                                            <Button.Flyout>
                                                <Flyout Placement="RightEdgeAlignedTop">
                                                    <StackPanel Margin="8" MinWidth="220">
                                                        <DockPanel Margin="0,0,0,6">
                                                            <CheckBox
                                                                DockPanel.Dock="Right"
                                                                HorizontalAlignment="Right"
                                                                IsChecked="{Binding SlideTransitionDurationMs, Mode=OneWay, Converter={x:Static ObjectConverters.IsNotNull}}"
                                                                IsCheckedChanged="FadeOverrideCheckBox_OnIsCheckedChanged" />
                                                            <TextBlock Text="Override fade duration" VerticalAlignment="Center" />
                                                        </DockPanel>
                                                        <DockPanel IsEnabled="{Binding SlideTransitionDurationMs, Converter={x:Static ObjectConverters.IsNotNull}}">
                                                            <TextBlock
                                                                DockPanel.Dock="Right"
                                                                Margin="6,0,0,0"
                                                                Text="{Binding SlideTransitionDurationMs, StringFormat='{}{0:0}ms', TargetNullValue='—'}"
                                                                VerticalAlignment="Center" />
                                                            <Slider
                                                                LargeChange="200"
                                                                Maximum="2000"
                                                                Minimum="0"
                                                                SmallChange="50"
                                                                TickFrequency="100"
                                                                Value="{Binding SlideTransitionDurationMs, TargetNullValue=0}"
                                                                VerticalAlignment="Center" />
                                                        </DockPanel>
                                                    </StackPanel>
                                                </Flyout>
                                            </Button.Flyout>
                                        </Button>
                                    </StackPanel>
                                </Flyout>
                            </Button.Flyout>
                        </Button>
                        <Button
                            Background="{DynamicResource EditButtonBackgroundBrush}"
                            BorderBrush="#b3aed9"
                            BorderThickness="1"
                            CornerRadius="4"
                            IsVisible="{Binding ResolvedDesignTheme, Converter={StaticResource IsMotionBackgroundThemeConverter}}"
                            Margin="6,6,0,6"
                            Padding="22,6"
                            ToolTip.Tip="Motion background video override">
                            <TextBlock>Video</TextBlock>
                            <Button.Flyout>
                                <Flyout Placement="BottomEdgeAlignedRight">
                                    <controls:MotionBackgroundVideoPicker
                                        Margin="8"
                                        VideoPath="{Binding MotionBackgroundVideoOverride}" />
                                </Flyout>
                            </Button.Flyout>
                        </Button>
                    </StackPanel>
                </DataTemplate>
```

No changes are needed in `ItemEditDockRoot.axaml.cs` — the new button opens its `Flyout` automatically on click, the same way the existing "Theme" and "Fade transition" rows do, with no dedicated click handler.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build HandsLiftedApp.Core`
Expected: build succeeds, no XAML compilation errors.

- [ ] **Step 3: Manual click-through verification in a running app**

This class of bug (a control inside a nested `Flyout` silently failing to open a dialog) produces no compiler error, no test failure, and no exception — per this project's `CLAUDE.md`, it must be verified by actually clicking through it. Run the app and, for a song item in a playlist:
1. Assign a Plain-mode theme (or no theme) — confirm the "Video" button is completely absent (not just disabled) next to "Edit".
2. Assign a MotionBackground-mode theme with a default video set — confirm the "Video" button appears.
3. Click "Video", click "..." (Browse) — confirm the native file picker dialog actually opens (this is the nested-Flyout `ShowOpenFileDialog` interaction path from Task 6) and selecting a video file updates the path display.
4. Click "Clear" — confirm the path display clears.

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml
git commit -m "feat: add per-item motion background video override button to ItemEditDockRoot"
```

---

### Task 8: Wire background mode + default video picker into `SlideThemeDesigner`

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml:1-33` (xmlns, resources), `:285-289` (theme editor panel, near "Applies To")
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs:104-133` (constructor)

**Interfaces:**
- Consumes: `MotionBackgroundVideoPicker` (Task 6), `IsMotionBackgroundModeConverter` (Task 5), `BaseSlideTheme.BackgroundMode`/`DefaultMotionBackgroundVideoPath` (Task 1).

- [ ] **Step 1: Add the new xmlns and converter resource**

In `SlideThemeDesigner.axaml`, add a new xmlns to the root `<UserControl ...>` element (the existing `controls:` prefix already points at the separate `HandsLiftedApp.Controls` assembly, so this new control needs its own prefix):

```xml
             xmlns:coreControls="clr-namespace:HandsLiftedApp.Core.Controls"
```

Add to `<UserControl.Resources>` (alongside the existing `converters2:` entries):

```xml
        <converters2:IsMotionBackgroundModeConverter x:Key="IsMotionBackgroundModeConverter" />
```

- [ ] **Step 2: Add the Background Mode control and video picker to the theme editor panel**

Insert immediately after the existing "Applies To" block (after line 288, before the Font/Font Weight `Grid`):

```xml
                    <TextBlock Text="Background" Margin="0 6 0 0" />
                    <controls:ComboBoxWithoutWheelScroll x:Name="backgroundModeComboBox"
                                                         HorizontalAlignment="Stretch"
                                                         SelectedItem="{Binding BackgroundMode}" />

                    <StackPanel IsVisible="{Binding BackgroundMode, Converter={StaticResource IsMotionBackgroundModeConverter}}"
                                Margin="0 6 0 0">
                        <TextBlock Text="Default Motion Background Video" />
                        <coreControls:MotionBackgroundVideoPicker VideoPath="{Binding DefaultMotionBackgroundVideoPath}" />
                    </StackPanel>
```

- [ ] **Step 3: Populate the new combo box's items**

In `SlideThemeDesigner.axaml.cs`, add immediately after the existing `themeTypeComboBox.ItemsSource = Enum.GetValues<SlideThemeType>();` (line 117):

```csharp
            backgroundModeComboBox.ItemsSource = Enum.GetValues<ThemeBackgroundMode>();
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build HandsLiftedApp.Core`
Expected: build succeeds, no XAML compilation errors.

- [ ] **Step 5: Manual click-through verification in a running app**

Same rationale as Task 7 Step 3 — this control also sits inside `SlideThemeDesigner`'s panel, exercising the same `ShowOpenFileDialog` interaction path. Run the app, open the Slide Theme Designer, select a Song Theme:
1. Confirm "Background" defaults to "Plain" and the video picker is hidden.
2. Switch it to "MotionBackground" — confirm the video picker appears.
3. Click "..." (Browse) — confirm the native file picker opens and selecting a video updates the path display.
4. Click "Clear" — confirm the path display clears.
5. Switch to a different theme in the list and back — confirm the Background Mode and video path shown reflect the selected theme (via `SyncEditorToSelection`'s existing `themeEditorPanel.DataContext` reassignment), not the previous theme's values.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs
git commit -m "feat: add background mode and default video picker to SlideThemeDesigner"
```

---

### Task 9: Full test suite regression check

**Files:** none (verification-only task)

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test HandsLiftedApp.Tests`
Expected: PASS — all pre-existing tests plus every test added in Tasks 1-5 are green, with no regressions in `SongItemInstanceTests`, `HandsLiftedDocXmlSerializerTests`, or `ItemInstanceFactoryTests`.

- [ ] **Step 2: Full build**

Run: `dotnet build`
Expected: the whole solution builds cleanly (this catches any XAML/codegen issue in `HandsLiftedApp.Core` that a scoped `dotnet build HandsLiftedApp.Core` in Tasks 6-8 might not surface against dependent projects).
