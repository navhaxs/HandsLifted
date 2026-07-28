# Playlist-Scoped Default Themes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single app-wide default theme with three playlist-scoped defaults (songs without motion bg, songs with motion bg, scripture), stored on the playlist and saved/loaded with it.

**Architecture:** Add three nullable `Guid` fields to `HandsLiftedApp.Data.Models.Playlist` (inherited automatically by the runtime `PlaylistInstance` since it subclasses `Playlist`). Add resolver methods (`ResolveSongTheme`, `ResolveScriptureTheme`) and a change-notification observable (`DefaultThemeAssignmentsChanged`) to `PlaylistInstance`. Route the four existing theme-fallback call sites (`SongSlideInstance`, `SongTitleSlideInstance`, `ScriptureItemInstance`) through these resolvers instead of the legacy `AppPreferences.DefaultTheme` fallback directly. Extend the existing `SlideThemeDesigner` theme list with a right-click "set as default" context menu.

**Tech Stack:** C# / Avalonia 11 / ReactiveUI / XmlSerializer (see `HandsLiftedDocXmlSerializer`).

## Global Constraints

- Fallback chain (from the approved spec): explicit per-item `Design` override → playlist category default → legacy app-wide `AppPreferences.DefaultTheme` → `new BaseSlideTheme()`. The legacy app default is a **permanent** final fallback, not just a one-time seed.
- Old playlist files load with all three new fields `null` — behavior must be byte-for-byte identical to today until the user explicitly sets a playlist-level default. No migration step.
- ReactiveUI's no-selector `WhenAnyValue` caps at 7 properties in this codebase's pinned version (20.1.1) — never exceed that without an explicit selector.
- Never run `find`/`grep` via Bash/PowerShell in this repo — use the Glob/Grep tools.

---

### Task 1: Add playlist-level default theme fields + save/round-trip through XML

**Files:**
- Modify: `HandsLiftedApp.Data/Models/Playlist.cs:56-57`
- Modify: `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs:33-56` (the `playlistSerialized` initializer inside `SerializePlaylist`)
- Test: `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs`

**Interfaces:**
- Produces: `Playlist.DefaultSongThemeId`, `Playlist.DefaultSongMotionThemeId`, `Playlist.DefaultScriptureThemeId` (all `Guid?`, ReactiveObject properties). These are inherited as-is by `PlaylistInstance` (which subclasses `Playlist`) — later tasks read/write them directly on `PlaylistInstance`.

- [ ] **Step 1: Write the failing round-trip tests**

Add to `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs` (inside the existing `HandsLiftedDocXmlSerializerTests` class, after `SerializePlaylist_ThenDeserialize_RoundTripsScriptureItem`):

```csharp
[TestMethod]
public void SerializePlaylist_ThenDeserialize_RoundTripsDefaultThemeIds()
{
    var songThemeId = Guid.NewGuid();
    var songMotionThemeId = Guid.NewGuid();
    var scriptureThemeId = Guid.NewGuid();
    var playlist = new PlaylistInstance
    {
        DefaultSongThemeId = songThemeId,
        DefaultSongMotionThemeId = songMotionThemeId,
        DefaultScriptureThemeId = scriptureThemeId
    };

    var path = Path.Combine(_tempDir, "playlist-defaults.xml");
    HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

    var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

    Assert.AreEqual(songThemeId, deserialized.DefaultSongThemeId);
    Assert.AreEqual(songMotionThemeId, deserialized.DefaultSongMotionThemeId);
    Assert.AreEqual(scriptureThemeId, deserialized.DefaultScriptureThemeId);
}

[TestMethod]
public void SerializePlaylist_DefaultThemeIdsUnset_RoundTripAsNull()
{
    var playlist = new PlaylistInstance();

    var path = Path.Combine(_tempDir, "playlist-no-defaults.xml");
    HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

    var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);

    Assert.IsNull(deserialized.DefaultSongThemeId);
    Assert.IsNull(deserialized.DefaultSongMotionThemeId);
    Assert.IsNull(deserialized.DefaultScriptureThemeId);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~HandsLiftedDocXmlSerializerTests"`
Expected: FAIL — `Playlist` has no `DefaultSongThemeId`/`DefaultSongMotionThemeId`/`DefaultScriptureThemeId` members (compile error).

- [ ] **Step 3: Add the three fields to `Playlist`**

In `HandsLiftedApp.Data/Models/Playlist.cs`, immediately after the existing `Designs` property (line 57):

