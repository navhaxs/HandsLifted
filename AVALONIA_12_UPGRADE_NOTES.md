# Avalonia 11.3.18 → 12.0.5 upgrade — handoff notes

**Status: DONE and merged to `master`.** Builds clean (0 errors), launches successfully, and both remaining interactive-risk items (ReactiveUI 5-major-version jump, libmpv video/motion-background playback) have been manually tested with no issues found. This upgrade is considered complete.

**Follow-up (2026-07-28): bumped 12.0.5 → 12.1.0.** Checked NuGet dependency floors before bumping: `Xaml.Behaviors.Avalonia`/`.Interactions`/`.Interactivity` 12.0.5 depend on `Avalonia [12.0.5, )` (open floor, not an exact pin — the "don't casually bump" warning below turned out to be over-cautious for a same-major patch bump), `PanAndZoom` 12.0.0.1 depends on `Avalonia [12.0.0, )`, `ReactiveUI.Avalonia` 12.0.3 (kept at this version, not bumped to the newer 12.1.0 which requires ReactiveUI >= 24.0.0 and new `ReactiveUI.Primitives(.Avalonia)` packages) depends on `Avalonia [12.0.4, )`, and `Avalonia.Skia` 12.1.0 still pins the same `SkiaSharp [3.119.4, )` floor already in use. All satisfied. `dotnet restore` + `dotnet build -c Release` on `HandsLiftedApp.Desktop` were clean (0 errors), and the app launched successfully (`dotnet run`, confirmed `Avalonia 12.1.0.0` in the startup log, no crash). Only `Directory.Build.props`'s `AvaloniaVersion` changed — no other package versions needed to move.

## Where this lives

- This work is merged into local `master` (commit `a6e70e9`, merge commit `216fa33`). It is **local only** — not pushed to `origin` (`origin/master` is ~83 commits behind local `master` at the time of merge).
- It was developed on branch `worktree-avalonia-12-upgrade` in worktree `C:\Users\Jeremy\RiderProjects\HandsLifted\.claude\worktrees\avalonia-12-upgrade` (left in place — host-managed workspace, not removed as part of finishing this work).
- The branch forked from local `master` at commit `f7c695c` ("build: bump Avalonia to 11.3.18, patch vulnerable transitive deps"), back when master was still on Avalonia 11.3.18. By the time of merge, master had advanced 60 commits past that fork point (an entire Scripture slide-type feature, plus unrelated fixes) — **still on Avalonia 11.3.18** — so merging this branch upgraded that new Scripture code to Avalonia 12 in the same motion. See "Merged to master" below for what that surfaced.

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

**RxApp**: ReactiveUI 23.x moved `RxApp.MainThreadScheduler`/`RxApp.TaskpoolScheduler` to a new `RxSchedulers` static class (same `ReactiveUI` namespace, no new `using` needed). Confirmed via ReactiveUI GitHub source at tag `23.2.28`: `src/ReactiveUI/RxSchedulers.cs` exists, `src/ReactiveUI/RxApp.cs` does not. Mechanical fix: `RxApp.MainThreadScheduler` → `RxSchedulers.MainThreadScheduler`. **Fully fixed repo-wide** (all in-scope files). `RxApp.SuspensionHost`'s 3 occurrences turned out to be entirely in `SlideEditorStandalone` (out of scope) — no in-scope fix needed.

## Resolved in the follow-up session (Subagent-Driven Development, 11 tasks, 2026-07-28)

A second session picked this branch up at 71 build errors and drove it to **0 build errors** via 11 reviewed tasks plus a final whole-branch review. Full mapping, in case another API surfaces something similar later:

