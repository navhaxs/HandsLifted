# VisionScreens — Claude Steering Notes

Church presentation software (Avalonia 12 / C# / SkiaSharp / libmpv).

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
4. `SongSlideSpecBuilder` / `SongTitleSlideSpecBuilder` / `ScriptureParagraphSpecBuilder` / `CustomSlideSpecBuilder` — build `SlideRenderSpec` from runtime slide instances

`OnActiveSlideChanged` in `LivePane.axaml.cs` and `ProjectorWindow.axaml.cs` (and the stage display's `DefaultLayout.axaml.cs`) converts the active `Slide` to a `SlideRenderSpec` via `SlideSpecResolver.Resolve(slide, logoPath)` and calls `SlideCanvas.Transition(spec, TimeSpan)`.

### Slide type switch — one shared copy: `SlideSpecResolver.Resolve`

The `Slide -> SlideRenderSpec` switch lives in `HandsLiftedApp.Core/Render/Skia/Builders/SlideSpecResolver.cs` and is shared by `LivePane`, `ProjectorWindow`, `DefaultLayout` and `SlidePreloadService`. A new slide type is added **there, once**, and must be handled explicitly (the `_ => null` arm covers blank/custom AXAML slides only):

```csharp
SongSlideInstance s       => SongSlideSpecBuilder.Build(s),
SongTitleSlideInstance t  => SongTitleSlideSpecBuilder.Build(t),
ScriptureSlideInstance sc => ScriptureParagraphSpecBuilder.Build(sc),
ImageSlideInstance img    => ...,
LogoSlide                 => ...,   // synthetic; no file path; path from SlideSpecResolver.ResolveLogoPath(playlist)
CustomSlide cs            => CustomSlideSpecBuilder.Build(cs),
_                         => null,
```

`LogoSlide` is a synthetic slide type returned when `Playlist.PresentationState == Logo`. It has no file path — the logo image comes from `SlideSpecResolver.ResolveLogoPath(playlist)` (playlist logo, falling back to the app-wide default logo).

### Deleting or renaming a spec-builder class — grep the whole repo, not just your task's file list

A spec-builder (e.g. `SongSlideSpecBuilder`, `ScriptureParagraphSpecBuilder`) is called from more than `SlideSpecResolver`: runtime slide instances call it from their own `Render()` (e.g. `SongSlideInstance`, `SongItemInstance`) and designer views call it directly (e.g. `SongSlideView`). When `ScriptureSlideSpecBuilder` was replaced by `ScriptureParagraphSpecBuilder`, a written implementation plan's file list missed call sites outside the files the plan named, and the build wouldn't have passed without fixing them. Before deleting/renaming any builder class, use Grep for the old name across the whole repo (scoped to the project directory, and ignoring `.claude/worktrees/`) — don't trust a task's own file list to have enumerated every caller.

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

### Theme background opacity over a motion video

`BaseSlideTheme.BackgroundOpacity` (0-100) blends the theme's image/video over `BackgroundColour`. For a **motion video** this is NOT done by changing the video layer's opacity (that layer is owned by `MotionBackgroundLayer` and sits below the canvas): `ThemeBackgroundSpecBuilder` returns `SolidBackground(colour, alpha = 1 - opacity)` so the canvas paints the colour *over* the video (source-over = `colour*(1-o) + video*o`). At 100 it returns `TransparentBackground`, as before. Images/video-frame thumbnails instead carry `BackColor` + `Opacity` on `ImageBackground`/`SkiaBitmapBackground`.

## avares:// path mangling

The playlist XML serializer converts `avares://Assembly/path` to a relative path on save. On deserialization, `ToAbsolutePath(playlistDir, relative)` produces a garbage Windows path like:

```
C:\VisionScreens Data\avares:\HandsLiftedApp.Core\Assets\DefaultTheme\logo-default.png
```

**Repair for already-saved files**: `SlideSpecResolver.NormalizeMediaPath` (public static, applied by `ResolveLogoPath`) turns a mangled path back into `avares://Assembly/path`. Keep it: playlists saved before the fix still contain mangled paths.

**Root cause is fixed for new saves** — `RelativeFilePathResolver.ToRelativePath` / `ToAbsolutePath` now leave `avares://` URIs alone (`IsAvaresUri` guard). Anything that stores a media path and might hold an `avares://` URI must go through those resolvers, not hand-rolled `Path` logic.

`SlideRenderer.DrawBackground` handles `avares://` URIs via `AssetLoader.Open(new Uri(path))`.

---

## Avalonia 12 API traps (the app is on Avalonia 12.1.1; full history in `AVALONIA_12_UPGRADE_NOTES.md`)

Version lives in `Directory.Build.props` (`AvaloniaVersion`). Before bumping, check the third-party package floors listed in the upgrade notes. Things that compile in older Avalonia/blog-post code but not here:

- **`VisualRoot` is not the `Window`.** It resolves to an internal `TopLevelHost`, so `(Window)VisualRoot` throws at runtime with no compile error. Use `TopLevel.GetTopLevel(visual)` (or `this` when you are the window). Code outside a `Visual` subclass can't read `VisualRoot` at all.
- **Drag and drop** uses `DataTransfer` / `DataTransferItem` / `DataFormat` (`DataObject`, `DataFormats` are error-stubs); `DragDrop.DoDragDropAsync` needs the original `PointerPressedEventArgs`, not a later `PointerMoved` one.
- **Compiled bindings are stricter:** a view with `{Binding ...}` needs `x:DataType` (or `x:CompileBindings="False"`).
- Renamed or removed: `Window.SystemDecorations` -> `WindowDecorations` (also as an AXAML attribute), `ExtendClientAreaChromeHints` (removed), `PlacementMode` -> `Placement` on `Popup`/`ContextMenu`, `RadioButton.Checked`/`Unchecked` -> `IsCheckedChanged`, `OpenFileDialog` -> `StorageProvider.OpenFilePickerAsync`, `ItemContainerGenerator.IndexFromContainer/ContainerFromIndex` -> same methods on `ItemsControl`, `GotFocusEventArgs` -> `FocusChangedEventArgs`, clipboard `GetTextAsync` -> `TryGetTextAsync`.
- Package/namespace renames: `ReactiveUI.Avalonia` (namespace `ReactiveUI.Avalonia`, `UseReactiveUI(_ => { })`), `Xaml.Behaviors.*` (the C# namespaces did **not** change, only the `assembly=` in AXAML xmlns), `PanAndZoom`. `DisposeWith` now needs `System.Reactive.Disposables.Fluent`.
- Windows draw their own chrome (`WindowDrawnDecorations`); custom title bars mark interactive regions with `WindowDecorationProperties.ElementRole="DecorationsElement"`.
- A full non-incremental build after a merge is the only reliable check: errors surface in waves as each project first compiles.

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

`this.WhenAnyValue(a, b, c, d, e, f, g)` (7 property expressions, no trailing selector) compiles; adding an 8th argument in the same no-selector form did not on ReactiveUI 20.1.1. The project is now on ReactiveUI 23.2.28 (the Avalonia 12 upgrade forced it) and this cap has **not been re-verified** on 23.x — the selector form below is safe either way. Note also `RxApp.MainThreadScheduler` became `RxSchedulers.MainThreadScheduler` in 23.x. Once you need to watch an 8th (or more) property in one subscription, add an explicit selector: `this.WhenAnyValue(a, b, c, d, e, f, g, h, (_1,_2,_3,_4,_5,_6,_7,_8) => Unit.Default)` — the selector-based overloads go higher. A no-op `Unit.Default` selector doesn't change the underlying combine-latest firing semantics; it only changes what the (usually discarded) emitted value is.

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

## Undo / redo (`HandsLiftedApp.Core/Undo/`)

One `UndoStack` per **scope**: the playlist (`PlaylistInstance.UndoStack`, Edit menu + Ctrl+Z in `MainView`) and one per editor window (song editor, each custom slide inside a slide editor window, theme designer). Stacks never mix. UI thread only. A failed undo/redo clears that stack (a half-applied history is worse than none).

**Which mechanism to use for a new edit**

| Edit | Use |
|---|---|
| A command with a call site (add/delete/move) | `stack.Execute(new XAction(...))` |
| A value a two-way binding writes (title box, slider, checkbox) | add it to the allow-list in `PlaylistPropertyRecorder` (playlist items) — it records old→new and merges typing/slider ticks |
| An editor window with many ways to edit one object | a snapshot recorder: `SongEditRecorder`, `CustomSlideEditRecorder`, `ThemeEditRecorder` diff a whole-state snapshot and record before→after |
| Several steps that are one user action | `stack.BeginTransaction(...)` / the recorder's `BeginBatch(...)` |

**Rules learned the hard way**
- Items are found again **by UUID**, never by object: undoing a delete rebuilds a *fresh* instance from an XML snapshot (`PlaylistItemSnapshot`), so any step holding the old instance is wrong.
- Slides inside a group are found by **position** (`GroupSlideEdits`). Anything that replaces a group's whole `Items` list outside undo (Sync / re-import, group editor "Load") drops that group's position-based steps automatically (`PlaylistPropertyRecorder` → `UndoStack.DiscardStepsFor`). Undo/redo must edit `Items` in place and never swap the collection, or it would drop its own history. `MediaGroupItem` raises `Items` changed for *every* in-place edit, so compare the collection reference, not the property name.
- Object-initializer assignments made after construction **are** recorded as edits: tests must `UndoStack.Clear()` after setup.
- Snapshot comparison must ignore derived views. `BaseSlideTheme`'s `Calculated*` properties update asynchronously (and `BaseSlideTheme.CopyFrom` must skip them or it resets font weights) — never compare XML or all public properties of a theme.
- Collections owned by a shared library object (song stanzas, theme list) are mutated in place on undo so the library's own change handlers and autosave fire.
- Tests that touch `ReactiveObject`s with `ObserveOn(MainThreadScheduler)` pipelines should run inside `DispatcherTestThread.Run(...)`; otherwise the test dispatcher thread delivers derived-property notifications concurrently and a notification can occasionally be lost. Tests must not start real imports: `PlaylistItemSnapshot.StartImport` is stubbed in the assembly init.

**Deliberately NOT undoable** (don't "fix" these without a design discussion)
- Sync / re-import of PDF, PowerPoint, Google Slides and online-video items, and "Change PowerPoint file" (they refresh from an external source; generated files may be overwritten). Their effect on history is only to drop that group's position-based steps.
- Group editor "Load" (replaces slides from a file) — same treatment as a re-import.
- Files copied into the media library / playlist folder when media is added: undo removes the item, the file stays on disk.
- Downloads (online video), the Setup window, and app preferences other than the default song/scripture themes ("Set as default" in the theme designer is undoable).
- Song/theme **library file** operations (creating, renaming, deleting library files directly). Edits to a song or theme's *contents* are undoable.
- In the playlist only a song's **title and theme** are recorded (while a song editor window has that song open, its edits go to the window's history instead — `SongEditRecorder.IsEditing`). Every other song edit happens in the song editor window, which has its own history.
- Presentation state (Logo/Blank/Slides toggles) and the selected slide.

Restored items have "save and reload" fidelity: generated slides are regenerated, and the selected slide inside the item resets. The Edit menu can't undo across a playlist load/new (history is cleared there).

---

## A C# namespace does not tell you which project a file lives in

This codebase does not enforce folder-equals-namespace or namespace-equals-project. Several runtime classes physically live in `HandsLiftedApp.Core` (folder-wise, e.g. under `Core/Models/RuntimeData/Slides/`) but declare themselves in the `HandsLiftedApp.Data.Slides`/`HandsLiftedApp.Data.Models.*` **namespace**, matching their base class's namespace rather than their own project (`ScriptureSlideInstance`, `SongItemInstance`, etc.). Likewise `ScriptureAddDialog` physically lives under `Views/AddItem/` but declares `namespace HandsLiftedApp.Core.Views` (flat), matching its folder-sibling `AddItemWindow`'s own namespace rather than `.AddItem`.

**Why this matters:** the actual dependency-direction rule (e.g. "`HandsLiftedApp.Data` must never depend on `HandsLiftedApp.Core`") is governed by which **.csproj** a file is physically compiled into, not by its `namespace` line. Before adding a new property/type to an existing class, check the file's actual path (and that project's `.csproj` references) — don't infer safety from the namespace alone. This also means an existing `using` statement already covering a namespace may already cover a *new* file you're about to add in that same namespace, even if the new file lives in a different folder than files you'd expect that namespace to be in — check before adding a namespace you assume is missing.

---

## Tooling rule — no `find`/`grep` shellouts

Never run `find` or `grep` via Bash/PowerShell in this repo — Git-Bash `find.exe` over `.git`/`packages`/`bin`/`obj` runs long, stacks up, and eats CPU (multiple orphaned `find.exe` seen at 6%+ CPU each). Use Glob (file search) and Grep (content search) tools instead, always. Always scope `path` to project dir (or narrower subfolder) — never search unscoped from drive root or default cwd. The repo root also contains `.claude/worktrees/*` full checkouts: a root-scoped Glob/Grep returns every hit twice (once per worktree) — scope to `HandsLiftedApp.Core`, `HandsLiftedApp.Data`, etc. or add a glob that excludes `.claude`, and never edit a file under `.claude/worktrees/` by mistake.

## Custom slide editor

- **One drawer.** `CustomSlideDrawer` (with `CustomSlideTextLayout` for text layout and `SlideImageCache` for decoded images) draws custom slides for BOTH the projector (`CustomSlideSpecBuilder`) and the editor (`CustomSlideEditorCanvas`). Fix rendering bugs there, never in a second copy. `CustomSlideRender` (Avalonia controls) is only for slide-list thumbnails and the stage display's next-slide view (via `SlideRender`), never the projector or editor.
- **Proxy borders.** The designer draws nothing itself: `Root` holds a transparent `Border` per element so `SelectedAdorner` (drag/resize handles) keeps working; the adorner adorns a proxy, not a drawn control. Proxy rects are bound two-way to the element.
- **Z-order = list order, index 0 frontmost**. Bring forward / Send backward swap list positions.
- **Inline text editing.** `InlineTextEditor` is an overlay `TextBox` that commits `Text` exactly once (commit, Esc, LostFocus or detach); while it is open the canvas hides that element's text (`HiddenTextElement`). Don't write `Text` per keystroke - that breaks undo (one step per edit). The element stays selected while typing (outline + resize handles stay up); only the adorner's move area (`SelectedAdorner.BodyHitTestable`) goes click-through so clicks reach the box. A rebuilt adorner must get the same flag at creation (`SelectionBodyHitTestableFor`). The designer's `ZoomBorder` has `EnableDoubleClickZoom="False"`: its default double-click zoom (2x) otherwise fires together with the double-click that starts editing.
- **Toolbar buttons must stay `Focusable="False"`** (set per button and in the style), or clicking Bold etc. steals focus from the inline box and ends the edit.
- **Keyboard** lives in `CustomSlideDesigner.HandleKey` and only fires with canvas focus (not after clicking a layers-list row).
- **Headless tests** of these controls use `HandsLiftedApp.Tests/TestSupport/AvaloniaTextRendering.cs`, whose cursor-factory stub is found by reflection on an Avalonia designer-support type: expect it to break on an Avalonia upgrade.
- Manual click-through list: `docs/superpowers/plans/2026-11-22-custom-slide-editor-manual-checklist.md`.

## Key file locations

| File | Purpose |
|---|---|
| `HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs` | SkiaSharp drawing engine |
| `HandsLiftedApp.Core/Render/Skia/SlideCanvas.cs` | Avalonia Skia host control (code-only, no AXAML) |
| `HandsLiftedApp.Core/Render/Skia/Builders/` | Song / SongTitle / Scripture / Custom slide spec builders |
| `HandsLiftedApp.Core/Render/Skia/Builders/SlideSpecResolver.cs` | The one shared `Slide -> SlideRenderSpec` switch, logo path resolution, `avares://` repair |
| `HandsLiftedApp.Core/Undo/` | `UndoStack`, recorders (`PlaylistPropertyRecorder`, `SongEditRecorder`, `CustomSlideEditRecorder`, `ThemeEditRecorder`) and actions |
| `HandsLiftedApp.Tests/TestSupport/DispatcherTestThread.cs` | Run `ReactiveObject` tests on a dispatcher thread |
| `HandsLiftedApp.Core/Render/MotionBackground/MotionBackgroundLayer.axaml.cs` | libmpv owner — LivePane only |
| `HandsLiftedApp.Core/Render/MotionBackground/MotionBackgroundObserver.axaml.cs` | Secondary windows |
| `HandsLiftedApp.Core/Services/MotionBackgroundService.cs` | Context broadcast, create/dispose helpers |
| `Libraries/LibMpv/Avalonia.Controls.LibMpv/SoftwareVideoView.cs` | Avalonia libmpv renderer (primary/secondary) |
| `Libraries/LibMpv/src/LibMpv.Context/MpvContext.Rendering.cs` | Render context lifecycle |
| `HandsLiftedApp.Core/Views/LivePane.axaml.cs` | Slide subscription, spec building (MainWindow) |
| `HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs` | Slide subscription, spec building (projector) |
| `HandsLiftedApp.Core/Render/Skia/Builders/CustomSlideDrawer.cs` | The one custom-slide drawer (projector + editor canvas) |
| `HandsLiftedApp.Core/Render/Skia/CustomSlideEditorCanvas.cs` | Editor canvas; draws via `CustomSlideDrawer` |
| `HandsLiftedApp.Core/Views/Editors/MediaGroupItemEditor/SlideItemEditor/Parts/CustomSlideDesigner.axaml.cs` | Designer: selection proxies, keyboard (`HandleKey`), zoom |
| `HandsLiftedApp.Core/Views/Editors/MediaGroupItemEditor/SlideItemEditor/Parts/CustomSlideToolbar.axaml` | Editor toolbar |
| `HandsLiftedApp.Core/Views/Editors/MediaGroupItemEditor/SlideItemEditor/Parts/InlineTextEditor.cs` | Overlay TextBox for in-canvas text editing |
| `HandsLiftedApp.Core/Views/Editors/SnapCalculator.cs` | Drag snapping |
| `HandsLiftedApp.Core/Views/Editors/MediaGroupItemEditor/SlideItemEditor/SlideElementCommands.cs` | Add/Duplicate/Delete/Bring/Send/Nudge |