```csharp
        private Guid? _defaultSongThemeId;
        public Guid? DefaultSongThemeId { get => _defaultSongThemeId; set => this.RaiseAndSetIfChanged(ref _defaultSongThemeId, value); }

        private Guid? _defaultSongMotionThemeId;
        public Guid? DefaultSongMotionThemeId { get => _defaultSongMotionThemeId; set => this.RaiseAndSetIfChanged(ref _defaultSongMotionThemeId, value); }

        private Guid? _defaultScriptureThemeId;
        public Guid? DefaultScriptureThemeId { get => _defaultScriptureThemeId; set => this.RaiseAndSetIfChanged(ref _defaultScriptureThemeId, value); }
```

- [ ] **Step 4: Wire the fields into `SerializePlaylist`**

In `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs`, in the `playlistSerialized` object initializer, add three lines right after `SlideTransitionDurationMs = playlist.SlideTransitionDurationMs,`:

```csharp
                SlideTransitionDurationMs = playlist.SlideTransitionDurationMs,
                DefaultSongThemeId = playlist.DefaultSongThemeId,
                DefaultSongMotionThemeId = playlist.DefaultSongMotionThemeId,
                DefaultScriptureThemeId = playlist.DefaultScriptureThemeId,
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~HandsLiftedDocXmlSerializerTests"`
Expected: PASS (all tests in the file, including the two new ones and the pre-existing `RoundTripsScriptureItem` test).

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Data/Models/Playlist.cs HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs
git commit -m "feat: add playlist-scoped default theme ids to Playlist model"
```

---

### Task 2: Load default theme ids into the live playlist on playlist load

**Files:**
- Modify: `HandsLiftedApp.Core/ViewModels/MainViewModel.cs:172-176` (inside the `LoadPlaylistAction` subscription)

**Interfaces:**
- Consumes: `Playlist.DefaultSongThemeId`/`DefaultSongMotionThemeId`/`DefaultScriptureThemeId` from Task 1.

This handler is an inline lambda subscribed to `MessageBus`, constructed as part of `MainViewModel`'s non-design-mode constructor (which opens a JSON config file and wires up dispatcher-bound UI state) — it cannot be exercised by a lightweight unit test the way Task 1's serializer round-trip could. Verification here is a manual smoke check (see Step 3).

- [ ] **Step 1: Copy the three fields from the deserialized document onto the live `Playlist`**

In `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`, inside the `LoadPlaylistAction` handler, change:

```csharp
                Playlist.Title = x.Title;
                Playlist.SlideTransitionDurationMs = x.SlideTransitionDurationMs;
                Playlist.Meta = x.Meta;
                
                Playlist.LogoGraphicFile =
```

to:

```csharp
                Playlist.Title = x.Title;
                Playlist.SlideTransitionDurationMs = x.SlideTransitionDurationMs;
                Playlist.Meta = x.Meta;
                Playlist.DefaultSongThemeId = x.DefaultSongThemeId;
                Playlist.DefaultSongMotionThemeId = x.DefaultSongMotionThemeId;
                Playlist.DefaultScriptureThemeId = x.DefaultScriptureThemeId;
                
                Playlist.LogoGraphicFile =
```

- [ ] **Step 2: Build the solution**

Run: `dotnet build HandsLiftedApp.sln`
Expected: Build succeeds with no new errors/warnings.

- [ ] **Step 3: Manual verification (no automated test — see rationale above)**

Run the app, open Setup/theme designer (once Task 7 lands, this step can also confirm the UI), save a playlist, reload it, and confirm the playlist round-trips without error. Until Task 7 lands, it's enough to confirm the app starts and loads an existing playlist file without exceptions (nothing observable changes yet since nothing sets these fields from the UI before Task 7).

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Core/ViewModels/MainViewModel.cs
git commit -m "feat: load playlist-scoped default theme ids on playlist load"
```

---

### Task 3: `PlaylistInstance` theme resolver methods + change notification

**Files:**
- Modify: `HandsLiftedApp.Core/Models/PlaylistInstance.cs`
- Test: Create `HandsLiftedApp.Tests/Models/PlaylistInstanceTests.cs`

**Interfaces:**
- Consumes: `Playlist.Designs` (`ObservableCollection<BaseSlideTheme>`), `Playlist.DefaultSongThemeId`/`DefaultSongMotionThemeId`/`DefaultScriptureThemeId` (`Guid?`) from Task 1.
- Produces:
  - `PlaylistInstance.ResolveSongTheme(Guid explicitDesignId, bool hasMotionBackground) : BaseSlideTheme` — never returns null.
  - `PlaylistInstance.ResolveScriptureTheme(Guid explicitDesignId) : BaseSlideTheme` — never returns null.
  - `PlaylistInstance.DefaultThemeAssignmentsChanged : IObservable<System.Reactive.Unit>` — fires whenever any of the three default-id fields changes (does not fire on subscribe).

- [ ] **Step 1: Write the failing tests**

