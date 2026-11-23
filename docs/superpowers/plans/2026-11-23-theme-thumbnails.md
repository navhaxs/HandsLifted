# Theme Thumbnails Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a rendered preview of each slide theme in the theme designer lists and in the theme dropdowns of the song editor and item edit dock.

**Architecture:** A static `ThemePreviewSpecBuilder` turns any `BaseSlideTheme` into a sample `SlideRenderSpec` using the real projector builders. A static `ThemeThumbnailCache` renders that spec to an Avalonia `Bitmap` once per theme and reports invalidations when the theme is edited. A reusable `ThemePreviewImage` control shows the cached bitmap (lazy, debounced refresh). XAML sites use the control; the three dropdowns share one style that turns items into tiles.

**Tech Stack:** Avalonia 12.1.1, ReactiveUI 23.x, SkiaSharp, MSTest (`HandsLiftedApp.Tests`, headless `HandsLiftedApp.UiTests`).

**Spec:** `docs/superpowers/specs/2026-11-23-theme-thumbnails-design.md`

## Global Constraints

- `HandsLiftedApp.Data` must not reference Skia/Core: no thumbnail property on `BaseSlideTheme` (`HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs`).
- Thumbnails are 16:9, rendered through `SlideRenderer.RenderThumbnail` / `BitmapUtils.RenderThumbnail` (not `ThumbnailRenderSize.CurrentWidth`).
- Never compare/key on all `BaseSlideTheme` properties; ignore `Calculated*` property changes.
- Motion-video themes: colour/image background only (`ThemeBackgroundSpecBuilder.Build(theme, hasMotionBackground: false)`), plus a play badge when `DefaultMotionBackgroundVideoPath` is non-empty.
- Blank/none option (a `null` item from `PrependBlankThemeOptionConverter`) stays a text-only tile.
- Keep the existing `AppliesToSongsVisibilityConverter` / `AppliesToScriptureVisibilityConverter` item filters.
- Avalonia 12: compiled bindings need `x:DataType`; popup content is not under `Window` (do not use `TopLevel.GetTopLevel(...) as Window` from inside popups).
- No `find`/`grep` shellouts; scope searches to project dirs, never edit under `.claude/worktrees/`.
- The working tree contains unrelated uncommitted lyric-reflow changes: every commit stages only the files named in its task (`git add <paths>`, never `-A`).
- Run tests with the dotnet PATH workaround; a full non-incremental build after the last task.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## Review Focus

1. Theme whose background image file does not exist: thumbnail still renders (colour only), no exception. (Task 2)
2. Slider drag fires 50 theme changes in a second: debounced, at most a couple of renders, none per change. (Task 3)
3. Control that is created but never attached (virtualised/closed dropdown): renders nothing. (Task 3)
4. `Theme` swapped on a live control (list item recycled, or theme deleted then restored as a new object): shows the new theme, old subscription dropped. (Task 3)
5. `null` theme (blank option) and empty/very long theme `Name`: no exception, tile does not overflow. (Tasks 3, 5)

---

## File Structure

| File | Responsibility |
|---|---|
| `HandsLiftedApp.Core/Render/Skia/Builders/ThemePreviewSpecBuilder.cs` (new) | `BaseSlideTheme` -> sample `SlideRenderSpec` (song/general: lyrics; scripture: verse) |
| `HandsLiftedApp.Core/Services/ThemeThumbnailCache.cs` (new) | per-theme bitmap cache + `Invalidated` observable |
| `HandsLiftedApp.Core/Controls/ThemePreviewImage.cs` (new) | the reusable control |
| `HandsLiftedApp.Core/Views/ThemePickerStyles.axaml` (new) | `ComboBox.themePicker` style: tile item template, wrap panel, small selection box |
| `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml` (modify) | add control to `DesignListItemTemplate` |
| `HandsLiftedApp.Core/Views/Editors/Song/SongEditorControl.axaml`, `Views/ItemEditDock/ItemEditDockRoot.axaml` (modify) | apply `themePicker` class, drop name-only templates |
| `HandsLiftedApp.Core/App.axaml` (modify) | include `ThemePickerStyles.axaml` |
| `HandsLiftedApp.Tests/Render/Skia/ThemePreviewSpecBuilderTests.cs` (new) | spec tests |
| `HandsLiftedApp.UiTests/ThemeThumbnailCacheTests.cs`, `ThemePreviewImageTests.cs`, `ThemePickerUiTests.cs` (new) | headless tests + screenshots |

---

