# Scripture Live Theme-Edit Repagination Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When any property of a scripture item's currently-resolved theme changes — including editing the theme object in place, not just switching `Design` to a different theme — the item repaginates (debounced) and invalidates its slides' cached thumbnails, so live theme edits (e.g. dragging a font-size slider) don't leave stale, wrongly-wrapped slides.

**Architecture:** A new reactive subscription in `ScriptureItemInstance`'s constructor, mirroring `ScriptureSlideInstance`'s existing theme-change pattern (`WhenAnyValue(Theme).Select(t => t?.WhenAnyPropertyChanged()...).Switch()`) but wired to a debounced full `GenerateSlidesAsync(forceInvalidateCache: true)` instead of a plain re-render. `GenerateSlidesAsync`/`UpdatePages` gain a `forceInvalidateCache` parameter (default `false`) so every existing call site is unaffected.

**Tech Stack:** .NET 8, MSTest, ReactiveUI/DynamicData (`WhenAnyPropertyChanged`), `DebounceThrottle` (`DebounceDispatcher`, already used elsewhere in this codebase for the identical purpose).

## Global Constraints

- net8.0, MSTest, matches all prior phases.
- Any theme property change triggers repagination (not filtered to layout-affecting properties only) — the repagination pass is cheap, in-memory-only, no I/O.
- Debounce duration: 200ms, matching `ScriptureSlideInstance`'s own render-trigger and `SongItemInstance`'s stanza-update debounce (same `DebounceDispatcher` class, same duration).
- Force-invalidate `Cached` unconditionally on every reused slide when `forceInvalidateCache` is true, rather than relying on content-diffing — closes the edge case where a font-size delta coincidentally doesn't shift any line-wrap boundary.
- Every existing `GenerateSlidesAsync()` call site (initial load in `ItemInstanceFactory.cs`/`MainViewModel.cs`, verse-range edits, `ResolvedDesignTheme`'s own setter for `Design`-switching) is unaffected by the new parameter's default value — no other file needs to change.
- No automated test for the debounced reactive subscription's wiring itself — this codebase has no existing test for `ScriptureSlideInstance`'s own analogous Theme-change-triggers-re-render subscription either (established precedent: bare reactive wiring is verified by code review, not timing-dependent tests). The new `forceInvalidateCache` parameter's *effect* is directly, deterministically unit-tested instead (see Task 1's test).

---

### Task 1: Live theme-edit repagination

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs`

**Interfaces:**
- Produces: `ScriptureItemInstance.GenerateSlidesAsync(bool forceInvalidateCache = false)` (signature change — was `GenerateSlidesAsync()`, no params). No other public interface changes.
- Consumes: `BaseSlideTheme.WhenAnyPropertyChanged()` (DynamicData, `HandsLiftedApp.Data.SlideTheme.BaseSlideTheme` already supports this — `ScriptureSlideInstance.cs` already calls it the same way), `DebounceDispatcher` (`DebounceThrottle` package, already a dependency of this project via `ScriptureSlideInstance.cs`/`SongItemInstance.cs`).

This is the only task in this plan — the change is small and tightly coupled (constructor subscription + method signature + cache-invalidation logic all need to land together to make sense).

- [ ] **Step 1: Write the failing test**

In `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs`, add `using HandsLiftedApp.Core.Models.Thumbnail;` and `using SkiaSharp;` to the using block, then add this test method (anywhere in the class, e.g. after the existing `ResolvedDesignTheme_DesignEmpty_FallsBackToDefaultTheme` test):

```csharp
    [TestMethod]
    public async Task GenerateSlidesAsync_ForceInvalidateCache_ResetsCachedOnReusedSlide()
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
        var slide = (ScriptureSlideInstance)instance.Slides[0];

        using var skBitmap = new SKBitmap(1, 1);
        slide.Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
        Assert.IsNotNull(slide.Cached);

        await instance.GenerateSlidesAsync(forceInvalidateCache: true);
        Dispatcher.UIThread.RunJobs();

        Assert.IsNull(slide.Cached, "forceInvalidateCache must reset Cached even when content didn't change");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~GenerateSlidesAsync_ForceInvalidateCache_ResetsCachedOnReusedSlide"`
Expected: FAIL — compile error, `GenerateSlidesAsync` doesn't accept a `forceInvalidateCache` argument yet.

- [ ] **Step 3: Add the reactive subscription and `_themeChangeDebounce` field**

In `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`, add `using DebounceThrottle;` and `using DynamicData.Binding;` to the using block.

Add this field right after the existing `_injectedStore` field:

```csharp
        private readonly DebounceDispatcher _themeChangeDebounce = new(200);
```

