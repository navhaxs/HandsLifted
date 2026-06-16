# Playlist Load Performance — Design Spec

**Date:** 2026-06-15  
**Status:** Approved

---

## Problem

Playlist load freezes UI for ~7 seconds. Root causes:

1. **XML deserialization on UI thread** (~1s) — `HandsLiftedDocXmlSerializer.DeserializePlaylist` blocks the UI thread.
2. **Construction triggers rendering** — `SongSlideInstance` constructor calls `debounceDispatcher.Debounce(() => GenerateBitmaps())` (line 57), plus `WhenAnyValue(x => x.Text)` emits its initial value on subscribe, both scheduling full SkiaSharp renders per slide during construction. 93 slides × ~50ms render = significant background load that floods the UI thread with ~186 binding update dispatches (`Cached` + `Thumbnail` per slide).
3. **Synchronous image decoding in XAML converter** — `BitmapAssetValueConverter` calls `BitmapLoader.LoadBitmap` (sync) during layout evaluation, blocking the UI thread per fresh image.

---

## Non-Goals

- No changes to the live-editing debounce path (Design/Theme/Text change after load).
- No virtualization of the slide list (deferred to future).
- No architectural change to `UpdateStanzaSlides` or `SongItemInstance`.

---

## Design

### Fix A — Deserialize off UI thread

**File:** `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`

Wrap `DeserializePlaylist` in `Task.Run`. Also fix pre-existing bug: the call currently always passes `msg.FilePath` instead of `loadFilePath` (the autosave-resolved path).

```csharp
// Before (line 156):
var x = HandsLiftedDocXmlSerializer.DeserializePlaylist(msg.FilePath);

// After:
var x = await Task.Run(() => HandsLiftedDocXmlSerializer.DeserializePlaylist(loadFilePath));
```

Remainder of the load handler (property assignments, `ItemInstanceFactory` loop, `Playlist.Items =`) stays on the UI thread — those touch ReactiveUI INPC objects and must remain there.

---

### Fix B — Pure construction: no render side-effects

**File:** `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongSlideInstance.cs`

Remove line 57 (the explicit initial `Debounce(GenerateBitmaps)`). Add `.Skip(1)` to all subscriptions that fire on construction to suppress initial-value emissions:

```csharp
// Design change subscription (already has Skip(1) — verify, leave as-is)
parentSongItem?.WhenAnyValue(x => x.Design)
    .Skip(1)  // already present — no change
    ...

// Theme property change — already fires on Switch(), no initial-value concern

// Text change subscription:
this.WhenAnyValue(x => x.Text)
    .Skip(1)                           // ADD: suppress construction-time emission
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(_ => RequestRender());
```

Rename `GenerateBitmaps()` → `Render()` (private, called by orchestrator and `RequestRender`).

`RequestRender()` = per-slide debounce for edit path:
```csharp
private void RequestRender()
    => debounceDispatcher.Debounce(() => Globals.Instance.SlideRenderQueue.Enqueue(this));
```

After this fix, constructing 93 `SongSlideInstance` objects does zero rendering work.

---

### Fix C — SlideRenderQueue: bounded-concurrency orchestrator

**New file:** `HandsLiftedApp.Core/Services/SlideRenderQueue.cs`

Single instance on `Globals`. Replaces 93 independent debounce timers for the initial-load path. Shared by load path (batch) and edit path (single-item via `RequestRender`).

```csharp
public sealed class SlideRenderQueue
{
    // ProcessorCount / 2, min 2 — leaves headroom for UI thread
    private readonly SemaphoreSlim _gate =
        new(Math.Max(2, Environment.ProcessorCount / 2));

    public void Enqueue(SongSlideInstance slide)
        => _ = Task.Run(() => RenderOne(slide));

    public void EnqueueBatch(IEnumerable<SongSlideInstance> slides)
        => _ = Task.Run(() => RenderBatch(slides));

    private async Task RenderBatch(IEnumerable<SongSlideInstance> slides)
    {
        var tasks = slides.Select(s => RenderOne(s));
        await Task.WhenAll(tasks);
    }

    private async Task RenderOne(SongSlideInstance slide)
    {
        await _gate.WaitAsync();
        try { await Task.Run(slide.Render); }
        finally { _gate.Release(); }
    }
}
```