### Task 1: ThemePreviewSpecBuilder

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/Builders/ThemePreviewSpecBuilder.cs`
- Test: `HandsLiftedApp.Tests/Render/Skia/ThemePreviewSpecBuilderTests.cs`

**Interfaces:**
- Consumes: `SongSlideSpecBuilder.Build(SongSlideInstance)`, `ScriptureParagraphSpecBuilder.Build(ScriptureSlideInstance)`, `ScriptureParagraphLayoutEngine.Paginate(IReadOnlyList<ScriptureVerseRef>, string headerText, BaseSlideTheme) -> List<ScriptureParagraphPage>` (page has `Lines`, `FontSize`), `ThemeBackgroundSpecBuilder.Build`.
- Produces: `public static class ThemePreviewSpecBuilder { public static SlideRenderSpec Build(BaseSlideTheme? theme); }` and `public const string SampleLyrics` (the same 5 lines as `SlideThemeToPreviewConverter`).

- [ ] **Step 1: Write failing tests** (run inside `DispatcherTestThread.Run(...)` like other tests that build slide instances). Names and assertions:
  - `Build_SongTheme_HasTextElementsAndThemeColourBackground`: theme `{Type=SongTheme, BackgroundColour=Red, TextColour=White}` -> `spec.Elements.Count > 0`, `spec.Background is SolidBackground`.
  - `Build_GeneralTheme_UsesSongLyrics`: `Type=General` -> `Elements.Count > 0`.
  - `Build_ScriptureTheme_HasTextElements`: `Type=ScriptureTheme` -> `Elements.Count > 0` (sample verse paginated; first page only).
  - `Build_ThemeWithMotionVideo_BackgroundIsNotTransparent`: `DefaultMotionBackgroundVideoPath="x.mp4"`, opacity 100 -> `Background` is not `TransparentBackground`.
  - `Build_NullTheme_ReturnsBlackSolidBackgroundNoElements`.
- [ ] **Step 2: Run** `dotnet test HandsLiftedApp.Tests --filter ThemePreviewSpecBuilderTests`. Expected: compile FAIL (type missing).
- [ ] **Step 3: Implement `ThemePreviewSpecBuilder.Build(BaseSlideTheme? theme) -> SlideRenderSpec`.** Song/General: construct `new SongSlideInstance(null, null, "preview", SampleLyrics)`, set `Theme = theme`, call `SongSlideSpecBuilder.Build` (as the existing `SlideThemeToPreviewConverter` does), then replace the background with `ThemeBackgroundSpecBuilder.Build(theme, false)` (the instance may resolve motion on its own). Scripture: paginate one sample verse ("For God so loved the world..." John 3:16 via `ScriptureVerseRef(3,16,text)`), build a `ScriptureSlideInstance(null, "preview")` with `Theme`, `Lines = page.Lines`, `EffectiveFontSize = page.FontSize`, then `ScriptureParagraphSpecBuilder.Build`. Null theme: `new SlideRenderSpec(new SolidBackground(SKColors.Black), Array.Empty<RenderElement>())`. Match the exact namespaces by reading the neighbouring builders.
- [ ] **Step 4: Run tests.** Expected: all 5 PASS.
- [ ] **Step 5: Commit** `feat: sample slide spec for theme previews` (stage the two files only).

---

### Task 2: ThemeThumbnailCache

**Files:**
- Create: `HandsLiftedApp.Core/Services/ThemeThumbnailCache.cs`
- Test: `HandsLiftedApp.UiTests/ThemeThumbnailCacheTests.cs` (headless: building Avalonia bitmaps needs the platform)

**Interfaces:**
- Consumes: `ThemePreviewSpecBuilder.Build`, `BitmapUtils.RenderThumbnail` pattern but at a fixed width (use `SlideRenderer.RenderThumbnail(spec, PixelWidth)` then `BitmapUtils` pixel-copy helper; reuse the existing copy helper that `BitmapUtils.RenderThumbnail` uses; make it `internal`/public if it is private).
- Produces:
  ```csharp
  public static class ThemeThumbnailCache {
      public const int PixelWidth = 320;
      public static Bitmap? GetOrRender(BaseSlideTheme? theme);   // UI thread; null -> null
      public static IObservable<BaseSlideTheme> Invalidated { get; } // fires on UI thread after a non-Calculated* change
      public static int RenderCount { get; }                       // test hook
  }
  ```

- [ ] **Step 1: Write failing tests** in `HeadlessSession.Run`:
  - `GetOrRender_SameTheme_RendersOnce`: two calls return the same reference; `RenderCount` +1.
  - `GetOrRender_AfterThemeEdit_RendersNewBitmap`: change `BackgroundColour`, call again -> different reference.
  - `ChangeOfCalculatedProperty_DoesNotInvalidate`: raise a change on a `Calculated*` property (use a font-weight change that updates one, assert `Invalidated` did not fire for a bare `Calculated*` name by calling the internal filter, or set up via `RaisePropertyChanged("CalculatedFontWeight")` if accessible).
  - `GetOrRender_NullTheme_ReturnsNull`.
  - `GetOrRender_MissingBackgroundImage_StillRenders` (Review Focus 1): `BackgroundGraphicFilePath=@"Z:\nope.png"` -> non-null bitmap, no exception.
- [ ] **Step 2: Run** `dotnet test HandsLiftedApp.UiTests --filter ThemeThumbnailCacheTests`. Expected: FAIL (type missing).
- [ ] **Step 3: Implement.** `ConditionalWeakTable<BaseSlideTheme, Entry>`; `Entry { Bitmap? Bitmap; IDisposable Subscription; }`. On first request subscribe to `theme.Changed` (ReactiveObject), filter `!e.PropertyName.StartsWith("Calculated")`; on change set `Entry.Bitmap = null` and push the theme into a `Subject<BaseSlideTheme>` exposed as `Invalidated` (observe on `RxSchedulers.MainThreadScheduler`). Never dispose a replaced bitmap (several controls may still show it; GC reclaims it) - note this one-line reason in a comment. Wrap render in try/catch returning null and `Log.Warning` (use "-" not an em dash).
- [ ] **Step 4: Run tests.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: theme thumbnail cache`.