1. **`RxApp.*`** (all ~21 files) → `RxSchedulers.*`, as above.
2. **`DisposeWith`** (`AddItemViewModel.cs`) — moved to Rx.NET's `System.Reactive.Disposables.Fluent` namespace (added in System.Reactive 6.x, not a ReactiveUI-owned API anymore). Fix: add `using System.Reactive.Disposables.Fluent;`.
3. **Drag-and-drop leftovers**: residual `DataFormats.*`/`e.Data` in `AddItemButton.axaml.cs` (main pass had missed it), plus a **new discovery**: 3 sites (`LibraryPaneView.axaml.cs`, `MediaGroupItemEditor.axaml.cs`, `LibraryQueryView.axaml.cs`) needed the same `PointerEventArgs`→`PointerPressedEventArgs` capture-field pattern as `SlideThumbnailBehavior.cs` (the reference pattern), for a `PointerMoved`-handler drag-threshold case the first pass didn't reach.
4. **`SystemDecorations` → `WindowDecorations`**: fixed the one remaining C# site (`ProjectorWindow.axaml.cs`) AND (found only in a final whole-branch review, since XAML usage only *warns*, `AVLN5001`, not errors) 10 more `.axaml` files using it as an attribute (`Confirmation/*.axaml`, `MessageWindow.axaml`, `Setup/DisplayIdentifyWindow.axaml`, `SplashWindow.axaml`).
5. **`ItemContainerGenerator.ContainerFromIndex`** (`PlaylistSlidesView.axaml.cs`, 3 sites) — same pattern as `IndexFromContainer`, drop the `.ItemContainerGenerator` hop.
6. **`DrawingContextHelper.WrapSkiaCanvas`** (`BitmapUtils.cs`) — went `public`→`internal` in Avalonia 12 (not removed/renamed). Its result was dead code (constructed, disposed, never drawn through — the real thumbnail path is a direct `SKCanvas.DrawRect` + raw pixel-buffer copy) — the call was simply deleted.
7. **`ExtendClientAreaChromeHints`** — fully removed API, 13 in-scope files, attribute deleted (matches the `BrowserWindow.axaml` reference pattern already in place). This left the 4 extended-chrome windows (`AboutWindow`, `AddItemWindow`, `MainWindow`, `SetupWindow`) with cosmetic caption-button placement issues — fixed in the separate "Title bar polish" pass below, not as part of this task.
8. **`OpenFileDialog` → `StorageProvider.OpenFilePickerAsync`**: only 3 files actually needed conversion (`TextBoxFilePathPicker.axaml.cs`, `AddItemWindow.axaml.cs`, `SongEditorWindow.axaml.cs`) — the other ~7 files on the original candidate list were a grep false-match on the local interaction member name `ShowOpenFileDialog`, not the removed Avalonia type (several were already migrated). None of the 3 old dialogs set `Title`/`Filters`, so none were added; `AllowMultiple` carried over exactly; result mapped via `file.TryGetLocalPath()`.
9. **Stale `Avalonia.Xaml.Interactivity`/`Avalonia.Xaml.Interactions` `assembly=` in `.axaml` xmlns** — discovered only *after* fixing #8, because that was the first time `HandsLiftedApp.Core`'s C# compile succeeded, which let the Avalonia XAML compiler run on ~40 files it had never reached before (120 new `AVLN2000` errors surfaced in one jump — the "errors surface incrementally" lesson at a much bigger scale than expected). Root cause: the Phase-1 package rename (`Avalonia.Xaml.Interactivity`→`Xaml.Behaviors.Interactivity`, `Avalonia.Xaml.Interactions`→`Xaml.Behaviors.Interactions`) changed the assembly/dll name but the **C# namespace stayed identical** (confirmed via the `wieslawsoltes/Xaml.Behaviors` GitHub source). Fix: change only the `assembly=` value in each xmlns declaration, 4 files, ~116 of the 120 errors gone in one pass (one file, `ItemSlidesView.axaml`, alone had ~100 sites fail from its one bad xmlns line).
10. **`PlacementMode` → `Placement`** on `Popup`/`ContextMenu` — pure property rename, enum type and member values (`BottomEdgeAlignedLeft`, `Bottom`, etc.) unchanged. 2 sites: `LivePane.axaml`, `SongArrangementControl.axaml`.
11. **`RadioButton.Checked` → `IsCheckedChanged`** (`SlideThemeDesigner.axaml`) — Avalonia 12 dropped the WPF-style `Checked`/`Unchecked` events; `IsCheckedChanged` fires on both check AND uncheck transitions. Running the handler on both transitions is what's wanted (it's a pure function of both toggles' current state) — a same-day final-review pass found and removed an over-cautious guard that had suppressed exactly that self-correction, fixing a pre-existing (bug-for-bug identical to Avalonia 11) transient dual-visible-preview-panel glitch for free.

**Notable finding for future upgrades**: after any task lets a previously-failing project's C# compile succeed, expect the Avalonia XAML compiler to reach files it never touched before and surface a fresh batch of errors — this happened once already at a much bigger scale (120 errors, ~40 files) than any single earlier round. Don't declare an upgrade's build-fix phase done on a partial/incremental build; always confirm with a full `dotnet build ... -c Release` from a clean state.

## Found by interactive testing (2026-07-28): a launch-blocking crash the build-fix pass could never have caught

`dotnet run` on the actual app crashed on every launch with:

