# Playlist Load Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate ~7-second UI freeze during playlist load by moving deserialization off the UI thread, making slide construction side-effect-free, and routing all thumbnail generation through a bounded-concurrency background queue.

**Architecture:** Fix A moves XML deserialization to a threadpool Task. Fix B removes render side-effects from `SongSlideInstance`/`SongTitleSlideInstance` constructors so construction is pure. Fix C adds `SlideRenderQueue` (a `SemaphoreSlim`-gated `Task.Run` dispatcher) registered on `Globals`, wired from `UpdateStanzaSlides` for initial batch load and from per-slide `RequestRender()` for live edits. Fix D makes `CustomSlideRender` load images asynchronously instead of blocking the UI thread.

**Tech Stack:** C# / net8.0, Avalonia 11, ReactiveUI, SkiaSharp, MSTest

---

## File Map

| File | Action | Reason |
|---|---|---|
| `HandsLiftedApp.Core/ViewModels/MainViewModel.cs` | Modify | Fix A: async deserialize |
| `HandsLiftedApp.Core/Services/SlideRenderQueue.cs` | **Create** | Fix C: queue implementation |
| `HandsLiftedApp.Core/Globals.cs` | Modify | Fix C: register queue as singleton |
| `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongSlideInstance.cs` | Modify | Fix B: pure constructor, Render() method |
| `HandsLiftedApp.Data/Slides/SongTitleSlideInstance.cs` | Modify | Fix B: same as SongSlideInstance |
| `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs` | Modify | Fix C: EnqueueBatch after UpdateStanzaSlides |
| `HandsLiftedApp.Core/Render/CustomSlide/CustomSlideRender.axaml.cs` | Modify | Fix D: async image load |
| `HandsLiftedApp.Tests/Services/SlideRenderQueueTests.cs` | **Create** | Unit tests for SlideRenderQueue |

---

## Task 1: Fix A — Deserialize off UI thread

**Files:**
- Modify: `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Read current line 156 context**

Verify the current code reads:
```csharp
var x = HandsLiftedDocXmlSerializer.DeserializePlaylist(msg.FilePath);
```
Note: `msg.FilePath` is used here but `loadFilePath` (the autosave-resolved path) was computed at line 142-153. This is a pre-existing bug — also fixed here.

- [ ] **Step 2: Change deserialization to run on threadpool**

In `MainViewModel.cs`, replace line 156:
```csharp
// Before:
var x = HandsLiftedDocXmlSerializer.DeserializePlaylist(msg.FilePath);

// After:
var x = await Task.Run(() => HandsLiftedDocXmlSerializer.DeserializePlaylist(loadFilePath));
```

The handler at line 118 is already `async`, so `await` is valid here.

- [ ] **Step 3: Build to confirm no compile errors**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --no-restore
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```powershell
git add HandsLiftedApp.Core/ViewModels/MainViewModel.cs
git commit -m "perf: deserialize playlist XML on threadpool thread