---

### Task 3: ThemePreviewImage control

**Files:**
- Create: `HandsLiftedApp.Core/Controls/ThemePreviewImage.cs`
- Test: `HandsLiftedApp.UiTests/ThemePreviewImageTests.cs`

**Interfaces:**
- Consumes: `ThemeThumbnailCache.GetOrRender`, `.Invalidated`, `.RenderCount`.
- Produces: `public class ThemePreviewImage : Control` with `StyledProperty<BaseSlideTheme?> ThemeProperty` / `Theme`, `public Bitmap? Source { get; }` (what is currently drawn). Measures to 16:9 from available width (height = width * 9 / 16; zero size when `Theme` is null). `Render` draws the bitmap stretched to bounds, a 1px rounded border in a neutral semi-transparent colour, and a small play-triangle badge bottom-right when `Theme?.DefaultMotionBackgroundVideoPath` is not null/empty.

- [ ] **Step 1: Write failing tests** (`HeadlessSession.Run`, control hosted in a `Window`, `UiTestHelpers.Settle()` plus a short poll helper that spins the dispatcher up to 2 s):
  - `Attached_WithTheme_ShowsBitmap`: `Source != null`.
  - `NeverAttached_DoesNotRender` (Review Focus 3): create control with theme, do not show; `ThemeThumbnailCache.RenderCount` unchanged.
  - `ThemeEdited_RefreshesSource`: change `BackgroundColour`; poll until `Source` reference differs.
  - `RapidEdits_AreDebounced` (Review Focus 2): 50 `BackgroundColour` changes in a loop, poll until stable; `RenderCount` grew by at most 3.
  - `ThemeSwapped_ShowsNewTheme_AndStopsListeningToOld` (Review Focus 4): swap to theme B; editing A no longer changes `Source`.
  - `NullTheme_MeasuresZeroAndDoesNotThrow` (Review Focus 5).
- [ ] **Step 2: Run** the filter `ThemePreviewImageTests`. Expected: FAIL (type missing).
- [ ] **Step 3: Implement.** On `AttachedToVisualTree`: load `Source` from cache and subscribe to `ThemeThumbnailCache.Invalidated.Where(t => ReferenceEquals(t, Theme)).Throttle(250ms)` observed on the main-thread scheduler, then reload and `InvalidateVisual`. On detach: dispose subscription. On `Theme` change while attached: reload and resubscribe. Do not call the cache while unattached.
- [ ] **Step 4: Run tests.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: ThemePreviewImage control`.

---

### Task 4: Designer lists

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml:103-157` (`DesignListItemTemplate`)
- Test: `HandsLiftedApp.UiTests/ThemePickerUiTests.cs`

**Interfaces:**
- Consumes: `ThemePreviewImage` (`xmlns` for `HandsLiftedApp.Core.Controls`).
- Produces: each designer list item shows the thumbnail above the name/labels.

