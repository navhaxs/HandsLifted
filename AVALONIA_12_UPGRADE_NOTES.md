# Avalonia 11.3.18 → 12.0.5 upgrade — handoff notes

**Status: WIP, does not build yet.** Checkpointed for a fresh session to continue.

## Where this lives

- Worktree: `C:\Users\Jeremy\RiderProjects\HandsLifted\.claude\worktrees\avalonia-12-upgrade`
- Branch: `worktree-avalonia-12-upgrade`
- Latest commit: `0f840af` — "wip: Avalonia 12.0.5 upgrade (does not build yet)"
- Base: branched from local `master` (which was already at Avalonia 11.3.18 — see commit `f7c695c` "build: bump Avalonia to 11.3.18, patch vulnerable transitive deps" for unrelated prior security-patch context).

## How to verify progress

```bash
cd C:/Users/Jeremy/RiderProjects/HandsLifted/.claude/worktrees/avalonia-12-upgrade
dotnet restore HandsLiftedApp.sln          # should be clean already
dotnet build HandsLiftedApp.Desktop/HandsLiftedApp.Desktop.csproj -c Release
```

Only `HandsLiftedApp.Desktop` (the real shipped app and its dependency chain: Core, Controls, Data, Desktop, Importer.OnlineSongLyrics, GoogleSlidesImporter, LibMpv) has been in scope. Demo/Sample/Scratch projects under `Demos/`, `Libraries/vlcsharpavalonia/samples/`, `Libraries/LibMpv/samples/`, `ScratchApp`, `SlideEditorStandalone` etc. were touched only incidentally (mechanical renames applied repo-wide where cheap) — they are NOT verified to build and are out of scope unless the user asks otherwise.

**Important workflow lesson learned this session**: errors surface incrementally. Fixing one file's compile errors often lets MSBuild reach further into the dependency graph and reveal a *new* batch of errors in files that were never reached before (e.g. `RxApp` went from 54 errors in 4 files → 74 errors in 21 files after fixing an unrelated blocker upstream). **Don't assume a category is "done" after grep finds zero remaining hits in files that already failed to compile — re-grep the whole repo after every build/fix round**, since fixing an early-project error changes which later projects/files even get compiled.

## Why 12.0.5 specifically

The user picked 12.0.5, and it turned out to be exactly right: several third-party packages this app depends on were renamed for Avalonia 12 support, and they converge on requiring **exactly** `Avalonia >= 12.0.5` (e.g. `Xaml.Behaviors.Avalonia` 12.0.5 requires `Avalonia >= 12.0.5` precisely). Do not casually bump past 12.0.5 (e.g. to the newer 12.1.0) without re-checking these package floors first.

## Phase 1 — package/version swaps (DONE, verified via clean `dotnet restore`)

`Directory.Build.props`: `AvaloniaVersion` → `12.0.5`.