```
System.InvalidCastException: Unable to cast object of type 'Avalonia.Controls.TopLevelHost' to type 'Avalonia.Controls.Window'.
   at HandsLiftedApp.Core.Views.MainWindow.SubscribeToWindowState() ...
```

Root cause: `MainWindow.axaml.cs`'s `SubscribeToWindowState()` cast `this.VisualRoot` to `Window` (with a polling loop waiting for it to become non-null, since it's null before the window attaches — an old, self-acknowledged "stupid hack"). In Avalonia 11, once attached, a Window's `VisualRoot` resolved to the `Window` itself. **Avalonia 12's compositor rework changed this: `VisualRoot` now resolves to an internal `TopLevelHost` wrapper instead**, so the cast throws the instant the window attaches — 100% reproducible, every launch. This is a *runtime* behavior change with no compile-time signal at all (the property still exists, still type-checks, still returns non-null) — nothing in the entire 11-task build-fix pass could have caught it; only running the app did.

Fix (`fb8b20e`): `MainWindow` already IS the host window (`this`), so the VisualRoot lookup was unnecessary — replaced with direct use of `this`, no cast, no polling.

**The same `VisualRoot`-as-`Window` pattern also existed in `Libraries/LibMpv/Avalonia.Controls.LibMpv/NativeVideoView.cs`** (2 sites: an `Observable.FromEventPattern(VisualRoot, ...)` reflection-based event subscription, and a `_floatingContent.Show(VisualRoot as Window)` call) — this is exactly the libmpv render path this file's own notes flagged as "confirmed untouched, needs an explicit smoke test." Fixed the same way, but using `TopLevel.GetTopLevel(this)` instead of raw `VisualRoot` — `TopLevel.GetTopLevel()` is the documented, Avalonia-12-correct way to resolve the owning top-level from a `Visual`, and is already the established pattern elsewhere in this codebase (e.g. `GoogleSlidesItemEditView.axaml.cs`). **This half of the fix is unverified by an actual video-rendering run** — the smoke test only confirmed the app launches and loads a real playlist library (with images) without crashing; it did not exercise a motion background or NDI/video output. See "What's still open" below.

**Takeaway for anyone auditing other Avalonia 12 upgrades**: grep for `(Window)` casts (hard or `as`) on `VisualRoot`/`this.VisualRoot` repo-wide — the "docs/CLAUDE-notes said this compiles fine, don't touch it" guidance from earlier in this upgrade was correct about compilation and wrong about runtime behavior. `grep -rn "VisualRoot" --include="*.cs" . | grep -v obj/` and manually check every hit that casts to a concrete type rather than using `TopLevel.GetTopLevel(...)`.

## Title bar polish (2026-07-28, after the crash fix)

Beyond the launch-blocking crash above, interactive use turned up cosmetic issues from Avalonia 12's `WindowDrawnDecorations` chrome model (it now draws its own title bar/caption buttons instead of relying on native OS chrome — see https://github.com/AvaloniaUI/Avalonia/discussions/21170 for the pattern this was based on):

- **Caption buttons rendered too low** on windows with a tall `ExtendClientAreaTitleBarHeightHint` (90px, for the custom app bar). Root cause: the theme's caption buttons have a fixed `Height=30` with `VerticalAlignment=Stretch`, which falls back to centering rather than filling when `Height` is set explicitly — so on a 90px-tall title bar they render offset ~30px down. Fixed globally in `App.axaml`: `WindowDrawnDecorations /template/ StackPanel#PART_OverlayPanel > Button { VerticalAlignment: Top }`.
- **Default drawn fullscreen button and window title text** hidden globally (the app draws its own title bar content instead) — same `App.axaml` styles block. The title text needed targeting the inner `TextBlock`, not `PART_TitleTextPanel` itself, since the panel's `IsVisible` is set via `TemplateBinding` in the theme (a template-level value outranks an app `Style` setter on the same property).
- **Thick border only when maximized**: a stale 7px `Padding` in `MainWindow.axaml.cs`'s `SubscribeToWindowState()` used to compensate for a Win32 native-chrome maximize-overflow quirk. Avalonia 12's drawn decorations already inset maximized content correctly, so the old compensation just showed as an unwanted border — removed.
- **`AboutWindow`'s Space key didn't press the `IsDefault` Done button** — by Avalonia's own design (`Button.cs`'s `ListenForDefault`), `IsDefault` only ever wires up Enter; Space only activates whatever control has keyboard focus. Fixed by focusing Done on window open, matching normal "primary action focused by default" dialog UX.
- `MainView.axaml`'s custom top bar marked with `WindowDecorationProperties.ElementRole="DecorationsElement"` so it stays interactive under the extended chrome (`Avalonia.Controls.Chrome` namespace).