- [ ] **Step 1: Write failing test** `Designer_Lists_ShowThumbnailPerTheme`: open the main window, put 3 themes (one per `SlideThemeType`) into `Globals.Instance.MainViewModel.Playlist.Designs` (follow how existing UI tests/seed code add themes), open the designer view (`SlideThemeDesigner` hosted in a `Window` with the view model as `DataContext`), settle, assert each visible list contains a `ThemePreviewImage` whose `Source != null`, then `window.Screenshot("theme-designer-lists")`.
- [ ] **Step 2: Run** `--filter ThemePickerUiTests`. Expected: FAIL (no `ThemePreviewImage` found).
- [ ] **Step 3: Add** `<controls:ThemePreviewImage DockPanel.Dock="Top" Theme="{Binding}" />` as the first child of the template's `DockPanel` (hidden `ListBoxItem`s from the type filters never attach, so never render).
- [ ] **Step 4: Run test; read the screenshot** `HandsLiftedApp.UiTests/bin/Debug/net10.0/screenshots/theme-designer-lists.png` and check thumbnail size, name below, "default" labels intact. Expected: PASS.
- [ ] **Step 5: Commit** `feat: theme thumbnails in the designer lists`.

---

### Task 5: Tile-style theme dropdowns

**Files:**
- Create: `HandsLiftedApp.Core/Views/ThemePickerStyles.axaml`
- Modify: `HandsLiftedApp.Core/App.axaml` (add `StyleInclude` for the new file), `HandsLiftedApp.Core/Views/Editors/Song/SongEditorControl.axaml:91-106`, `HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml:85-99,127-141`
- Test: `HandsLiftedApp.UiTests/ThemePickerUiTests.cs`

**Interfaces:**
- Consumes: `ThemePreviewImage`.
- Produces: style selector `ComboBox.themePicker`: `ItemTemplate` = tile (preview on top, `Name` below with `TextTrimming="CharacterEllipsis"`, `FallbackValue` text for the null option), `ItemsPanel` = `WrapPanel` (tile width fixed, about 2 columns), `SelectionBoxItemTemplate` = small thumbnail (about 48 px wide) + name on one line.

- [ ] **Step 1: Write failing tests:**
  - `SongEditor_ThemeDropdown_ShowsTiles`: host `SongEditorControl` (Design tab selected) over a view model with 3 themes, open the combo (`comboBox.IsDropDownOpen = true`, settle), assert the popup content (reach it through the combo's `ItemsPanelRoot` / popup child, not the window tree) contains 3 `ThemePreviewImage`s with `Source != null`; screenshot `theme-dropdown-song-editor`.
  - `ItemDock_BlankOption_IsTextOnly` (Review Focus 5): bind the scripture combo with the blank `null` item; assert that item's tile has a zero-size `ThemePreviewImage` and no exception; long `Name` (200 chars) is trimmed (tile `Bounds.Width` equals the fixed width).
- [ ] **Step 2: Run** `--filter ThemePickerUiTests`. Expected: FAIL.
- [ ] **Step 3: Implement** the style file and apply `Classes="themePicker"` to the three `ComboBox`es, removing their inline `ComboBox.ItemTemplate` (keep the `ComboBox.Styles` visibility filters). Add `x:DataType="slideTheme:BaseSlideTheme"` on the tile `DataTemplate`.
- [ ] **Step 4: Run tests; read both screenshots** (open dropdown tiles; closed box with small thumbnail) and fix layout until readable. Expected: PASS.
- [ ] **Step 5: Commit** `feat: tile-style theme dropdowns with thumbnails`.

---

### Task 6: Live check and full build

**Files:** none (verification only).

- [ ] **Step 1:** Full non-incremental build of the solution; expected 0 errors.
- [ ] **Step 2:** `dotnet test HandsLiftedApp.Tests` and `dotnet test HandsLiftedApp.UiTests`; expected all PASS (ignore failures present before this branch, report them).
- [ ] **Step 3:** `tools\devtools.ps1 start`, open a playlist with an existing song, click through: designer lists (edit a theme colour and watch its thumbnail update), Song editor Design tab dropdown, item edit dock Theme flyout dropdown (song and scripture item). Read `tools\devtools.ps1 shot` PNGs. Click the dropdown items for real: a popup-hosted control that silently does nothing is the known trap. `tools\devtools.ps1 stop` afterwards.
- [ ] **Step 4:** Report what was checked and anything not covered (motion-video live frames are intentionally not shown).

---

## Self-review

- Spec coverage: control + cache + refresh (Tasks 2-3), sample content per type and motion badge (Task 1, 3), three XAML sites (Tasks 4-5), blank tile (Task 5), live check (Task 6).
- One deviation from the spec: the cache does not dispose replaced bitmaps (shared by several controls; GC reclaims them). Called out in Task 2.
- Names consistent: `ThemePreviewSpecBuilder.Build`, `ThemeThumbnailCache.GetOrRender/Invalidated/RenderCount/PixelWidth`, `ThemePreviewImage.Theme/Source`, class `themePicker`.