Create `HandsLiftedApp.Tests/Models/PlaylistInstanceTests.cs`:

```csharp
using System;
using System.Reactive;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Models;

[TestClass]
public class PlaylistInstanceTests
{
    [TestInitialize]
    public void Setup()
    {
        // ResolveSongTheme/ResolveScriptureTheme fall back to Globals.Instance.AppPreferences,
        // which is null unless Globals.OnStartup() has run. Matches the convention already used
        // by ScriptureItemInstanceTests.Setup().
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    private static BaseSlideTheme MakeTheme(string name) => new BaseSlideTheme { Name = name };

    [TestMethod]
    public void ResolveSongTheme_ExplicitDesignPresent_ReturnsExplicitTheme()
    {
        var playlist = new PlaylistInstance();
        var explicitTheme = MakeTheme("Explicit");
        var songDefault = MakeTheme("SongDefault");
        playlist.Designs.Add(explicitTheme);
        playlist.Designs.Add(songDefault);
        playlist.DefaultSongThemeId = songDefault.Id;

        var result = playlist.ResolveSongTheme(explicitTheme.Id, hasMotionBackground: false);

        Assert.AreSame(explicitTheme, result);
    }

    [TestMethod]
    public void ResolveSongTheme_ExplicitDesignMissingFromDesigns_FallsBackToCategoryDefault()
    {
        var playlist = new PlaylistInstance();
        var songDefault = MakeTheme("SongDefault");
        playlist.Designs.Add(songDefault);
        playlist.DefaultSongThemeId = songDefault.Id;

        // explicitDesignId points at a theme that no longer exists in Designs (e.g. deleted)
        var result = playlist.ResolveSongTheme(Guid.NewGuid(), hasMotionBackground: false);

        Assert.AreSame(songDefault, result);
    }

    [TestMethod]
    public void ResolveSongTheme_NoExplicit_NoMotionBackground_ReturnsSongDefault()
    {
        var playlist = new PlaylistInstance();
        var songDefault = MakeTheme("SongDefault");
        var motionDefault = MakeTheme("MotionDefault");
        playlist.Designs.Add(songDefault);
        playlist.Designs.Add(motionDefault);
        playlist.DefaultSongThemeId = songDefault.Id;
        playlist.DefaultSongMotionThemeId = motionDefault.Id;

        var result = playlist.ResolveSongTheme(Guid.Empty, hasMotionBackground: false);

        Assert.AreSame(songDefault, result);
    }

    [TestMethod]
    public void ResolveSongTheme_NoExplicit_WithMotionBackground_ReturnsMotionDefault()
    {
        var playlist = new PlaylistInstance();
        var songDefault = MakeTheme("SongDefault");
        var motionDefault = MakeTheme("MotionDefault");
        playlist.Designs.Add(songDefault);
        playlist.Designs.Add(motionDefault);
        playlist.DefaultSongThemeId = songDefault.Id;
        playlist.DefaultSongMotionThemeId = motionDefault.Id;

        var result = playlist.ResolveSongTheme(Guid.Empty, hasMotionBackground: true);

        Assert.AreSame(motionDefault, result);
    }

    [TestMethod]
    public void ResolveSongTheme_CategoryDefaultUnset_FallsBackToAppDefault()
    {
        var playlist = new PlaylistInstance();

        var result = playlist.ResolveSongTheme(Guid.Empty, hasMotionBackground: false);

        Assert.AreSame(Globals.Instance.AppPreferences.DefaultTheme, result);
    }

    [TestMethod]
    public void ResolveSongTheme_CategoryDefaultPointsToDeletedTheme_FallsBackToAppDefault()
    {
        var playlist = new PlaylistInstance();
        playlist.DefaultSongThemeId = Guid.NewGuid(); // not present in Designs

        var result = playlist.ResolveSongTheme(Guid.Empty, hasMotionBackground: false);

        Assert.AreSame(Globals.Instance.AppPreferences.DefaultTheme, result);
    }

    [TestMethod]
    public void ResolveScriptureTheme_ExplicitDesignPresent_ReturnsExplicitTheme()
    {
        var playlist = new PlaylistInstance();
        var explicitTheme = MakeTheme("Explicit");
        var scriptureDefault = MakeTheme("ScriptureDefault");
        playlist.Designs.Add(explicitTheme);
        playlist.Designs.Add(scriptureDefault);
        playlist.DefaultScriptureThemeId = scriptureDefault.Id;

        var result = playlist.ResolveScriptureTheme(explicitTheme.Id);

        Assert.AreSame(explicitTheme, result);
    }

    [TestMethod]
    public void ResolveScriptureTheme_NoExplicit_ReturnsScriptureDefault()
    {
        var playlist = new PlaylistInstance();
        var scriptureDefault = MakeTheme("ScriptureDefault");
        playlist.Designs.Add(scriptureDefault);
        playlist.DefaultScriptureThemeId = scriptureDefault.Id;

        var result = playlist.ResolveScriptureTheme(Guid.Empty);

        Assert.AreSame(scriptureDefault, result);
    }

    [TestMethod]
    public void ResolveScriptureTheme_DefaultUnset_FallsBackToAppDefault()
    {
        var playlist = new PlaylistInstance();

        var result = playlist.ResolveScriptureTheme(Guid.Empty);

        Assert.AreSame(Globals.Instance.AppPreferences.DefaultTheme, result);
    }

    [TestMethod]
    public void DefaultThemeAssignmentsChanged_FiresWhenAnyOfTheThreeIdsChange()
    {
        var playlist = new PlaylistInstance();
        var fireCount = 0;
        playlist.DefaultThemeAssignmentsChanged.Subscribe(_ => fireCount++);

        playlist.DefaultSongThemeId = Guid.NewGuid();
        playlist.DefaultSongMotionThemeId = Guid.NewGuid();
        playlist.DefaultScriptureThemeId = Guid.NewGuid();

        Assert.AreEqual(3, fireCount);
    }

    [TestMethod]
    public void DefaultThemeAssignmentsChanged_DoesNotFireOnSubscribe()
    {
        var playlist = new PlaylistInstance();
        var fireCount = 0;
        playlist.DefaultThemeAssignmentsChanged.Subscribe(_ => fireCount++);

        Assert.AreEqual(0, fireCount);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~PlaylistInstanceTests"`
