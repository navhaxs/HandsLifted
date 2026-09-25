# Song item motion-background video override — design

## Context / user story

As a user, I can create themes to style my song slides:

- Two types of theme "background mode": **plain** or **motion background** (new axis).
- A motion-background theme can have a default video assigned (new).

As a user, I can assign a theme to a song item (existing functionality, unchanged). A new button appears next to "Edit" in `ItemEditDockRoot` for song item types.

For a given song item, if a motion-background theme has been assigned, the user can assign a video to use as the motion background for that item, overriding the theme's default video.

The UI for assigning a video is a reusable popup component, used in two places:
1. The new button next to "Edit" in `ItemEditDockRoot` (per song item — sets the item-level override).
2. `SlideThemeDesigner` (for motion-background-type themes — sets the theme's default video).

## Current-state findings (why this isn't a small tweak)

Investigation of the existing codebase surfaced a mismatch between the story's premise and current code, which shapes this design:

- `SlideThemeType` (`HandsLiftedApp.Data/Models/SlideTheme/SlideThemeType.cs`) is `General | SongTheme | ScriptureTheme` — a **content-category** axis (which designer column a theme belongs to), not a plain/motion-background axis. No such axis exists on `BaseSlideTheme` today.
- No "default video" field exists on `BaseSlideTheme` today.
- The only existing per-song video field is `SongItem.MotionBackgroundVideoPath` (`HandsLiftedApp.Data/Models/Items/SongItem.cs`) — **shared-library-level**: one value per song, shared across every playlist that references that song. It's edited inline (Browse/Clear buttons, no popup) in `SongEditorControl`.
- `SongItemInstance` (runtime, `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs`) delegates `Design` and `MotionBackgroundVideoPath` straight through to the shared library `SongItem` — there is currently no playlist-item-local state for either.
- Precedent for adding a playlist-item-local override field exists: `SlideTransitionDurationMs` is threaded through `Item` (base data class) → `SongItemReference` (XML data model) → `HandsLiftedDocXmlSerializer.SerializeItem` → `ItemInstanceFactory.ToItemInstance` → `SongItemInstance`. This design follows the same three-touch-point threading pattern for the new override field.

## Decisions (resolved during brainstorming)

1. **Background mode is a new orthogonal property**, not a repurposing of `SlideThemeType`. A `SongTheme` can independently be `Plain` or `MotionBackground`.
2. **The video override is playlist-item-scoped** (new field on `SongItemReference`/`SongItemInstance`), not a reuse of the shared `SongItem.MotionBackgroundVideoPath`. The same song can have a different override video in different playlists.
3. **Resolution order**: item override → song's own video (existing shared field) → theme's default video → none (plain). The existing shared per-song field is kept as a fallback tier, not retired; `SongEditorControl` is unchanged.
4. **The new "Video" button in `ItemEditDockRoot`** is a standalone button next to "Edit" (not nested inside Edit's flyout), visible only when the song item's resolved theme has `BackgroundMode == MotionBackground`. Hidden entirely otherwise (not shown-disabled).
5. **`SlideThemeDesigner`** gets an explicit Plain/MotionBackground toggle in the theme editor panel; the video-picker button/popup for the theme's own default video appears only when MotionBackground is selected.
6. **No copy-into-media-library behavior** for the new video picker — it references the chosen file path as-is (relativized via the existing `ToRelativePathIfUnderMediaLibrary` helper, same treatment as other media paths), matching the existing `SongEditorControl` video picker rather than the copy-on-pick behavior of the theme background-image picker.

## Data model changes

### `BaseSlideTheme` (`HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs`)

- New enum `ThemeBackgroundMode { Plain, MotionBackground }`.
- New property `BackgroundMode` (`ThemeBackgroundMode`, default `Plain`).
- New property `DefaultMotionBackgroundVideoPath` (`string?`).
- Include both in the existing reflection-based `CopyFrom` (theme duplication already works generically over public properties, so no special-casing expected — verify during implementation).

### `SongItemReference` (`HandsLiftedApp.Data/Models/Items/SongItemReference.cs`) and `SongItemInstance` (`HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs`)

- New property `MotionBackgroundVideoOverride` (`string?`), playlist-item-scoped (does **not** delegate to `ResolvedSong` — stored directly on the instance/reference, unlike `Design` and the existing `MotionBackgroundVideoPath`).

## Resolution logic

New computed property on `SongItemInstance`, e.g. `ResolvedMotionBackgroundVideoPath`:

```
1. MotionBackgroundVideoOverride, if non-empty and valid
2. else ResolvedSong.MotionBackgroundVideoPath (existing shared field), if non-empty and valid
3. else ResolvedDesignTheme.DefaultMotionBackgroundVideoPath, if ResolvedDesignTheme.BackgroundMode == MotionBackground and non-empty
4. else null (plain — no motion background)
```

`HasMotionBackground` (currently `!string.IsNullOrWhiteSpace(MotionBackgroundVideoPath) && MotionBackgroundService.IsValidVideoFile(...)`) is updated to check `ResolvedMotionBackgroundVideoPath` instead of the raw `MotionBackgroundVideoPath`.

The new "Video" override button's visibility gate is separate from this resolution chain: it depends only on `ResolvedDesignTheme?.BackgroundMode == MotionBackground` (the assigned theme's mode), per decision 4.

## Reusable popup component

New control, e.g. `MotionBackgroundVideoPickerFlyout` (exact name/location TBD at implementation time — likely alongside `TextBoxFilePathPicker` in `HandsLiftedApp.Core/Controls/`).

Modeled on the existing menu-row → nested-`Flyout` pattern already used three times in `ItemEditDockRoot.axaml` (Theme picker, Fade-transition slider) rather than introducing a new interaction shape.

Contents:
- Browse button — opens file picker filtered to `*.mp4;*.mov;*.avi;*.wmv;*.mkv;*.webm` (same filter as `SongEditorControl.BrowseMotionBackground_OnClick`).
- Read-only text display of the current path (or "None" / theme-default-in-effect messaging where relevant).
- Clear button — resets the bound value to `null`.

API: two-way bindable `VideoPath` (`string?`) `DirectProperty`, following the shape of the existing `TextBoxFilePathPicker.FilePath` property.

No copy-into-library step (decision 6); path relativization follows the existing `ToRelativePathIfUnderMediaLibrary` / `RelativeFilePathResolver.ToAbsolutePath` pattern used elsewhere for media paths.

**Used in two places:**
- `ItemEditDockRoot`'s new "Video" button (song item template) → bound to `SongItemInstance.MotionBackgroundVideoOverride`.
- `SlideThemeDesigner`'s new video-picker button (shown when a theme's `BackgroundMode == MotionBackground`) → bound to `BaseSlideTheme.DefaultMotionBackgroundVideoPath`.

