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