Expected: FAIL — `PlaylistInstance` has no `ResolveSongTheme`/`ResolveScriptureTheme`/`DefaultThemeAssignmentsChanged` members (compile error).

- [ ] **Step 3: Add `using System.Reactive;` to `PlaylistInstance.cs`**

`HandsLiftedApp.Core/Models/PlaylistInstance.cs` currently imports `System.Reactive.Linq` but not the bare `System.Reactive` namespace (needed for `Unit`). Add it to the using block at the top of the file, alongside the existing `System.Reactive.Linq` line.

- [ ] **Step 4: Add `DefaultThemeAssignmentsChanged` construction to the constructor**

In `HandsLiftedApp.Core/Models/PlaylistInstance.cs`, immediately after the existing `_nextSlide = ...ToProperty(this, x => x.NextSlide);` block and before the `if (Design.IsDesignMode) { return; }` check, add:

```csharp
            DefaultThemeAssignmentsChanged = this.WhenAnyValue(
                    p => p.DefaultSongThemeId,
                    p => p.DefaultSongMotionThemeId,
                    p => p.DefaultScriptureThemeId)
                .Skip(1)
                .Select(_ => Unit.Default);
```

Add the backing property declaration near the other public properties (e.g. just above `public void UpdateIndexes()`):

```csharp
        public IObservable<Unit> DefaultThemeAssignmentsChanged { get; }
```

- [ ] **Step 5: Add the two resolver methods**

In `HandsLiftedApp.Core/Models/PlaylistInstance.cs`, add near `UpdateIndexes()`:

```csharp
        public BaseSlideTheme ResolveSongTheme(Guid explicitDesignId, bool hasMotionBackground)
        {
            if (explicitDesignId != Guid.Empty)
            {
                var explicitTheme = Designs.FirstOrDefault(d => d.Id == explicitDesignId);
                if (explicitTheme != null) return explicitTheme;
            }

            var defaultId = hasMotionBackground ? DefaultSongMotionThemeId : DefaultSongThemeId;
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

            var byDefault = DefaultScriptureThemeId.HasValue
                ? Designs.FirstOrDefault(d => d.Id == DefaultScriptureThemeId.Value)
                : null;
            return byDefault ?? Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
        }
```

`BaseSlideTheme` is already imported in this file via `HandsLiftedApp.Data.SlideTheme`.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~PlaylistInstanceTests"`
Expected: PASS (all 11 tests).

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/Models/PlaylistInstance.cs HandsLiftedApp.Tests/Models/PlaylistInstanceTests.cs
git commit -m "feat: add PlaylistInstance theme resolvers for song/scripture defaults"
```

---

### Task 4: Route `SongSlideInstance` theme resolution through the playlist resolver

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongSlideInstance.cs`

**Interfaces:**
- Consumes: `PlaylistInstance.ResolveSongTheme(Guid, bool)` and `PlaylistInstance.DefaultThemeAssignmentsChanged` from Task 3. `SongItemInstance.HasMotionBackground : bool` (pre-existing).