Fix A: HandsLiftedDocXmlSerializer.DeserializePlaylist blocks UI thread for ~1s.
Move to Task.Run. Also fixes pre-existing bug using msg.FilePath instead of
loadFilePath (the autosave-resolved path)."
```

---

## Task 2: Create `SlideRenderQueue`

**Files:**
- Create: `HandsLiftedApp.Core/Services/SlideRenderQueue.cs`
- Create: `HandsLiftedApp.Tests/Services/SlideRenderQueueTests.cs`

- [ ] **Step 1: Write failing tests first**

Create `HandsLiftedApp.Tests/Services/SlideRenderQueueTests.cs`:

```csharp
using System.Collections.Concurrent;
using HandsLiftedApp.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class SlideRenderQueueTests
{
    [TestMethod]
    public async Task EnqueueAsync_ExecutesWork()
    {
        var queue = new SlideRenderQueue(maxConcurrency: 1);
        bool ran = false;

        await queue.EnqueueAsync(() => ran = true);

        Assert.IsTrue(ran);
    }

    [TestMethod]
    public async Task EnqueueBatchAsync_RunsAllWorkItems()
    {
        var queue = new SlideRenderQueue(maxConcurrency: 2);
        var executed = new ConcurrentBag<int>();

        await queue.EnqueueBatchAsync(
            Enumerable.Range(0, 5).Select<int, Action>(i => () => executed.Add(i)));

        Assert.AreEqual(5, executed.Count);
    }

    [TestMethod]
    public async Task EnqueueBatchAsync_RespectsConcurrencyLimit()
    {
        const int maxConcurrency = 2;
        var queue = new SlideRenderQueue(maxConcurrency: maxConcurrency);
        int maxObserved = 0;
        int current = 0;
        var sync = new object();

        await queue.EnqueueBatchAsync(
            Enumerable.Range(0, 10).Select<int, Action>(_ => () =>
            {
                lock (sync)
                {
                    current++;
                    if (current > maxObserved) maxObserved = current;
                }
                Thread.Sleep(20);
                lock (sync) { current--; }
            }));

        Assert.IsTrue(maxObserved <= maxConcurrency,
            $"Expected max {maxConcurrency}, observed {maxObserved}");
    }

    [TestMethod]
    public async Task EnqueueAsync_DefaultConcurrency_IsAtLeastTwo()
    {
        // Verify default ctor doesn't throw and processes work
        var queue = new SlideRenderQueue();
        bool ran = false;
        await queue.EnqueueAsync(() => ran = true);
        Assert.IsTrue(ran);
    }
}
```

- [ ] **Step 2: Run tests — verify they FAIL**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "SlideRenderQueueTests" --no-build 2>&1
```
Expected: Build error — `SlideRenderQueue` does not exist yet.

- [ ] **Step 3: Create `SlideRenderQueue.cs`**

Create `HandsLiftedApp.Core/Services/SlideRenderQueue.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HandsLiftedApp.Core.Services;

/// <summary>
/// Renders slides on threadpool threads, bounded by processor count / 2.
/// Keeps headroom for the UI thread and prevents all cores saturating on thumbnail generation.
/// </summary>
public sealed class SlideRenderQueue
{
    private readonly SemaphoreSlim _gate;

    public SlideRenderQueue(int maxConcurrency = 0)
    {
        var concurrency = maxConcurrency > 0
            ? maxConcurrency
            : Math.Max(2, Environment.ProcessorCount / 2);
        _gate = new SemaphoreSlim(concurrency, concurrency);
    }

    /// <summary>
    /// Enqueues a single unit of render work. Returns a Task for testing; callers
    /// in production should fire-and-forget with <c>_ =</c>.
    /// </summary>
    public Task EnqueueAsync(Action work) => RunWithGate(work);

    /// <summary>
    /// Enqueues a batch of render work items and waits until all complete.
    /// Returns a Task for testing; callers in production should fire-and-forget.
    /// </summary>
    public Task EnqueueBatchAsync(IEnumerable<Action> workItems)
        => Task.WhenAll(workItems.Select(RunWithGate));

    /// <summary>Fire-and-forget convenience overload for production use.</summary>
    public void Enqueue(IRenderable renderable)
        => _ = EnqueueAsync(renderable.Render);

    /// <summary>Fire-and-forget batch convenience overload for production use.</summary>
    public void EnqueueBatch(IEnumerable<IRenderable> renderables)
        => _ = EnqueueBatchAsync(renderables.Select<IRenderable, Action>(r => r.Render));

    private async Task RunWithGate(Action work)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await Task.Run(work).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
}
```

- [ ] **Step 4: Add `IRenderable` interface to the same file** (above the class)

```csharp
/// <summary>
/// Implemented by slide types whose thumbnail bitmaps are generated off-thread.
/// </summary>
public interface IRenderable
{
    void Render();
}
```