`Directory.Packages.props` changes:
| Old package | New package | Version | Note |
|---|---|---|---|
| `Avalonia.ReactiveUI` | `ReactiveUI.Avalonia` | `12.0.3` | Moved from AvaloniaUI org to reactiveui org. Needs `Avalonia >= 12.0.4`. |
| `Avalonia.Xaml.Behaviors` | `Xaml.Behaviors.Avalonia` | `12.0.5` | Same author (Wiesław Šoltés), renamed family. |
| `Avalonia.Xaml.Interactions` | `Xaml.Behaviors.Interactions` | `12.0.5` | ” |
| `Avalonia.Xaml.Interactivity` | `Xaml.Behaviors.Interactivity` | `12.0.5` | ” — this is the one with `Behavior<T>` base class. |
| `Avalonia.Controls.PanAndZoom` | `PanAndZoom` | `12.0.0.1` | Same author, dropped `Avalonia.Controls.` prefix. |
| `Avalonia.Controls.Skia` | *(removed)* | — | Confirmed zero source usage in `HandsLiftedApp.XTransitioningContentControl` — was vestigial, just deleted the `PackageReference`. |
| `Avalonia.Diagnostics` | *(kept as-is for now)* | `11.3.18` | No 12.x release. Official successor is `AvaloniaUI.DiagnosticsSupport`, but it's an out-of-process devtools bridge with a **different API** than `this.AttachDevTools()` — NOT a drop-in. `AttachDevTools()` is called in ~13 files (`AboutWindow`, `MessageWindow`, confirmation windows, etc.) — all Debug-only via `Condition="'$(Configuration)' == 'Debug'"` on the PackageReference. Since Avalonia.Diagnostics 11.3.18 declares `Avalonia >= 11.3.18` and our core is now 12.0.5 (higher), NuGet restore accepts it, but this is **untested** — it may or may not actually work/load at runtime against a 12.x core, and hasn't been build-tested since Debug config wasn't built this session (only `-c Release`, which excludes this package by the `Condition`). **Build a Debug config and check `AttachDevTools()` still works before shipping**, or port to `AvaloniaUI.DiagnosticsSupport` properly.
| `Material.Icons.Avalonia` | *(same package)* | `3.0.2` | Stable release supports `Avalonia >= 12.0.0` (an earlier pass mistakenly thought only a nightly existed — 3.0.2 is real/stable). |
| `AsyncImageLoader.Avalonia` | *(same package)* | `3.8.0` | Supports `Avalonia >= 12.0.0`. |
| `Avalonia.AvaloniaEdit` | *(same package)* | `12.0.0` | Only used by `HandsLifted.FetchBible` (small standalone tool, not the main app). |
| `Avalonia.Controls.DataGrid` | *(same package)* | `12.0.1` | Only used by `ScratchApp` (demo). |
| `ReactiveUI` (core, transitively pulled) | | `23.2.28` | Forced up from `18.3.1` because `ReactiveUI.Avalonia` 12.0.3 requires it. **5 major version jump** — see "Biggest open risk" below. |
| `DynamicData` | | `9.4.31` | Forced up from `7.9.1` (ReactiveUI 23.x requires `>= 9.4.31`). |
| `System.Reactive` | | `6.1.0` | Forced up from `5.0.0`. |
| `Splat` | *(new, added)* | `19.4.1` | New transitive dependency of ReactiveUI 23.x, wasn't previously pinned centrally. |
| `SkiaSharp` | | `3.119.4` | Forced up from `2.88.1` — Avalonia.Skia 12.0.5 requires `SkiaSharp 3.119.4` exactly. See `BitmapUtils.cs` issue below, this is clearly the cause. |