No new automated test: `ResolveTheme` reads `Globals.Instance.MainViewModel?.Playlist`, a process-global singleton that's expensive/fragile to construct in a unit-test host (touches `ConfigurationBuilder` and a JSON config file) — this is a pre-existing gap (the current `SongSlideSpecBuilderTests` never exercises the `Playlist`-lookup branch either, only the `Guid.Empty` → app-default fallback branch). Task 3's `PlaylistInstance.ResolveSongTheme` unit tests already cover the resolution *logic* directly; this task is thin glue code on top of it. Verify via the regression run in Step 3.

- [ ] **Step 1: Update `ResolveTheme` to delegate to the playlist resolver**

In `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongSlideInstance.cs`, replace:

```csharp
        private static BaseSlideTheme ResolveTheme(Guid designId)
        {
            if (designId != Guid.Empty)
            {
                var theme = Globals.Instance.MainViewModel?.Playlist?.Designs
                    .FirstOrDefault(d => d.Id == designId);
                if (theme != null) return theme;
            }
            return Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
        }
```

with:

```csharp
        private static BaseSlideTheme ResolveTheme(Guid designId, bool hasMotionBackground)
        {
            return Globals.Instance.MainViewModel?.Playlist?.ResolveSongTheme(designId, hasMotionBackground)
                   ?? Globals.Instance.AppPreferences?.DefaultTheme
                   ?? new BaseSlideTheme();
        }
```

- [ ] **Step 2: Update the constructor to pass motion-background state and re-resolve on the two new triggers**

Replace:

```csharp
            Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty);

            parentSongItem?.WhenAnyValue(x => x.Design)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(designId =>
                {
                    Theme = ResolveTheme(designId);
                    RequestRender();
                });
```

with:

```csharp
            Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty, parentSongItem?.HasMotionBackground ?? false);

            parentSongItem?.WhenAnyValue(x => x.Design)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(designId =>
                {
                    Theme = ResolveTheme(designId, parentSongItem?.HasMotionBackground ?? false);
                    RequestRender();
                });

            // Motion background presence flips which playlist default applies when Design is
            // unset (Guid.Empty), so this must re-resolve Theme, not just re-render.
            parentSongItem?.WhenAnyValue(x => x.MotionBackgroundVideoPath)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty, parentSongItem?.HasMotionBackground ?? false);
                    RequestRender();
                });

            // If Design is unset, this slide is riding whichever playlist default applies -
            // re-resolve whenever the user repoints one of the three playlist defaults.
            Globals.Instance.MainViewModel?.Playlist?.DefaultThemeAssignmentsChanged
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    if ((parentSongItem?.Design ?? Guid.Empty) == Guid.Empty)
                    {
                        Theme = ResolveTheme(Guid.Empty, parentSongItem?.HasMotionBackground ?? false);
                        RequestRender();
                    }
                });
```