- [ ] **Step 5: Run tests — verify they PASS**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "SlideRenderQueueTests"
```
Expected: 4 tests pass.

- [ ] **Step 6: Commit**

```powershell
git add HandsLiftedApp.Core/Services/SlideRenderQueue.cs
git add HandsLiftedApp.Tests/Services/SlideRenderQueueTests.cs
git commit -m "feat: add SlideRenderQueue for bounded-concurrency thumbnail generation

Fix C: bounded threadpool queue (ProcessorCount/2) replaces 93 independent
debounce timers. Exposes IRenderable interface for slide types."
```

---

## Task 3: Register `SlideRenderQueue` on `Globals`

**Files:**
- Modify: `HandsLiftedApp.Core/Globals.cs`

- [ ] **Step 1: Add `SlideRenderQueue` property to `Globals`**

In `Globals.cs`, add after the `ImportWorkerThread` property (line 43):

```csharp
public ImportWorkerThread ImportWorkerThread { get; } = new();

// Add this line:
public SlideRenderQueue SlideRenderQueue { get; } = new SlideRenderQueue();
```

- [ ] **Step 2: Add missing using at top of `Globals.cs`**

The file already has `using HandsLiftedApp.Core.Services;` — if not present, add it after the existing usings.

- [ ] **Step 3: Build**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --no-restore
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```powershell
git add HandsLiftedApp.Core/Globals.cs
git commit -m "feat: register SlideRenderQueue singleton on Globals"
```

---

## Task 4: Refactor `SongSlideInstance` — pure constructor (Fix B)

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongSlideInstance.cs`

The goals:
1. Add `text` and `label` optional constructor params — set BEFORE subscriptions so initial `WhenAnyValue` emissions carry the correct value.
2. `.Skip(1)` on the `WhenAnyValue(x => x.Text)` subscription so the construction-time initial emission is suppressed.
3. Remove line 57 (`debounceDispatcher.Debounce(() => GenerateBitmaps())`) — no side-effect render at construction.
4. Rename `GenerateBitmaps()` → `Render()`, implement `IRenderable`.
5. Add `RequestRender()` for the live-edit debounce path.

- [ ] **Step 1: Replace the constructor and rename `GenerateBitmaps`**

Replace the entire `SongSlideInstance` constructor and `GenerateBitmaps` method with:

```csharp
public SongSlideInstance(SongItemInstance? parentSongItem, SongStanza? parentSongStanza, string id,
    string? text = null, string? label = null)
    : base(parentSongItem, parentSongStanza, id)
{
    // Set text/label BEFORE subscriptions so initial WhenAnyValue emissions
    // carry the correct values; Skip(1) suppresses those initial emissions.
    if (text != null) Text = text;
    if (label != null) Label = label;

    Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty);

    parentSongItem?.WhenAnyValue(x => x.Design)
        .Skip(1)
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(designId =>
        {
            Theme = ResolveTheme(designId);
            RequestRender();
        });

    this.WhenAnyValue(x => x.Theme)
        .Select(t => t?.WhenAnyPropertyChanged() ?? Observable.Never<BaseSlideTheme?>())
        .Switch()
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(_ => RequestRender());

    this.WhenAnyValue(x => x.Text)
        .Skip(1)  // suppress construction-time emission; text was set before subscriptions
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(_ => RequestRender());

    _calculatedSlideThumbnailBadge = this.WhenAnyValue(x => x.Label, x => x.ParentSongStanza,
            (label2, parentSongStanza) =>
            {
                if (label2 != null && label2.Length > 0)
                    return new SlideThumbnailBadge() { Label = label2, Colour = parentSongStanza.Colour };
                return null;
            })
        .ObserveOn(RxApp.MainThreadScheduler)
        .ToProperty(this, x => x.SlideThumbnailBadge);
}

private void RequestRender()
    => debounceDispatcher.Debounce(() => Globals.Instance.SlideRenderQueue.Enqueue(this));

public void Render()
{
    var spec = SongSlideSpecBuilder.Build(this);
    using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
    var cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
    var thumb = BitmapUtils.CreateThumbnail(cached);
    Avalonia.Threading.Dispatcher.UIThread.Post(
        () => { Cached = cached; Thumbnail = thumb; },
        Avalonia.Threading.DispatcherPriority.Background);
}
```

