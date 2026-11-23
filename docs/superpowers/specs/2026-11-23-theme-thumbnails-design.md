# Theme thumbnails in theme pickers - design

Date: 2026-11-23. Size: small change (one new control + cache, three XAML sites).

## Intent
Theme lists show only a name. Users pick a theme by look, like Word's "Themes" gallery. Show a rendered preview
of each theme in every place a theme is listed or picked.

## Goals
- Thumbnail (16:9) beside/above the name in:
  1. `SlideThemeDesigner.axaml` - the three `ListBox`es (General / Song / Scripture), via `DesignListItemTemplate`.
  2. `SongEditorControl.axaml` - Design tab "Slide Theme" `ComboBox`.
  3. `ItemEditDockRoot.axaml` - the two theme `ComboBox`es (lines ~87 and ~129).
- Thumbnail matches what the projector draws (same spec builders + `SlideRenderer`).
- Thumbnail refreshes when the theme is edited in the designer.

## Non-goals / anti-goals
- No new property on `BaseSlideTheme` (`HandsLiftedApp.Data` cannot reference Skia).
- No live libmpv frame per tile. Motion-video themes show colour/image background + a small play badge.
- No per-entry live `SlideCanvas` (heavy, transition machinery).
- Must not freeze the UI when a dropdown with many themes opens (lazy + cached).

## Approaches considered
- A. Converter + static cache: smallest, but no refresh on theme edit, weak for scripture. Rejected.
- **B. Reusable `ThemePreviewImage` control + shared cache: chosen by user.**
- C. Live `SlideCanvas` per entry: heavy with 20+ themes. Rejected.

## Design (B)
- `ThemePreviewImage` (Core, code-only control, `Image`-like): styled property `Theme` (`BaseSlideTheme?`).
  On attach / `Theme` change, render a sample slide to a `Bitmap` and show it. Render only when attached to the
  visual tree (lazy for virtualised lists / popups).
- Sample slide by theme type: song and general themes -> `SongSlideInstance` sample lyrics (reuse the text in
  `SlideThemeToPreviewConverter`), scripture themes -> a sample verse via `ScriptureParagraphSpecBuilder`.
  Background via `ThemeBackgroundSpecBuilder.Build(theme, hasMotionBackground: false)`, so a motion theme renders
  its colour/image; overlay a small play badge when the theme has a default motion video path.
- Render via `SlideRenderer.RenderThumbnail` / `BitmapUtils.RenderThumbnail` at a fixed pixel width sized for the
  largest use (width x display scaling, rounded). Do not use `ThumbnailRenderSize.CurrentWidth` (that is the slide
  grid size and changes with a setting).
- Cache (`ThemeThumbnailCache`, static, UI thread): key = theme instance via `ConditionalWeakTable` -> (version, Bitmap).
  Version bumps on theme change. Disposed old bitmap on replace.
- Refresh: control subscribes to the theme's `Changed` (per CLAUDE.md `ReactiveObject.Changed` pattern), debounced
  ~250 ms on the UI scheduler, ignoring `Calculated*` properties (they update async and are derived). Re-render,
  update cache, controls bound to that theme refresh via the cache change.
- Layout (proposal, user may adjust):
  - Designer lists: thumbnail on top at list width, name and "default" labels below (existing `DockPanel`).
  - Dropdowns: item template = tile (thumbnail + name); `ItemsPanel` is a 2-column `WrapPanel`/`UniformGrid`;
    closed box shows a small thumbnail + name. Keep the existing `AppliesToSongsVisibilityConverter` item filter.
  - "None selected" / blank option in the item edit dock (`PrependBlankThemeOptionConverter`) keeps a text-only tile.

## Constraints from the codebase
- Spec builders: create a fresh `SKTypeface` per returned element (existing builders already do).
- `BaseSlideTheme.CopyFrom`/`Calculated*` async: never diff or key on all properties.
- `ComboBox` in Avalonia 12: compiled bindings need `x:DataType`; `Popup` content is a `PopupRoot` (not `Window`).
- Click through the real app (devtools / headless UiTests) after the change; screenshots must be read.

## Left to the builder
- Exact thumbnail pixel width, tile size, badge art, debounce value.
- Whether the cache lives as a static class or a small service.
- Test plan: unit test cache invalidation on theme change; `HandsLiftedApp.UiTests` screenshot of designer lists and
  of an open dropdown.