Per-project `PackageReference` `Include=` renames applied everywhere the old id was referenced (repo-wide, including demo/sample projects, since it's a cheap mechanical rename): `Avalonia.ReactiveUI`→`ReactiveUI.Avalonia`, `Avalonia.Xaml.Behaviors`→`Xaml.Behaviors.Avalonia`, `Avalonia.Xaml.Interactions`→`Xaml.Behaviors.Interactions`, `Avalonia.Xaml.Interactivity`→`Xaml.Behaviors.Interactivity`, `Avalonia.Controls.PanAndZoom`→`PanAndZoom` (only in `HandsLiftedApp.Core.csproj`), `Avalonia.Controls.Skia` removed (only in `HandsLiftedApp.XTransitioningContentControl.csproj`).

## Phase 2 — code migration (PARTIAL)

### Fully fixed subsystems (verified compiling, don't re-touch)

**Drag-and-drop** (biggest single piece, ~9 files): `DataObject`/`DataFormats` are hard-removed in Avalonia 12 (`[Obsolete(error: true)]` stub classes). Replaced with the new `DataTransfer`/`DataFormat`/`DataTransferItem` model:
- `new DataObject()` → `new DataTransfer()`
- `dragData.Set(format, value)` → `dragData.Add(DataTransferItem.Create(format, value))`
- `DragDrop.DoDragDrop(triggerEvent, data, effects)` (sync) → `await DragDrop.DoDragDropAsync(triggerEvent, data, effects)` (async) — **and `triggerEvent` must now be the actual `PointerPressedEventArgs`, not any later `PointerEventArgs`** (e.g. from a `PointerMoved` handler). Where drag-start logic lived in a `PointerMoved` handler (common "drag threshold" pattern), had to add a field to capture and store the original `PointerPressedEventArgs` from the `PointerPressed` handler and thread it through instead (see `SlideThumbnailBehavior.cs` for the pattern).
- `DataFormats.Text`/`.Files`/`.FileNames` (static string-ish constants, all obsolete-error now) → `DataFormat.Text` / `DataFormat.File` (note: singular now, no plural `.Files`) — for custom in-process payloads (e.g. `SlideDragDropCustomDataFormat`, `MediaGroupItem.GroupItem`), use `DataFormat.CreateInProcessFormat<T>("identifier")` where `T : class`.
- `e.Data` (on `DragEventArgs`) → `e.DataTransfer`, typed `IDataTransfer` not `IDataObject`.
- `.Contains(format)` still exists (extension method).
- `.Get(format)` → `.TryGetValue(format)` (nullable return).
- `.GetText()` → `.TryGetText()`. `.GetFiles()` → `.TryGetFiles()`. `.GetFileNames()` (was already obsolete pre-v12, returned raw path strings) is fully gone — if still needed, map `TryGetFiles()` results through `.Path.LocalPath`.
- Files touched: `SlideDragDropCustomDataFormat.cs`, `SlideThumbnailBehavior.cs`, `ItemSlidesView.axaml.cs`, `ItemOrderListView.axaml.cs`, `LivePane.axaml.cs`, `LibraryPaneView.axaml.cs`, `LibraryQueryView.axaml.cs`, `MediaGroupItemEditor.axaml.cs`, `AddItemButton.axaml.cs` (this last one's `SetupDnd` factory was already dead/commented-out code, just needed to compile).

**Clipboard**: `IClipboard.GetTextAsync()` → `TryGetTextAsync()` (extension method in `Avalonia.Input.Platform`, needs that `using`). `SetTextAsync` name unchanged. Fixed in `BrowserWindow.axaml.cs`.

**Window decorations**: `Window.SystemDecorations` property (type `SystemDecorations` enum) → `Window.WindowDecorations` property (type `WindowDecorations` enum, same values `None`/`BorderOnly`/`Full`). Fixed in `NativeVideoView.cs`. **Two more sites still need this same fix** — see remaining work below.

**Focus events**: `GotFocusEventArgs` → `FocusChangedEventArgs` (same `Avalonia.Input` namespace). Fixed in `TextBoxToggleButton.axaml.cs`.

**Compiled bindings**: `ImportGoogleSlidesWindow.axaml` bound `{Binding ImportId}` with `DataContext = this` in code-behind but no `x:DataType` — Avalonia 12 is stricter about this (AVLN2100). Added `x:DataType="local:ImportGoogleSlidesWindow"` with a `clr-namespace` xmlns.

**Item container index lookup**: `someItemsControl.ItemContainerGenerator.IndexFromContainer(x)` → `someItemsControl.IndexFromContainer(x)` (moved directly onto `ItemsControl`, `ItemContainerGenerator` class trimmed down significantly). Fixed in `DragControlBehavior.cs`, `StanzaVerticalDragControlBehavior.cs`, `ItemOrderListView.axaml.cs`, `MediaGroupItemEditor.axaml.cs`. **One more site (`ContainerFromIndex`, not `IndexFromContainer`) still needs this** — see below.

**VisualRoot access**: `Visual.GetVisualRoot()` extension method is now `internal` (inaccessible). `Visual.VisualRoot` property is `protected internal` (only accessible via `this`/own-subclass access, not via a differently-typed field/parameter). Any code accessing another control's visual root from a class that does NOT itself inherit `Visual` (e.g. our custom `Behavior<Control>` subclasses) must use the public `TopLevel.GetTopLevel(visual)` static method instead. Fixed in `DragControlBehavior.cs`, `StanzaDragControlBehavior.cs`, `StanzaVerticalDragControlBehavior.cs`, `HandleAddItemButtonClick.cs`, and the `RenderScaling` equivalent in `OpenGlVideoView.cs` / `SoftwareVideoView.cs` (Android sample) since `TopLevel.RenderScaling` replaced the old `Visual.VisualRoot!.RenderScaling` pattern for the same accessibility reason. **Classes that DO inherit `Visual`/`Control` (e.g. the real `Avalonia.Controls.LibMpv/SoftwareVideoView.cs`, `NativeVideoView.cs`, `XTransitioningContentControl.cs`, `MainWindow.axaml.cs`, `StartView.axaml.cs`) can keep using bare `VisualRoot`/`this.VisualRoot` — verified these did NOT error, don't "fix" them, they're already fine.**

**ReactiveUI.Avalonia namespace**: package rename also means namespace rename: `using Avalonia.ReactiveUI;` → `using ReactiveUI.Avalonia;` (repo-wide, ~14 files). Also `.UseReactiveUI()` (parameterless) no longer exists — the only overload now is `UseReactiveUI(Action<ReactiveUIBuilder> withReactiveUIBuilder)`, so all call sites became `.UseReactiveUI(_ => { })` to preserve prior default behavior. `ReactiveWindow<T>` still exists under the new namespace, confirmed used for real in `MainWindow.axaml.cs` and `AddItemWindow.axaml.cs` (the actual app main window!).

**RxApp** (partial — 4 of ~21 files done): ReactiveUI 23.x moved `RxApp.MainThreadScheduler`/`RxApp.TaskpoolScheduler` to a new `RxSchedulers` static class (same `ReactiveUI` namespace, no new `using` needed). Confirmed via ReactiveUI GitHub source at tag `23.2.28`: `src/ReactiveUI/RxSchedulers.cs` exists, `src/ReactiveUI/RxApp.cs` does not. Mechanical fix: `RxApp.MainThreadScheduler` → `RxSchedulers.MainThreadScheduler`. **Already fixed** in `SongItem.cs`, `SlideElement.cs`, `BaseSlideTheme.cs`, `BrowserWindowViewModel.cs`. **17 more files still reference `RxApp`** (see remaining work).

## Remaining work (not yet started/finished)

Run this after each fix round to re-scope (don't trust old counts, see the "workflow lesson" above):

```bash
grep -rln "RxApp\." --include="*.cs" . | grep -v obj/
grep -rln "OpenFileDialog" --include="*.cs" . | grep -v obj/
grep -rln "ExtendClientAreaChromeHints" --include="*.cs" --include="*.axaml" . | grep -v obj/
grep -rn "e\.Data\.\|DataFormats\." --include="*.cs" . | grep -v obj/
grep -rn "SystemDecorations" --include="*.cs" . | grep -v obj/
grep -rn "ItemContainerGenerator\." --include="*.cs" . | grep -v obj/
```

1. **`RxApp.*` — 17 more files** (of 21 total found this session). Same mechanical fix as the 4 already done: `RxApp.MainThreadScheduler`/`RxApp.TaskpoolScheduler` → `RxSchedulers.MainThreadScheduler`/`RxSchedulers.TaskpoolScheduler`. Also check for `RxApp.SuspensionHost` (3 occurrences found repo-wide) — this one's replacement wasn't researched yet, check `src/ReactiveUI/RxSuspension.cs` in the ReactiveUI 23.2.28 source (`https://raw.githubusercontent.com/reactiveui/ReactiveUI/23.2.28/src/ReactiveUI/RxSuspension.cs`) for the new home of suspension-host state.

2. **`OpenFileDialog` — 12 files, BIGGEST remaining item**. Confirmed fully removed from Avalonia 12 source tree (was already obsolete pre-v12 in favor of `StorageProvider`). This is a **real API model change**, not a rename: old `OpenFileDialog` was a sync-ish dialog class returning `string[]` paths; new pattern is `await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { ... })` returning `IReadOnlyList<IStorageFile>`. Each of the 12 call sites needs individual review (options like filters/multi-select/title need re-mapping, and callers need to become `async`/await the result). Files: `HandsLiftedApp.Core/Controls/TextBoxFilePathPicker.axaml.cs`, `HandsLiftedApp.Core/ViewModels/AddItem/AddItemViewModel.cs`, `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`, `HandsLiftedApp.Core/Views/AddItem/AddItemWindow.axaml.cs`, `HandsLiftedApp.Core/Views/AddItem/Pages/StartView.axaml.cs`, `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs`, `HandsLiftedApp.Core/Views/Editors/GroupItemsEditor.axaml.cs`, `HandsLiftedApp.Core/Views/Editors/SongEditorWindow.axaml.cs`, `HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml.cs`, `HandsLiftedApp.Core/Views/MainWindow.axaml.cs`, plus `Demos/PowerPointConverterSampleApp/MainWindow.axaml.cs` and `Libraries/vlcsharpavalonia/samples/.../Example2ViewModel.cs` (both out-of-scope demos).

3. **`ExtendClientAreaChromeHints` attribute — 15 files, mostly `.axaml`**. This property is fully removed (checked `Window.cs` source, no trace). Same fix already applied to `BrowserWindow.axaml`: just delete the attribute (its closest equivalent, showing system chrome, is close enough to default behavior — a minor title-bar cosmetic difference to revisit visually later, not worth blocking on). Files: `AboutWindow.axaml`, `AddItemWindow.axaml`, `DeleteConfirmationWindow.axaml`, `ExitConfirmationWindow.axaml`, `GoogleSlidesReauthWindow.axaml`, `NewSongUnsavedConfirmationWindow.axaml`, `RenameDialog.axaml`, `RestoreAutosaveConfirmationWindow.axaml`, `UnsavedChangesConfirmationWindow.axaml`, `MainWindow.axaml` (+ `.cs`), `MessageWindow.axaml`, `Setup/SetupWindow.axaml`, plus 2 out-of-scope files (`HandsLiftedApp.SongSelectImporter/MainWindow.axaml`, `ScratchApp/MainWindow.axaml`).

4. **`SystemDecorations` — 2 more sites** (in-scope: `HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs:264`; out-of-scope demo: `Libraries/vlcsharpavalonia/src/LibVLCSharp.Avalonia/NativeVideoPresenter.cs`). Exact line already found: `this.SystemDecorations = isRequestingFullscreen ? SystemDecorations.None : SystemDecorations.Full;` → same fix as `NativeVideoView.cs`: `this.WindowDecorations = isRequestingFullscreen ? WindowDecorations.None : WindowDecorations.Full;`.

5. **`ItemContainerGenerator.ContainerFromIndex` — 1 file** (`HandsLiftedApp.Core/Views/PlaylistSlidesView.axaml.cs`). Same pattern as `IndexFromContainer` above — `ItemsControl.ContainerFromIndex(index)` exists directly on the control now (confirmed in `ItemsControl.cs` source alongside `IndexFromContainer`). Just drop the `.ItemContainerGenerator` hop.

6. **`e.Data`/`DataFormats` leftovers — 2 in-scope files**: `HandsLiftedApp.Core/Controls/AddItemButton.axaml.cs` and `HandsLiftedApp.Core/Controls/Navigation/ItemOrderListView.axaml.cs` still have residual references (the drag-drop pass above fixed the main constructs in these files but evidently missed some usages — re-check both fully) — same fix patterns as the "Drag-and-drop" section above. Plus 1 out-of-scope demo (`Demos/PowerPointConverterSampleApp/MainWindow.axaml.cs`).

7. **`DisposeWith` — 1 file** (`HandsLiftedApp.Core/ViewModels/AddItem/AddItemViewModel.cs:51`). `using System.Reactive.Disposables;` and `using ReactiveUI;` are both already present but `DisposeWith` still doesn't resolve — **not yet researched**, likely moved namespace or renamed in ReactiveUI 23.x. Check `https://raw.githubusercontent.com/reactiveui/ReactiveUI/23.2.28/src/ReactiveUI/...` for where `DisposeWith` lives now (search the repo tree via `https://api.github.com/repos/reactiveui/ReactiveUI/git/trees/<sha-for-23.2.28>?recursive=1` the same way this session found `RxSchedulers.cs`).

8. **`DrawingContextHelper.WrapSkiaCanvas` — 1 file** (`HandsLiftedApp.Core/BitmapUtils.cs:54`). This is a direct consequence of the SkiaSharp 2.88→3.0 bump forced by Avalonia.Skia 12.0.5. **Not yet researched.** Need to find Avalonia 12's replacement API for wrapping a raw `SKCanvas` into an `IDrawingContextImpl` (or whatever the equivalent interface is now called — may have been renamed given the "render target/platform surface interfaces reworked" breaking change). Check `src/Avalonia.Skia/DrawingContextHelper.cs` (or wherever it moved) in the Avalonia repo at tag `12.0.5`.

9. **`WindowDecorations.None`/`.Full` CS0176 errors** — these were surfaced as a side effect of item #4 above (before that fix, the compiler partially resolves the broken expression and reports this confusing secondary error). Should disappear once #4 is fixed; not a separate issue.

## Biggest open risk (not a compile error, needs manual testing)

Bumping ReactiveUI 18.3.1 → 23.2.28 is a 5-major-version jump. Once the whole thing compiles, this needs **real interactive testing**, not just a clean build — `WhenAnyValue` chains, `ReactiveCommand` usage, `ObservableAsPropertyHelper`/`ToProperty` patterns, and DynamicData operators (7.9.1 → 9.4.31, 2 majors) used throughout the ViewModels are all candidates for subtle behavioral changes that won't show up as compile errors. Budget real time for this before considering the upgrade done, per the project's own UI-testing guidance (don't claim success from a clean build alone for anything touching interactive behavior).

Also flagged but not re-verified after all these changes: the libmpv `SoftwareVideoView`/`NativeVideoView` NativeControlHost-based rendering path (per `CLAUDE.md`, this is architecturally sensitive — primary/secondary MpvContext model) should get an explicit smoke test given Avalonia 12's compositor/render-target rework, even though it currently compiles clean.

## Reference

- Official breaking changes doc: https://docs.avaloniaui.net/docs/avalonia12-breaking-changes
- Avalonia GitHub repo tag used for source lookups: `12.0.5` (commit `fee9c561ce036e8a3e8cee2397c75ca599b4790d`)
- ReactiveUI.Avalonia GitHub repo (new package, reactiveui org): `https://github.com/reactiveui/ReactiveUI.Avalonia`, tag `v12.0.3` used for source lookups
- ReactiveUI core GitHub repo tag used: `23.2.28`
- Useful technique used throughout this session: fetch a file straight from GitHub raw content at a specific tag to check current API shape, e.g.:
  `curl -s "https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.0.5/src/Avalonia.Controls/Window.cs"`
  or list a whole repo tree at a tag to find where a class moved to:
  `curl -s "https://api.github.com/repos/AvaloniaUI/Avalonia/git/refs/tags/12.0.5"` (get commit sha) then
  `curl -s "https://api.github.com/repos/AvaloniaUI/Avalonia/git/trees/<sha>?recursive=1"` (list all files, grep for a name)