Also add `: IRenderable` to the class declaration:
```csharp
public class SongSlideInstance : SongSlide, ISlideInstance, IRenderable
```

Add the missing using if not present:
```csharp
using HandsLiftedApp.Core.Services;
```

- [ ] **Step 2: Build**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --no-restore
```
Expected: Build succeeded. If there are compile errors about `IRenderable` not found, verify the using for `HandsLiftedApp.Core.Services` is at the top of `SongSlideInstance.cs`.

- [ ] **Step 3: Commit**

```powershell
git add HandsLiftedApp.Core/Models/RuntimeData/Slides/SongSlideInstance.cs
git commit -m "refactor: pure SongSlideInstance constructor, Render() off-thread

Fix B: remove render side-effect from constructor (line 57). Add text/label
ctor params set before subscriptions. Skip(1) on Text sub suppresses init
emission. Rename GenerateBitmaps->Render; post Cached/Thumbnail at Background
priority so UI updates don't starve input. Implements IRenderable."
```

---

## Task 5: Refactor `SongTitleSlideInstance` — same pattern (Fix B)

**Files:**
- Modify: `HandsLiftedApp.Data/Slides/SongTitleSlideInstance.cs`

Same pattern: remove line 68 explicit render, add `Skip(1)` to Title/Copyright subscription, rename `GenerateBitmaps` → `Render()`, implement `IRenderable`, add `RequestRender()`.

- [ ] **Step 1: Replace constructor and `GenerateBitmaps`**

Replace the constructor and `GenerateBitmaps` method:

```csharp
public SongTitleSlideInstance(SongItemInstance? parentSongItem) : base()
{
    ParentSongItem = parentSongItem;
    Log.Verbose("Creating slide instance");
    Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty);

    parentSongItem?.WhenAnyValue(x => x.Design)
        .Skip(1)
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(designId =>
        {
            Theme = ResolveTheme(designId);
            RequestRender();
        });

    parentSongItem?.WhenAnyValue(x => x.MotionBackgroundVideoPath)
        .Skip(1)
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(_ => RequestRender());

    this.WhenAnyValue(x => x.Theme)
        .Select(t => t?.WhenAnyPropertyChanged() ?? Observable.Never<BaseSlideTheme?>())
        .Switch()
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(_ => RequestRender());

    this.WhenAnyValue(s => s.Title, s => s.Copyright)
        .Skip(1)  // suppress construction-time emission; values are set externally after construction
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(_ => RequestRender());

    // No explicit Render() call here — UpdateStanzaSlides enqueues via SlideRenderQueue
}

private void RequestRender()
    => debounceDispatcher.Debounce(() => Globals.Instance.SlideRenderQueue.Enqueue(this));