Commit: `03a3655`.

## Merged to master (2026-07-28)

Merging `worktree-avalonia-12-upgrade` into `master` required resolving one real conflict and revealed more Avalonia-12 fallout in code that only existed on `master`'s side (the Scripture feature, added after this branch forked):

- **`BitmapUtils.cs` conflict**: `master` had independently fixed the exact same native-buffer leak flagged as a follow-up in this upgrade's final review (try/finally + `Marshal.FreeCoTaskMem`, plus a null-safe `EncodeToAvaloniaBitmap` refactor). Resolved by combining master's leak-fix structure with this branch's `WrapSkiaCanvas` removal — both needed, neither side was a strict superset. Merge commit: `216fa33`.
- **Post-merge full rebuild surfaced 3 more instances of already-known error categories**, this time in the ~60 commits of Scripture-feature code that had never been built against Avalonia 12: `RxApp.MainThreadScheduler` in `ScriptureSlideInstance.cs`, `ExtendClientAreaChromeHints`+`SystemDecorations` in `ScriptureAddDialog.axaml`, and a second `Popup` needing the `PlacementMode`→`Placement` rename in `LivePane.axaml` (`fadeFlyout`, added by the Scripture work). Fixed and verified via a full non-incremental rebuild (0 errors) plus a repo-wide grep for every known problem pattern from this upgrade (confirmed no further instances outside comments/dead code). Commit: `a6e70e9`.
- **Takeaway**: merging an upgrade branch into a base that kept moving is its own round of "errors surface incrementally" — a clean build on the feature branch alone does not mean the merged result is clean. Always rebuild (ideally non-incrementally) after merging, and re-run the same problem-pattern greps used during the original upgrade.

## Verified (2026-07-28): the two remaining interactive-risk items

Both items flagged throughout this upgrade as "needs real interactive testing, not just a clean build" have now been manually tested by the user on the merged `master`, with no issues found:

- **ReactiveUI 18.3.1 → 23.2.28** (5-major-version jump): `WhenAnyValue` chains, `ReactiveCommand` execution, `ObservableAsPropertyHelper`/`ToProperty` patterns, DynamicData operators — all exercised through normal interactive use. OK.
- **libmpv `SoftwareVideoView`/`NativeVideoView` rendering path**, including the `VisualRoot`→`TopLevel.GetTopLevel(this)` fix in `NativeVideoView.cs` — motion background/video playback exercised directly. OK.

This closes out the last two open items from this upgrade. Small non-blocking follow-ups (not part of this upgrade, correctly out of scope, worth tracking separately): `BitmapUtils.CreateThumbnail`'s remaining unchecked-null `SKBitmap.Resize()` result (the leak itself is now fixed, see "Merged to master" above); the 4 drag sources touched by the drag-and-drop migration (library pane, library query, media-group thumbstrip, add-item drop target) haven't been individually exercised, though none showed issues during general use.

## Reference

- Official breaking changes doc: https://docs.avaloniaui.net/docs/avalonia12-breaking-changes
- Avalonia GitHub repo tag used for source lookups: `12.0.5` (commit `fee9c561ce036e8a3e8cee2397c75ca599b4790d`)
- ReactiveUI.Avalonia GitHub repo (new package, reactiveui org): `https://github.com/reactiveui/ReactiveUI.Avalonia`, tag `v12.0.3` used for source lookups
- ReactiveUI core GitHub repo tag used: `23.2.28`
- Xaml.Behaviors GitHub repo (new package family, wieslawsoltes org): `https://github.com/wieslawsoltes/Xaml.Behaviors`, commit `fd692d956d1ea9eecb48165d40fa1203284d68b0` used for source lookups (namespace vs. assembly-name confirmation)
- Avalonia 12 `WindowDrawnDecorations`/custom-title-bar discussion: https://github.com/AvaloniaUI/Avalonia/discussions/21170
- Useful technique used throughout this session: fetch a file straight from GitHub raw content at a specific tag to check current API shape, e.g.:
  `curl -s "https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.0.5/src/Avalonia.Controls/Window.cs"`
  or list a whole repo tree at a tag to find where a class moved to:
  `curl -s "https://api.github.com/repos/AvaloniaUI/Avalonia/git/refs/tags/12.0.5"` (get commit sha) then
  `curl -s "https://api.github.com/repos/AvaloniaUI/Avalonia/git/trees/<sha>?recursive=1"` (list all files, grep for a name)