In the constructor, insert this new subscription immediately after the existing `this.WhenAnyValue(x => x.Design).Subscribe(_ => this.RaisePropertyChanged(nameof(ResolvedDesignTheme)));` line and before the 8-argument `ItemDataModified` subscription:

```csharp
            // Repaginate (debounced) whenever any property of the currently-resolved theme
            // changes — not just when Design switches to a different theme object, but also
            // when an already-selected theme is edited in place (e.g. a live font-size slider
            // in the theme editor, which mutates the same BaseSlideTheme object every item
            // pointing at that Design already shares). Pagination is font-size/line-height
            // dependent, so a plain re-render (what ScriptureSlideInstance's own Theme-change
            // subscription does) isn't enough here — the slide count itself may need to change.
            this.WhenAnyValue(x => x.ResolvedDesignTheme)
                .Select(t => t?.WhenAnyPropertyChanged() ?? Observable.Never<BaseSlideTheme?>())
                .Switch()
                .Subscribe(_ => _themeChangeDebounce.Debounce(() =>
                    _ = GenerateSlidesAsync(forceInvalidateCache: true).ContinueWith(
                        t => Log.Error(t.Exception, "Failed to generate scripture slides for {Title}", Title),
                        TaskContinuationOptions.OnlyOnFaulted)));
```

- [ ] **Step 4: Add the `forceInvalidateCache` parameter**

Change the `GenerateSlidesAsync` signature and its call to `UpdatePages`. Replace:

```csharp
        public async Task GenerateSlidesAsync()
        {
```

with:

```csharp
        public async Task GenerateSlidesAsync(bool forceInvalidateCache = false)
        {
```

and replace the method's final line:

```csharp
            var referenceLabel = FormatReferenceLabel(bookTitle);
            UpdatePages(referenceLabel, verses);
        }
```

with:

```csharp
            var referenceLabel = FormatReferenceLabel(bookTitle);
            UpdatePages(referenceLabel, verses, forceInvalidateCache);
        }
```

- [ ] **Step 5: Thread the parameter through `UpdatePages` and use it in the cache-invalidation check**

Replace:

```csharp
        private void UpdatePages(string referenceLabel, List<ScriptureVerseRef> verses)
        {
```

with:

```csharp
        private void UpdatePages(string referenceLabel, List<ScriptureVerseRef> verses, bool forceInvalidateCache = false)
        {
```

Replace the reused-slide branch:

```csharp
                    if (existing != null)
                    {
                        existing.Lines = page.Lines;
                        if (existing.Text != flatText) existing.Text = flatText;
                        if (existing.Label != referenceLabel) existing.Label = referenceLabel;
                        if (!ReferenceEquals(existing.Theme, theme))
                        {
                            existing.Theme = theme;
                            existing.Cached = null;
                        }
                        newSlides.Add(existing);
                    }
```

with:

```csharp
                    if (existing != null)
                    {
                        existing.Lines = page.Lines;
                        if (existing.Text != flatText) existing.Text = flatText;
                        if (existing.Label != referenceLabel) existing.Label = referenceLabel;
                        bool themeChanged = !ReferenceEquals(existing.Theme, theme);
                        if (themeChanged) existing.Theme = theme;
                        if (themeChanged || forceInvalidateCache) existing.Cached = null;
                        newSlides.Add(existing);
                    }
```

(The rest of `UpdatePages` — the new-slide branch, the `_slides`/`RaisePropertyChanged`/`EnqueueBatch` block — is unchanged.)

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureItemInstanceTests"`
Expected: PASS (8 tests — 7 existing + 1 new).

- [ ] **Step 7: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 148 + 1 = 149, no regressions.

- [ ] **Step 8: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs
git commit -m "feat: repaginate scripture slides on live theme-property edits, not just Design switches"
```

- [ ] **Step 9: Manual verification**

Run the app. Insert a scripture item with a long enough passage to span multiple slides at a moderate font size. Open the theme editor for the Design that item currently uses, drag the font-size slider up significantly, and confirm (after ~a quarter second) the item's slide count/thumbnails update to reflect the new size — no overlapping/overflowing text, no stale-sized thumbnails.

---

## Final Whole-Branch Review

This is a single-task plan touching one production file and one test file — the final review can be scoped tightly: confirm `GenerateSlidesAsync`'s two other call sites outside this file (`ItemInstanceFactory.cs`, `MainViewModel.cs`) still compile unchanged (they call it with no arguments, which the new default parameter value preserves), and confirm the new subscription doesn't fire during construction before `ParentPlaylist`/`Design` are set (i.e. no NRE or premature repagination on a freshly-constructed, not-yet-configured instance).