public void Render()
{
    SKBitmap? videoFrame = null;
    if (HasMotionBackground && System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows))
    {
        var videoPath = ParentSongItem?.MotionBackgroundVideoPath;
        if (!string.IsNullOrWhiteSpace(videoPath))
        {
            try
            {
                using var avaBmp = ShellThumbs.WindowsThumbnailProvider.GetThumbnail(
                    videoPath, 1920, 1080, ShellThumbs.ThumbnailOptions.None);
                if (avaBmp != null)
                    videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SongTitleSlideInstance] Failed to extract video thumbnail from {Path}", videoPath);
            }
        }
    }

    var spec = SongTitleSlideSpecBuilder.Build(this, videoFrame);
    using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
    videoFrame?.Dispose();
    var cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
    var thumb = BitmapUtils.CreateThumbnail(cached);
    Avalonia.Threading.Dispatcher.UIThread.Post(
        () => { Cached = cached; Thumbnail = thumb; },
        Avalonia.Threading.DispatcherPriority.Background);
}
```

Add `: IRenderable` to the class declaration:
```csharp
public class SongTitleSlideInstance : SongTitleSlide, ISlideInstance, IRenderable
```

Add the missing using if not present:
```csharp
using HandsLiftedApp.Core.Services;
```

- [ ] **Step 2: Build**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --no-restore
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```powershell
git add "HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs"
git commit -m "refactor: pure SongTitleSlideInstance constructor, Render() off-thread

Fix B: same pattern as SongSlideInstance. Remove line-68 explicit render,
Skip(1) on Title/Copyright sub, Render() posts at Background priority."
```

---

## Task 6: Wire `EnqueueBatch` in `UpdateStanzaSlides` (Fix C)

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs`

After `StanzaSlides = newSlides` (line ~310), collect newly-created slides and enqueue them. We detect "new" slides by checking `Cached == null` — freshly created slides have no cached bitmap yet.

- [ ] **Step 1: Update `UpdateStanzaSlides` to enqueue new slides**

After line 312 (`this.RaisePropertyChanged("Slides");`), add:

```csharp
// Enqueue newly created slides for background thumbnail generation.
// Cached == null identifies slides created this call (existing slides keep their cached bitmap).
var toRender = new System.Collections.Generic.List<IRenderable>();

foreach (var slide in newSlides)
{
    if (slide is SongSlideInstance s && s.Cached == null)
        toRender.Add(s);
}

// Include title slide if it hasn't been rendered yet
if (TitleSlide is SongTitleSlideInstance titleInst && titleInst.Cached == null)
    toRender.Add(titleInst);

if (toRender.Count > 0)
    Globals.Instance.SlideRenderQueue.EnqueueBatch(toRender);
```

Add the using at the top of `SongItemInstance.cs` if not present:
```csharp
using HandsLiftedApp.Core.Services;
```

- [ ] **Step 2: Update `UpdateStanzaSlides` object-initializer calls to use named params**

The two `new SongSlideInstance(...)` calls in `UpdateStanzaSlides` currently use object initializer syntax. Change them to constructor params so Text/Label are set before subscriptions fire:

Find (line ~267-268):
```csharp
var slide = new SongSlideInstance(this, _datum, slideId)
    { Text = Text, Label = Label, };
```
Replace with:
```csharp
var slide = new SongSlideInstance(this, _datum, slideId, text: Text, label: Label);
```

Find (line ~291):
```csharp
new SongSlideInstance(this, new SongStanza(), "BLANK") { }
```
Replace with:
```csharp
new SongSlideInstance(this, new SongStanza(), "BLANK")
```
(Trailing `{ }` was a no-op; the empty braces can be removed.)

- [ ] **Step 3: Build**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --no-restore
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run all existing tests**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj
```
Expected: All tests pass including `SlideRenderQueueTests`.

- [ ] **Step 5: Commit**

```powershell
git add HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs
git commit -m "feat: enqueue new slides for background render after UpdateStanzaSlides