- [ ] **Step 3: Run the existing regression suite**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SongSlideSpecBuilderTests"`
Expected: PASS — no behavior change for these tests (they never set `Design` away from empty and don't set up `Globals.Instance.MainViewModel`, so they exercise the same app-default fallback as before).

- [ ] **Step 4: Build the solution**

Run: `dotnet build HandsLiftedApp.sln`
Expected: Build succeeds (confirms no other caller of the now-two-argument `ResolveTheme` was missed — it's `private static`, so all call sites are in this same file).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Slides/SongSlideInstance.cs
git commit -m "feat: resolve SongSlideInstance theme via playlist song/motion defaults"
```

---

### Task 5: Route `SongTitleSlideInstance` theme resolution through the playlist resolver

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs`

**Interfaces:**
- Consumes: same as Task 4.

Same rationale as Task 4 for no new automated test — this is the song-title-slide twin of that same wiring, and it already has an existing `MotionBackgroundVideoPath` subscription (currently render-only) that must be extended to also re-resolve `Theme`.

- [ ] **Step 1: Update `ResolveTheme`**

In `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs`, replace:

```csharp
        private static BaseSlideTheme ResolveTheme(Guid designId)
        {
            if (designId != Guid.Empty)
            {
                var theme = Globals.Instance.MainViewModel?.Playlist?.Designs
                    .FirstOrDefault(d => d.Id == designId);
                if (theme != null) return theme;
            }
            return Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
        }
```

with:

```csharp
        private static BaseSlideTheme ResolveTheme(Guid designId, bool hasMotionBackground)
        {
            return Globals.Instance.MainViewModel?.Playlist?.ResolveSongTheme(designId, hasMotionBackground)
                   ?? Globals.Instance.AppPreferences?.DefaultTheme
                   ?? new BaseSlideTheme();
        }
```

- [ ] **Step 2: Update the constructor**

Replace:

```csharp
            Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty);

            parentSongItem?.WhenAnyValue(x => x.Design)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(designId =>
                {
                    Theme = ResolveTheme(designId);
                    RequestRender();
                });

            parentSongItem?.WhenAnyValue(x => x.MotionBackgroundVideoPath)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => RequestRender());
```

with:

```csharp
            Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty, parentSongItem?.HasMotionBackground ?? false);

            parentSongItem?.WhenAnyValue(x => x.Design)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(designId =>
                {
                    Theme = ResolveTheme(designId, parentSongItem?.HasMotionBackground ?? false);
                    RequestRender();
                });

            // Motion background presence flips which playlist default applies when Design is
            // unset (Guid.Empty), so this must re-resolve Theme, not just re-render (this
            // subscription previously only re-rendered).
            parentSongItem?.WhenAnyValue(x => x.MotionBackgroundVideoPath)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty, parentSongItem?.HasMotionBackground ?? false);
                    RequestRender();
                });

            // If Design is unset, this slide is riding whichever playlist default applies -
            // re-resolve whenever the user repoints one of the three playlist defaults.
            Globals.Instance.MainViewModel?.Playlist?.DefaultThemeAssignmentsChanged
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    if ((parentSongItem?.Design ?? Guid.Empty) == Guid.Empty)
                    {
                        Theme = ResolveTheme(Guid.Empty, parentSongItem?.HasMotionBackground ?? false);
                        RequestRender();
                    }
                });
```

- [ ] **Step 3: Build the solution**

Run: `dotnet build HandsLiftedApp.sln`
Expected: Build succeeds.

- [ ] **Step 4: Run the full test suite for a regression check**

Run: `dotnet test HandsLiftedApp.Tests`
Expected: PASS (all pre-existing tests still pass; `SongTitleSlideInstance` has no dedicated test file today).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs
git commit -m "feat: resolve SongTitleSlideInstance theme via playlist song/motion defaults"
```

---

### Task 6: Route `ScriptureItemInstance.ResolvedDesignTheme` through the playlist resolver

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs`

**Interfaces:**
- Consumes: `PlaylistInstance.ResolveScriptureTheme(Guid)` and `PlaylistInstance.DefaultThemeAssignmentsChanged` from Task 3.

Unlike `SongSlideInstance`/`SongTitleSlideInstance`, `ScriptureItemInstance.ResolvedDesignTheme` already reads its **own** `ParentPlaylist` rather than the global `Globals.Instance.MainViewModel.Playlist` — this makes it directly unit-testable by constructing a `PlaylistInstance` and passing it in, matching the existing `SerializePlaylist_ThenDeserialize_RoundTripsScriptureItem` test's pattern.

- [ ] **Step 1: Write the failing tests**

In `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs`, add these usings to the top of the file:

```csharp
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Data.SlideTheme;
```

Then add these test methods (e.g. after the existing `ResolvedDesignTheme_DesignEmpty_FallsBackToDefaultTheme` test):

```csharp
    [TestMethod]
    public void ResolvedDesignTheme_PlaylistScriptureDefaultSet_UsesPlaylistDefault()
    {
        var playlist = new PlaylistInstance();
        var scriptureTheme = new BaseSlideTheme { Name = "Scripture Theme" };
        playlist.Designs.Add(scriptureTheme);
        playlist.DefaultScriptureThemeId = scriptureTheme.Id;

        var instance = new ScriptureItemInstance(playlist, MakeEmptyStore());

        Assert.AreSame(scriptureTheme, instance.ResolvedDesignTheme);
    }

    [TestMethod]
    public void ResolvedDesignTheme_ExplicitDesignOverridesPlaylistScriptureDefault()
    {
        var playlist = new PlaylistInstance();
        var scriptureDefaultTheme = new BaseSlideTheme { Name = "Scripture Default" };
        var explicitTheme = new BaseSlideTheme { Name = "Explicit" };
        playlist.Designs.Add(scriptureDefaultTheme);
        playlist.Designs.Add(explicitTheme);
        playlist.DefaultScriptureThemeId = scriptureDefaultTheme.Id;

        var instance = new ScriptureItemInstance(playlist, MakeEmptyStore())
        {
            Design = explicitTheme.Id
        };

        Assert.AreSame(explicitTheme, instance.ResolvedDesignTheme);
    }

    [TestMethod]
    public void ResolvedDesignTheme_PlaylistScriptureDefaultUnset_FallsBackToAppDefault()
    {
        var playlist = new PlaylistInstance();
        var instance = new ScriptureItemInstance(playlist, MakeEmptyStore());

        Assert.AreSame(Globals.Instance.AppPreferences.DefaultTheme, instance.ResolvedDesignTheme);
    }

    [TestMethod]
    public void ResolvedDesignTheme_RaisesPropertyChanged_WhenPlaylistScriptureDefaultChanges()
    {
        var playlist = new PlaylistInstance();
        var scriptureTheme = new BaseSlideTheme { Name = "Scripture Theme" };
        playlist.Designs.Add(scriptureTheme);

        var instance = new ScriptureItemInstance(playlist, MakeEmptyStore());
        var raised = false;
        instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ScriptureItemInstance.ResolvedDesignTheme)) raised = true;
        };

        playlist.DefaultScriptureThemeId = scriptureTheme.Id;

        Assert.IsTrue(raised);
        Assert.AreSame(scriptureTheme, instance.ResolvedDesignTheme);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~ScriptureItemInstanceTests"`
Expected: FAIL — `PlaylistInstance` has no `ResolveScriptureTheme`/`DefaultThemeAssignmentsChanged` yet from this test's point of view if run standalone; if Task 3 already landed, expect FAIL instead on the new assertions (theme not yet resolved through the playlist).

- [ ] **Step 3: Update `ResolvedDesignTheme` and its change notification**

In `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`, replace:

```csharp
            this.WhenAnyValue(x => x.Design)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(ResolvedDesignTheme)));
```

with:

```csharp
            this.WhenAnyValue(x => x.Design)
                .Select(_ => Unit.Default)
                .Merge(ParentPlaylist?.DefaultThemeAssignmentsChanged ?? Observable.Never<Unit>())
                .Subscribe(_ => this.RaisePropertyChanged(nameof(ResolvedDesignTheme)));
```

and replace:

```csharp
        public BaseSlideTheme? ResolvedDesignTheme
        {
            get => ParentPlaylist?.Designs.FirstOrDefault(d => d.Id == Design)
                   ?? Globals.Instance.AppPreferences?.DefaultTheme;
            set
```

with:

```csharp
        public BaseSlideTheme? ResolvedDesignTheme
        {
            get => ParentPlaylist?.ResolveScriptureTheme(Design)
                   ?? Globals.Instance.AppPreferences?.DefaultTheme;
            set
```

(`System.Reactive` and `System.Reactive.Linq` are already imported at the top of this file.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~ScriptureItemInstanceTests"`
Expected: PASS (all tests in the file, including the pre-existing `ResolvedDesignTheme_DesignEmpty_FallsBackToDefaultTheme`, which still passes unmodified since `ParentPlaylist` is `null` there and `null?.ResolveScriptureTheme(...)` short-circuits to the app default exactly as before).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs
git commit -m "feat: resolve ScriptureItemInstance theme via playlist scripture default"
```

---

### Task 7: SlideThemeDesigner UI — set-as-default context menu + badges

**Files:**
- Create: `HandsLiftedApp.Core/Converters/IsDefaultSongThemeConverter.cs`
- Create: `HandsLiftedApp.Core/Converters/IsDefaultSongMotionThemeConverter.cs`
- Create: `HandsLiftedApp.Core/Converters/IsDefaultScriptureThemeConverter.cs`
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml`
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs`

**Interfaces:**
- Consumes: `PlaylistInstance.DefaultSongThemeId`/`DefaultSongMotionThemeId`/`DefaultScriptureThemeId` from Task 1, reached via `Globals.Instance.MainViewModel?.Playlist` (matching the existing `IsDefaultThemeConverter`'s access pattern).

No automated test: this is Avalonia desktop UI with no headless test harness in this repo. Verification is manual (Step 6) — per this project's own convention (see `CLAUDE.md`'s note on the flyout/popup dialog bug that shipped because nobody clicked through it), do not consider this task done without actually running the app and exercising the context menu.

- [ ] **Step 1: Add the three new converters**

`HandsLiftedApp.Core/Converters/IsDefaultSongThemeConverter.cs`:

```csharp
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class IsDefaultSongThemeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is BaseSlideTheme theme)
                return theme.Id == Globals.Instance.MainViewModel?.Playlist?.DefaultSongThemeId;
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
```

`HandsLiftedApp.Core/Converters/IsDefaultSongMotionThemeConverter.cs` (identical shape, checking `DefaultSongMotionThemeId`):

```csharp
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class IsDefaultSongMotionThemeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is BaseSlideTheme theme)
                return theme.Id == Globals.Instance.MainViewModel?.Playlist?.DefaultSongMotionThemeId;
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
```

`HandsLiftedApp.Core/Converters/IsDefaultScriptureThemeConverter.cs` (checking `DefaultScriptureThemeId`):

```csharp
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class IsDefaultScriptureThemeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is BaseSlideTheme theme)
                return theme.Id == Globals.Instance.MainViewModel?.Playlist?.DefaultScriptureThemeId;
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
```

- [ ] **Step 2: Register the converters and add context menu items in the XAML**

In `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml`, add to `<UserControl.Resources>` (after the existing `IsDefaultThemeConverter` entry):

```xml
        <converters2:IsDefaultSongThemeConverter x:Key="IsDefaultSongThemeConverter" />
        <converters2:IsDefaultSongMotionThemeConverter x:Key="IsDefaultSongMotionThemeConverter" />
        <converters2:IsDefaultScriptureThemeConverter x:Key="IsDefaultScriptureThemeConverter" />
