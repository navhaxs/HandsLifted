# VisionScreens — Claude Steering Notes

Church presentation software (Avalonia 11 / C# / SkiaSharp / libmpv).

---

## Architecture overview

- **MainWindow** — always open. Hosts `LivePane` which owns the `MotionBackgroundLayer`.
- **ProjectorWindow** — optional, can be hidden/closed. Uses `MotionBackgroundObserver`.
- **StageDisplayWindow** — optional. Uses `MotionBackgroundObserver`.
- `Globals.Instance.MainViewModel` — single app-wide `MainViewModel`.
- `Globals.Instance.MpvContextInstance` — shared `MpvContext` for regular video slides (`MpvVideoSlideRenderer`). Do not reuse for motion backgrounds.

---

## Slide rendering pipeline

Slides render via SkiaSharp, NOT Avalonia layout:

1. `SlideRenderSpec` — data model describing background + text elements
2. `SlideRenderer.Draw()` — static SkiaSharp drawing engine (no Avalonia dependency)
3. `SlideCanvas` — Avalonia control hosting a Skia surface; drives transitions via `Transition(spec, duration)`
4. `SongSlideSpecBuilder` / `SongTitleSlideSpecBuilder` — build `SlideRenderSpec` from runtime slide instances

`OnActiveSlideChanged` in `LivePane.axaml.cs` and `ProjectorWindow.axaml.cs` converts the active `Slide` to a `SlideRenderSpec` and calls `SlideCanvas.Transition(spec, TimeSpan)`.

### Slide type switch — must handle ALL cases explicitly

```csharp
SlideRenderSpec? spec = slide switch
{
    SongSlideInstance s      => SongSlideSpecBuilder.Build(s),
    SongTitleSlideInstance t => SongTitleSlideSpecBuilder.Build(t),
    ImageSlideInstance img   => ...,
    LogoSlide                => ...,   // ← synthetic; no file path; reads Playlist.LogoGraphicFile
    _                        => null,  // blank/custom AXAML slides
};
```

`LogoSlide` is a synthetic slide type returned when `Playlist.PresentationState == Logo`. It has no file path — the logo image comes from `_vm.Playlist.LogoGraphicFile`.

### Deleting or renaming a spec-builder class — grep the whole repo, not just your task's file list

A spec-builder (e.g. `SongSlideSpecBuilder`, `ScriptureSlideSpecBuilder`) is called from more than just the runtime slide's own `Render()` method — it's also called directly from **both** slide-type switches above (`LivePane.axaml.cs` and `ProjectorWindow.axaml.cs`). When replacing `ScriptureSlideSpecBuilder` with `ScriptureParagraphSpecBuilder`, a written implementation plan's file list missed these two call sites entirely; the build wouldn't have passed without also fixing them. Before deleting/renaming any builder class, run `grep -rn "OldBuilderName" --include=*.cs .` across the whole repo — don't trust a task's own file list to have enumerated every caller.

### Reactive runtime slide instances: reassigning a property doesn't always trigger a re-render on its own

When code **reuses/mutates an existing runtime slide instance** across a regeneration pass (rather than constructing a fresh one) — e.g. `ScriptureItemInstance.UpdatePages` reusing a `ScriptureSlideInstance` by page-index identity — don't assume reassigning a rendering-relevant property (like `Theme`) automatically gets the slide re-rendered. If the slide's own reactive subscriptions don't happen to fire for that specific reassignment, and the surrounding batch-render logic gates on `Cached == null` (as `EnqueueBatch` calls throughout this codebase do), a content-unchanged-but-theme-changed slide can silently keep its stale cached thumbnail. When mutating a reused instance's `Theme` (or any other property the render queue doesn't already watch), explicitly reset `Cached = null` on that instance so the existing `Cached == null` sweep picks it up for re-render.

---

## Motion background (libmpv)

### Ownership rule: ONE MotionBackgroundLayer

Only **one** `MotionBackgroundLayer` must exist at any time. It creates and owns the `MpvContext`. All other windows use `MotionBackgroundObserver`.

- `MotionBackgroundLayer` → in `LivePane.axaml` (MainWindow, always open)
- `MotionBackgroundObserver` → in `ProjectorWindow.axaml`, `StageDisplayLayout/DefaultLayout.axaml`

`MotionBackgroundLayer` publishes its active `MpvContext` via `MotionBackgroundService.ActiveContext` (a `BehaviorSubject<MpvContext?>`). Observers subscribe to this and set their `VideoView.MpvContext` accordingly.

### SoftwareVideoView primary/secondary model

`SoftwareVideoView` uses a static `SharedBitmaps` dictionary keyed by `MpvContext`:

- **Primary** (first view to connect) — calls `StartSoftwareRendering`, does `SoftwareRender()` to update the shared bitmap each frame.
- **Secondary** (subsequent views) — calls `RegisterUpdateCallback` only; reads the shared bitmap. Does NOT call `StartSoftwareRendering`.

**Critical**: `StartSoftwareRendering` always calls `StopRendering()` internally, which sends `Command("stop")` to mpv. **Never call `StartSoftwareRendering` on a context that is already playing.** Only the primary calls it, on a freshly created context.

### MpvContext lifecycle in MotionBackgroundLayer

```
CreateMotionMpvContext()
→ VideoView.MpvContext = ctx         // primary connects, StartSoftwareRendering
→ Command("loadfile", path)           // start playing
→ PublishActiveContext(ctx)           // observers connect, RegisterUpdateCallback only
```

On `StopPlayback`:
```
VideoView.MpvContext = null           // primary disconnects, StopRendering if last ref
Command("stop")
PublishActiveContext(null)            // observers disconnect asynchronously
DisposeContext(ref _motionMpvContext)
```

`StopRendering` and `UnregisterUpdateCallback` both guard against `disposed` state since observer disconnects may race with `DisposeContext`.

---

## avares:// path mangling

The playlist XML serializer converts `avares://Assembly/path` to a relative path on save. On deserialization, `ToAbsolutePath(playlistDir, relative)` produces a garbage Windows path like:

```
C:\VisionScreens Data\avares:\HandsLiftedApp.Core\Assets\DefaultTheme\logo-default.png
```

**Workaround** (applied in `OnActiveSlideChanged`):

```csharp
private static string? NormalizeMediaPath(string? path)
{
    if (string.IsNullOrWhiteSpace(path)) return path;
    var idx = path.IndexOf("avares:", StringComparison.OrdinalIgnoreCase);
    if (idx > 0)
    {
        var rest = path.Substring(idx + "avares:".Length)
                       .Replace('\\', '/')
                       .TrimStart('/');
        if (rest.Length == 0) return path;
        return "avares://" + rest;
    }
    return path;
}
```

**Root cause not fixed** — the serializer (`HandsLiftedDocXmlSerializer`) applies `ToRelativePath` to `avares://` paths it should leave alone. Needs fixing upstream.

`SlideRenderer.DrawBackground` handles `avares://` URIs via `AssetLoader.Open(new Uri(path))`.

---

## Avalonia binding gotchas

### Resolving the owning Window from a control inside a Flyout/MenuFlyout/Popup

`TopLevel.GetTopLevel(control)` does **not** cross a `Popup` boundary — Avalonia hosts `MenuFlyout`/`Flyout`/`ContextMenu` content in a `PopupRoot`, which is itself a `TopLevel` but is **not** a `Window`. So `TopLevel.GetTopLevel(menuItem) as Window` silently evaluates to `null` for any control inside such a popup, and code that early-returns on that null (rather than crashing) fails as a **silent no-op** — this exact bug shipped once in this codebase (a flyout `MenuItem`'s click handler that opened a dialog never actually opened it) and survived a full task-review cycle because no reviewer had a display to click through and confirm.

The fix that works reliably in this codebase: walk the **logical** tree via `.Parent` until you hit a `Window`, the same technique `ControlExtension.FindAncestor<T>` and `AddItemFlyoutResourceDictionary.axaml.cs`'s own `FindNearestDataContextAncestor` already use to cross this identical popup boundary successfully:

```csharp
Window? parentWindow = null;
Control? ancestor = menuItem;
while (ancestor != null)
{
    if (ancestor is Window w) { parentWindow = w; break; }
    ancestor = ancestor.Parent as Control;
}
```

Give a graceful fallback (e.g. a non-modal `.Show()`) for the `parentWindow == null` case rather than silently returning — `HandleAddItemButtonClick.ShowAddWindow` already does this.

If you add code that opens a dialog/window from inside a flyout or context menu, **you must actually click through it in a running app** (or have someone who can) before considering the task done — this class of bug produces zero compiler errors, zero test failures, and zero exceptions; the button just does nothing.

### ReactiveUI's no-selector `WhenAnyValue` caps at 7 properties

`this.WhenAnyValue(a, b, c, d, e, f, g)` (7 property expressions, no trailing selector) compiles; adding an 8th argument in the same no-selector form does not — this project's ReactiveUI version (20.1.1) has no no-selector overload beyond 7. Once you need to watch an 8th (or more) property in one subscription, add an explicit selector: `this.WhenAnyValue(a, b, c, d, e, f, g, h, (_1,_2,_3,_4,_5,_6,_7,_8) => Unit.Default)` — the selector-based overloads go higher. A no-op `Unit.Default` selector doesn't change the underlying combine-latest firing semantics; it only changes what the (usually discarded) emitted value is.

### Element-name path bindings don't track intermediate object changes

With `x:CompileBindings="False"`, runtime bindings like `{Binding #listBox.SelectedItem.FontSize}` do **not** reliably re-evaluate when `SelectedItem` changes. The binding sees the initial value but goes stale on selection changes.

**Fix pattern**: Set the editor panel's `DataContext` imperatively in a `SelectionChanged` handler, then use simple `{Binding FontSize}` in the editor controls.

```csharp
private void SyncEditorToSelection()
{
    var item = listBox.SelectedItem as MyModel;
    editorPanel.DataContext = item;
    // also manually sync any ComboBox SelectedValue/SelectedItem here
}
// Wire to: SelectionChanged, DataContextChanged, ItemsSource subscription
```

### Observing all property changes on a ReactiveObject

`WhenAnyPropertyChanged()` (ReactiveUI extension) may not be accessible from all projects. Use `ReactiveObject.Changed` instead — available directly on any `ReactiveObject`:

```csharp
// Subscribe to ALL property changes of a nested ReactiveObject, switching on object changes:
var innerChanges = outer
    .WhenAnyValue(o => o.InnerObject)
    .Select(inner => inner?.Changed.Select(_ => Unit.Default) ?? Observable.Never<Unit>())
    .Switch();
```

---

## SKBitmap / SkiaSharp gotchas

- **Never store a disposed measurement `SKTypeface` in a returned render element.** A spec-builder typically does `using var typeface = GetTypeface(theme);` to measure text (word-wrap, line width), then must call `GetTypeface(theme)` **again, fresh**, for every `RenderElement` it actually returns — reusing the `using`-scoped measurement instance means every returned element holds a typeface that's disposed the moment the builder method returns, and `SlideRenderer` constructs an `SKFont` from it at actual draw time. This exact bug shipped once (`ScriptureParagraphSpecBuilder`, before `SongSlideSpecBuilder`'s already-correct pattern was noticed) and was invisible to unit tests that only inspect the returned `SlideRenderSpec`'s structure rather than actually rendering it — in this project's pinned SkiaSharp build, `SKTypeface.FromFamilyName` happens to return a cached/shared instance and disposing it is inert, so even a test that renders to a bitmap can't prove the bug exists here; fix it anyway, since relying on that caching behavior would be fragile across SkiaSharp versions. `SongSlideSpecBuilder`'s existing comment ("Create a fresh SKTypeface per element (the measurement typeface is disposed above)") is the pattern to copy.
- `SKBitmap.Decode(string filename)` silently returns `null` on failure — no exception. Always null-check.
- Use stream-based decode with file-exists check + `avares://` handling:

```csharp
Stream? imgStream = path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
    ? AssetLoader.Open(new Uri(path))
    : File.OpenRead(path);
using (imgStream) { var bmp = SKBitmap.Decode(imgStream); ... }
```

---

## MpvContext instances — keep separate

| Context | Owner | Purpose |
|---|---|---|
| `Globals.Instance.MpvContextInstance` | App startup | Regular video slides (`MpvVideoSlideRenderer`) |
| Created by `MotionBackgroundService.CreateMotionMpvContext()` | `MotionBackgroundLayer` | Motion background video |

Do not share these. Each has different config (e.g. motion bg uses `loop-file=inf`, `aid=no`).

---

## A C# namespace does not tell you which project a file lives in

This codebase does not enforce folder-equals-namespace or namespace-equals-project. Several runtime classes physically live in `HandsLiftedApp.Core` (folder-wise, e.g. under `Core/Models/RuntimeData/Slides/`) but declare themselves in the `HandsLiftedApp.Data.Slides`/`HandsLiftedApp.Data.Models.*` **namespace**, matching their base class's namespace rather than their own project (`ScriptureSlideInstance`, `SongItemInstance`, etc.). Likewise `ScriptureAddDialog` physically lives under `Views/AddItem/` but declares `namespace HandsLiftedApp.Core.Views` (flat), matching its folder-sibling `AddItemWindow`'s own namespace rather than `.AddItem`.

**Why this matters:** the actual dependency-direction rule (e.g. "`HandsLiftedApp.Data` must never depend on `HandsLiftedApp.Core`") is governed by which **.csproj** a file is physically compiled into, not by its `namespace` line. Before adding a new property/type to an existing class, check the file's actual path (and that project's `.csproj` references) — don't infer safety from the namespace alone. This also means an existing `using` statement already covering a namespace may already cover a *new* file you're about to add in that same namespace, even if the new file lives in a different folder than files you'd expect that namespace to be in — check before adding a namespace you assume is missing.

## Key file locations

| File | Purpose |
|---|---|
| `HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs` | SkiaSharp drawing engine |
| `HandsLiftedApp.Core/Render/Skia/SlideCanvas.axaml.cs` | Avalonia Skia host control |
| `HandsLiftedApp.Core/Render/Skia/Builders/` | SongSlide / SongTitleSlide spec builders |
| `HandsLiftedApp.Core/Render/MotionBackground/MotionBackgroundLayer.axaml.cs` | libmpv owner — LivePane only |
| `HandsLiftedApp.Core/Render/MotionBackground/MotionBackgroundObserver.axaml.cs` | Secondary windows |
| `HandsLiftedApp.Core/Services/MotionBackgroundService.cs` | Context broadcast, create/dispose helpers |
| `Libraries/LibMpv/Avalonia.Controls.LibMpv/SoftwareVideoView.cs` | Avalonia libmpv renderer (primary/secondary) |
| `Libraries/LibMpv/src/LibMpv.Context/MpvContext.Rendering.cs` | Render context lifecycle |
| `HandsLiftedApp.Core/Views/LivePane.axaml.cs` | Slide subscription, spec building (MainWindow) |
| `HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs` | Slide subscription, spec building (projector) |