Fix C: after building newSlides, detect fresh slides (Cached==null) and
enqueue via SlideRenderQueue. Covers initial load (all slides fresh) and
arrangement changes (only new slides added). Switch SongSlideInstance ctor
calls to named params so Text/Label set before subscriptions fire."
```

---

## Task 7: Fix `CustomSlideRender` async image loading (Fix D)

**Files:**
- Modify: `HandsLiftedApp.Core/Render/CustomSlide/CustomSlideRender.axaml.cs`

`CreateControlForElement` calls `BitmapLoader.LoadBitmap` synchronously on the UI thread when building the custom slide control tree. Change to fire-and-forget async: return the `Image` with null `Source` immediately, then set `Source` from a background load.

- [ ] **Step 1: Replace the synchronous image load in `CreateControlForElement`**

Find the block at line ~76-98:
```csharp
if (slideElement is ImageElement imageElement)
{
    Bitmap? imageData = null;
    try
    {
        if (!string.IsNullOrEmpty(imageElement.FilePath))
        {
            imageData = BitmapLoader.LoadBitmap(imageElement.FilePath);
        }
    }
    catch (Exception e)
    {
        Debug.WriteLine($"Error loading image: {e.Message}");
    }

    Image image = new Image()
    {
        Source = imageData,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Stretch = Stretch.Uniform,
        DataContext = slideElement,
        Width = imageElement.Width,
        Height = imageElement.Height,
    };
```

Replace with:
```csharp
if (slideElement is ImageElement imageElement)
{
    Image image = new Image()
    {
        Source = null,  // set async below; avoids blocking UI thread on disk I/O
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Stretch = Stretch.Uniform,
        DataContext = slideElement,
        Width = imageElement.Width,
        Height = imageElement.Height,
    };

    if (!string.IsNullOrEmpty(imageElement.FilePath))
    {
        _ = BitmapLoader.LoadBitmapAsync(imageElement.FilePath).ContinueWith(t =>
        {
            if (t.Result != null)
                Avalonia.Threading.Dispatcher.UIThread.Post(
                    () => image.Source = t.Result,
                    Avalonia.Threading.DispatcherPriority.Background);
        }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnRanToCompletion);
    }
```

- [ ] **Step 2: Build**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --no-restore
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```powershell
git add HandsLiftedApp.Core/Render/CustomSlide/CustomSlideRender.axaml.cs
git commit -m "perf: async image load in CustomSlideRender

Fix D: CreateControlForElement was calling BitmapLoader.LoadBitmap (sync, UI
thread) per ImageElement. Changed to LoadBitmapAsync fire-and-forget; Image
starts with null Source then fills in at Background priority."
```

---

## Task 8: Integration verification

- [ ] **Step 1: Run full test suite**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj
```
Expected: All tests pass.

- [ ] **Step 2: Build the full solution**

```powershell
dotnet build HandsLiftedApp.sln
```
Expected: Build succeeded.

- [ ] **Step 3: Run the app and load the playlist**

Launch `HandsLiftedApp.Desktop`. Open the playlist that previously caused the 7-second freeze. Observe:
- UI responds to mouse/keyboard within ~200ms of clicking "Open"
- Song items appear in the playlist list immediately (without thumbnails)
- Thumbnails fill in progressively in the background
- No UI freeze during or after load

- [ ] **Step 4: Verify edit path still works**

After load, click a song item. Edit the lyrics text. Confirm the slide thumbnail updates ~200ms after typing stops (the debounce in `RequestRender()` is still 200ms).

---

## Self-Review Notes

- **Fix A** also corrects pre-existing bug: `loadFilePath` vs `msg.FilePath` (autosave resolution was ignored on deserialize).
- **Task 6 Step 2** switches to named ctor params — this is required for Fix B to work correctly. The object initializer syntax would set Text AFTER subscriptions, defeating the Skip(1) suppression.
- `IRenderable` is defined in `SlideRenderQueue.cs` alongside the queue; both `SongSlideInstance` and `SongTitleSlideInstance` are in `HandsLiftedApp.Core` assembly so `internal` would work, but `public` interface is used to make the pattern explicit and testable.
- `Dispatcher.UIThread.Post(..., DispatcherPriority.Background)` — `Background` priority means property assignments run after all input events are processed, so thumbnails don't starve user interaction.
- `SlideRenderQueue` fire-and-forget calls (`Enqueue`, `EnqueueBatch`) swallow exceptions from background renders; add logging inside `Render()` calls if needed.