```

In the per-row `ContextMenu` (the one inside `<Style Selector="ListBoxItem">`), add three `MenuItem`s after the existing `Remove` item:

```xml
                                <MenuItem Header="Set as default for Songs" Click="SetDefaultSongTheme_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="Star" />
                                    </MenuItem.Icon>
                                </MenuItem>
                                <MenuItem Header="Set as default for Songs (motion bg)" Click="SetDefaultSongMotionTheme_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="Star" />
                                    </MenuItem.Icon>
                                </MenuItem>
                                <MenuItem Header="Set as default for Scripture" Click="SetDefaultScriptureTheme_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="Star" />
                                    </MenuItem.Icon>
                                </MenuItem>
```

In the `ListBox.ItemTemplate`'s `DataTemplate`, add three badges after the existing `default` `TextBlock`:

```xml
                            <TextBlock Text="default (songs)"
                                       DockPanel.Dock="Bottom"
                                       HorizontalAlignment="Center"
                                       FontSize="10"
                                       Foreground="#888888"
                                       IsVisible="{Binding ., Converter={StaticResource IsDefaultSongThemeConverter}}" />
                            <TextBlock Text="default (songs, motion)"
                                       DockPanel.Dock="Bottom"
                                       HorizontalAlignment="Center"
                                       FontSize="10"
                                       Foreground="#888888"
                                       IsVisible="{Binding ., Converter={StaticResource IsDefaultSongMotionThemeConverter}}" />
                            <TextBlock Text="default (scripture)"
                                       DockPanel.Dock="Bottom"
                                       HorizontalAlignment="Center"
                                       FontSize="10"
                                       Foreground="#888888"
                                       IsVisible="{Binding ., Converter={StaticResource IsDefaultScriptureThemeConverter}}" />