`SongSlideInstance.Render()` (replaces `GenerateBitmaps`):

```csharp
internal void Render()
{
    var spec = SongSlideSpecBuilder.Build(this);
    using var sk = SlideRenderer.RenderToSKBitmap(spec);
    var cached = BitmapUtils.SKBitmapToAvalonia(sk);
    var thumb  = BitmapUtils.CreateThumbnail(cached);
    // Post at Background priority — won't starve user input/scroll
    Dispatcher.UIThread.Post(() => { Cached = cached; Thumbnail = thumb; },
                             DispatcherPriority.Background);
}
```

**Wire-up in `UpdateStanzaSlides`** (`SongItemInstance.cs`, after `StanzaSlides = newSlides`):

```csharp
// Enqueue initial render for all new SongSlideInstance slides
var newSongSlides = newSlides.OfType<SongSlideInstance>().ToList();
if (newSongSlides.Count > 0)
    Globals.Instance.SlideRenderQueue.EnqueueBatch(newSongSlides);
```

**Globals:** Add `SlideRenderQueue SlideRenderQueue { get; } = new SlideRenderQueue();`

---

### Fix D — Async XAML image converter

**File:** `HandsLiftedApp.Utils/Extensions/BitmapAssetValueConverter.cs`

Current: calls `BitmapLoader.LoadBitmap` (sync) — decodes from disk on UI thread.

Replace with async load + placeholder pattern:

```csharp
public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
{
    if (value is not string rawUri || string.IsNullOrWhiteSpace(rawUri))
        return null;

    // Return null (placeholder) immediately; load async and trigger re-bind via reactive property
    // Use BitmapLoader cache — cache hit is synchronous and fast
    var cached = BitmapLoader.Cache.GetBitmap(rawUri);
    if (cached != null) return cached;

    // Fresh load: kick off async, return null for now
    _ = BitmapLoader.LoadBitmapAsync(rawUri);
    return null;
}
```

This means first display shows no image briefly; subsequent bindings hit the cache synchronously. For `avares://` paths (built-in assets) the existing synchronous `AssetLoader.Open` path is fast and can remain sync.

---

## File Changelist

| File | Change |
|---|---|
| `MainViewModel.cs` | Fix A: `await Task.Run(DeserializePlaylist)`, fix loadFilePath bug |
| `SongSlideInstance.cs` | Fix B: remove line-57 Debounce, `.Skip(1)` on Text sub, rename `GenerateBitmaps`→`Render`, add `RequestRender` |
| `SlideRenderQueue.cs` | Fix C: new file |
| `Globals.cs` | Fix C: add `SlideRenderQueue` property |
| `SongItemInstance.cs` | Fix C: wire `EnqueueBatch` after `newSlides` built in `UpdateStanzaSlides` |
| `BitmapAssetValueConverter.cs` | Fix D: async load with cache-hit fast path |

---

## Expected Result

| Phase | Before | After |
|---|---|---|
| Deserialization | ~1s blocking UI | ~1s on threadpool |
| Item construction | ~200ms UI thread | ~50ms (no render) |
| Thumbnail generation | ~4s flooding UI dispatches | Background, ~500ms, non-blocking |
| Fresh image decodes (converter) | ~100–200ms per image, UI thread | Async, non-blocking |
| **Total UI freeze** | **~7s** | **<200ms** |

---

## Risks / Notes

- `SongSlideInstance.Render()` accesses `SongSlideSpecBuilder.Build(this)` from a threadpool thread. `Build` reads `this.Theme`, `this.Text`, etc. Verify no unsafe mutation during read. Existing code already calls this off-thread via `DebounceDispatcher`, so no regression.
- `SlideRenderer.RenderToSKBitmap` creates a SkiaSharp canvas. SkiaSharp is thread-safe for independent canvases — no shared state except `BitmapCache` which is already `lock`-guarded. No regression.
- `BitmapUtils.SKBitmapToAvalonia` creates `new Bitmap(stream)` — Avalonia `Bitmap` construction is thread-safe. No regression.
- `Dispatcher.UIThread.Post(DispatcherPriority.Background)` means thumbnails populate after idle — visually: slides show as blank then fill in. Acceptable for load path. Edit path (live typing) uses `RequestRender()` → debounce → single `Render()` — same behaviour as today but with Background-priority UI post.