## UI changes

### `ItemEditDockRoot.axaml` / `.axaml.cs`

- New standalone button next to the existing "Edit" button, in the song-item (`SongItemInstance`) `DataTemplate` only.
- Visible only when `ResolvedDesignTheme?.BackgroundMode == MotionBackground` (decision 4).
- Opens the reusable popup bound to `MotionBackgroundVideoOverride`.

### `SlideThemeDesigner.axaml` / `.axaml.cs`

- New Plain/MotionBackground toggle (segmented control or two radio buttons) in the theme editor panel, alongside existing theme properties (e.g. near `BackgroundGraphicFilePath`'s picker).
- When `MotionBackground` is selected, show the reusable popup's trigger button bound to `DefaultMotionBackgroundVideoPath`.

## Serialization

Three touch points, mirroring the existing `SlideTransitionDurationMs` threading:

1. **`SongItemReference`** (`HandsLiftedApp.Data/Models/Items/SongItemReference.cs`) — add `MotionBackgroundVideoOverridePath` (`string?`).
2. **`HandsLiftedDocXmlSerializer`** (`HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs`):
   - `SerializeItem`'s `SongItemInstance` branch: copy the override path across (relativized via `ToRelativePathIfUnderMediaLibrary`, same as other media paths).
   - `SerializePlaylist`'s `Designs` copy loop: copy `BackgroundMode` and `DefaultMotionBackgroundVideoPath` onto the serialized theme (relativizing the video path the same way `BackgroundGraphicFilePath` is already handled).
3. **`ItemInstanceFactory.ToItemInstance`** (`HandsLiftedApp.Core/ItemInstanceFactory.cs`) — `SongItemReference` branch: copy the override path onto the new `SongItemInstance`, resolving relative→absolute via `RelativeFilePathResolver.ToAbsolutePath` (same pattern as other deserialized media paths).

## Known gotcha to carry into the implementation plan

The new "Video" button's Browse action opens an OS file-picker dialog from inside a nested `Flyout`/`Popup` (button → Flyout → picker control → its own Browse button). Per this project's documented Avalonia gotcha ([CLAUDE.md](../../../CLAUDE.md)): `TopLevel.GetTopLevel(control)` does not cross a `Popup` boundary and silently returns `null` for controls hosted inside a `Flyout`/`MenuFlyout`/`ContextMenu`, which breaks `StorageProvider`-based file-picker calls that need the owning `Window`. The Browse button's file-picker launch must resolve the owning `Window` via the logical-tree walk (`.Parent` loop) already used by `ControlExtension.FindAncestor<T>` and `AddItemFlyoutResourceDictionary.axaml.cs`'s `FindNearestDataContextAncestor`, not `TopLevel.GetTopLevel(...) as Window`.

This class of bug produces no compiler error, no test failure, and no exception — it must be verified by actually clicking through both popups (`ItemEditDockRoot` and `SlideThemeDesigner`) in a running app before the work is considered done.

## Testing approach

- Unit tests for the 4-case resolution chain (`ResolvedMotionBackgroundVideoPath`): item override set; song's own video set (no override); theme default set (no override, no song video); none set (plain).
- Unit tests for round-trip playlist XML serialization: new theme fields (`BackgroundMode`, `DefaultMotionBackgroundVideoPath`) and new item field (`MotionBackgroundVideoOverridePath`) survive save/load, including path relativization/resolution.
- Manual click-through in a running app for both popup usages (`ItemEditDockRoot`, `SlideThemeDesigner`), specifically confirming the file-picker dialog opens correctly from within the nested-Flyout context (see gotcha above).

## Out of scope

- Retiring or migrating the existing shared `SongItem.MotionBackgroundVideoPath` field or its `SongEditorControl` UI — kept as-is, as the middle fallback tier.
- Any change to `ScriptureItemInstance` or other non-song item types.
- Copy-into-media-library behavior for the new video picker (decision 6).