```

- [ ] **Step 3: Add the three click handlers in code-behind**

In `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs`, add after `RemoveItem_OnClick`:

```csharp
        private void SetDefaultSongTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                mainViewModel.Playlist.DefaultSongThemeId = item.Id;
            }
        }

        private void SetDefaultSongMotionTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                mainViewModel.Playlist.DefaultSongMotionThemeId = item.Id;
            }
        }

        private void SetDefaultScriptureTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                mainViewModel.Playlist.DefaultScriptureThemeId = item.Id;
            }
        }
```

- [ ] **Step 4: Extend the remove-guard to cover all three new slots**

In `RemoveItem_OnClick`, replace:

```csharp
                        if (item.Id == Globals.Instance.AppPreferences?.DefaultTheme?.Id)
                        {
                            MessageBus.Current.SendMessage(new MessageWindowViewModel()
                                { Title = "Cannot remove the global default theme" });
                        }
```

with:

```csharp
                        if (item.Id == Globals.Instance.AppPreferences?.DefaultTheme?.Id
                            || item.Id == mainViewModel.Playlist.DefaultSongThemeId
                            || item.Id == mainViewModel.Playlist.DefaultSongMotionThemeId
                            || item.Id == mainViewModel.Playlist.DefaultScriptureThemeId)
                        {
                            MessageBus.Current.SendMessage(new MessageWindowViewModel()
                                { Title = "Cannot remove a theme that is set as a default" });
                        }
```

- [ ] **Step 5: Build the solution**

Run: `dotnet build HandsLiftedApp.sln`
Expected: Build succeeds.

- [ ] **Step 6: Manual verification (required — see rationale above)**

Run the app, open the slide theme designer, right-click a theme and confirm all three "Set as default for..." actions appear and each one toggles the corresponding badge on that row (and off any row that previously had it). Confirm attempting to remove a theme currently set as any of the three defaults shows the "Cannot remove a theme that is set as a default" message instead of deleting it. Confirm a song slide/song title slide with no explicit `Design` picks up the "Songs" default, and the same song with a motion background video path set picks up the "Songs (motion bg)" default instead, and that toggling the video path live-swaps the displayed theme.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/Converters/IsDefaultSongThemeConverter.cs HandsLiftedApp.Core/Converters/IsDefaultSongMotionThemeConverter.cs HandsLiftedApp.Core/Converters/IsDefaultScriptureThemeConverter.cs HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs
git commit -m "feat: set-as-default context menu for playlist-scoped theme slots"
```
