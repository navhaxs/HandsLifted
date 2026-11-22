# Custom Slide Editor Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the custom slide editor look and behave like PowerPoint/Canva: WYSIWYG canvas, contextual toolbar, tidy layout, on-canvas text editing, keyboard shortcuts and snapping.

**Architecture:** One shared SkiaSharp drawer (`CustomSlideDrawer`) renders a `CustomSlide` for both the projector (`CustomSlideSpecBuilder`) and the editor canvas (`CustomSlideEditorCanvas`). The designer keeps its existing `SelectedAdorner` and drives it through transparent per-element proxy controls, so hit-testing and resize handles keep working unchanged. Toolbar edits write the same model properties as today, so the existing `CustomSlideEditRecorder` captures undo. Inline editing is an overlay `TextBox` that writes `Text` once on commit.

**Tech Stack:** C#, Avalonia 12.0.5, SkiaSharp, ReactiveUI, MSTest (`HandsLiftedApp.Tests`, `DispatcherTestThread.Run` for ReactiveObject tests).

**Spec:** `docs/superpowers/specs/2026-11-22-custom-slide-editor-polish-design.md`

## Global Constraints

- Avalonia 12 (12.0.5); do not bump packages. Never cast `VisualRoot` to `Window`; use `TopLevel.GetTopLevel(...)` or the logical-tree walk in CLAUDE.md. New views get `x:DataType` (stricter compiled bindings).
- Slide coordinate system stays 1920x1080 (`CustomSlide.SlideWidth/SlideHeight`); zoom is view-only. `SlideElements[0]` is frontmost.
- Saved XML: only **add** optional `Italic` / `Underline` fields; old files must load (both default false).
- Undo goes through `CustomSlideEditRecorder`; multi-property toolbar actions use `BeginBatch(...)`; new model properties must be in the recorder snapshot. Tests call `UndoStack.Clear()` after setup.
- Edit-mode text is a visible `TextBox` matched to the Skia layout (family, size, weight, italic, colour, alignment, line height, zero padding, wrap width = element width). Justify falls back to Left in Skia and the toolbar button gets a "not yet supported" tooltip.
- `LineHeightEm` is the source of truth; the int `LineHeight` is the shared value the designer and builder both read.
- Toolbar buttons are `Focusable="False"`. Clicking empty canvas deselects; clicking inside an element rectangle selects it even when its fill is transparent (topmost wins).
- Do not delete or change `CustomSlideRender` (still used for thumbnails in `ItemSlidesView`, `GroupItemsEditor`, `MediaGroupItemEditor`, `SlideRender.axaml`).
- Rich text is out of scope; keep "one style per element" confined to the model style properties, `CustomSlideDrawer.DrawText`, and the toolbar's apply-to-whole-element.
- Tooling: use Glob/Grep, never shell `find`/`grep`. Log calls use "-" not an em dash.
- Test command: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~<ClassName>"`.

## Review Focus

1. Empty slide / text element with empty or whitespace text: drawer draws only the background, no crash, selection still works.
2. Image element with missing file, bad path or undecodable data: drawer skips it, its proxy stays selectable and resizable.
3. A single word wider than the box (or a box narrower than one character): wraps without looping, text is clipped to the box, no exception.
4. Old XML saved before this change (no `Italic`/`Underline`) loads with both false and re-saves.
5. Inline edit: Esc with unchanged text adds no undo step; a real change is exactly one "Edit Text" step; Delete/nudge/duplicate with nothing selected is a no-op.

---

### Task 1: Fix vertical-alignment helper properties

**Files:**
- Modify: `HandsLiftedApp.Data/Models/SlideElement/SlideElement.cs` (the four `_calculatedVerticalAlignment*` `ToProperty` calls)
- Test: `HandsLiftedApp.Tests/Models/TextElementAlignmentTests.cs` (create)

**Interfaces:**
- Produces: `TextElement.CalculatedVerticalAlignmentTop/Center/Bottom/Stretch` raise `PropertyChanged` under their own names and track `VerticalAlignment`.

- [ ] **Step 1: Write failing tests** (inside `DispatcherTestThread.Run`): `VerticalAlignmentBottom_RaisesItsOwnHelperProperty` (set `VerticalAlignment = Bottom`; assert `PropertyChanged` fired for `"CalculatedVerticalAlignmentBottom"`, `CalculatedVerticalAlignmentBottom == true`, `CalculatedVerticalAlignmentCenter == false`) and `VerticalAlignmentChange_DoesNotRaiseHorizontalHelpers` (assert no `CalculatedTextAlignmentRight`/`Left`/`Center`/`Justify` notification).
- [ ] **Step 2: Run, expect FAIL** (helpers are registered under the horizontal names).
- [ ] **Step 3: Point each `ToProperty(this, x => x.CalculatedTextAlignment*)` of the four vertical helpers at its own `CalculatedVerticalAlignment*` property.**
- [ ] **Step 4: Run `TextElementAlignmentTests` and `CustomSlideEditRecorderTests`, expect PASS.**
- [ ] **Step 5: Commit** `fix: vertical alignment helpers raise their own property names`.

### Task 2: Italic, Underline and IsBold on TextElement

**Files:**
- Modify: `HandsLiftedApp.Data/Models/SlideElement/SlideElement.cs`, `HandsLiftedApp.Core/Undo/CustomSlideEditRecorder.cs` (`ElementState.Of`, `TextState`, `Key`, `CopyTo`, `ElementState.Describe`)
- Test: `HandsLiftedApp.Tests/Models/TextElementStyleTests.cs` (create); extend `HandsLiftedApp.Tests/Undo/CustomSlideEditRecorderTests.cs`

**Interfaces:**
- Produces: `bool TextElement.Italic`, `bool TextElement.Underline` (both `RaiseAndSetIfChanged`, default false, serialized); `[XmlIgnore] bool TextElement.IsBold { get; set; }` (true when `FontWeight == FontWeight.Bold`; setter writes `Bold`/`Normal`; raises `PropertyChanged("IsBold")` whenever `FontWeight` changes).

- [ ] **Step 1: Write failing tests:** `Xml_RoundTrips_ItalicAndUnderline`; `Xml_WithoutItalicUnderline_LoadsAsFalse` (serialize a `CustomSlide`, strip the two elements from the string, deserialize); `IsBold_Setter_TogglesFontWeight_AndRaisesNotification`; recorder tests `ItalicToggle_IsUndoable_AsFormatText` (undo menu text `"Undo Format Text"`, undo restores false) and `UnderlineToggle_IsUndoable`.
- [ ] **Step 2: Run, expect FAIL (compile errors then assertion failures).**
- [ ] **Step 3: Add the properties; add `Italic`/`Underline` to `TextState` (init props, in `Key`, `CopyTo`, `Of`) and to the "Format Text" comparison in `ElementState.Describe`.**
- [ ] **Step 4: Run the two test classes, expect PASS.**
- [ ] **Step 5: Commit** `feat: italic and underline on text elements`.

### Task 3: CustomSlideTextLayout (shared text layout)

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/Builders/CustomSlideTextLayout.cs`
- Test: `HandsLiftedApp.Tests/Render/CustomSlideTextLayoutTests.cs` (create)

**Interfaces:**
- Produces:
  - `record LayoutLine(string Text, float X, float Baseline, float Width)`
  - `static IReadOnlyList<LayoutLine> CustomSlideTextLayout.Layout(TextElement el, SKFont font)` - word-wraps to `el.Width` (existing `WrapText` behaviour, moved here; a word wider than the box sits on its own line), line height `el.LineHeight > 0 ? el.LineHeight : FontSize * 1.2f`, vertical placement Top/Center/Bottom (Stretch = Top), horizontal Left/Center/Right (Justify = Left), baseline = line top + ascent.
  - `static SKFont CustomSlideTextLayout.CreateFont(TextElement el)` - fresh `SKTypeface` from `FontFamilyAsText` with bold weight and `SKFontStyleSlant.Italic` when `el.Italic`, falls back to `SKTypeface.Default`. Caller disposes the font and its typeface.

- [ ] **Step 1: Write failing tests:** `LeftCentreRight_PlaceLineXAsExpected` (for an element at X=100,W=800: Left X==100; Centre X==100+(800-w)/2; Right X==100+800-w, w = `LayoutLine.Width`); `TopCentreBottom_PlaceFirstBaselineAsExpected` (compare first baseline across the three; Center sits halfway between Top and Bottom within 1px); `LongWord_OnOwnLine_DoesNotLoop` (one 200-char word in W=50 yields exactly 1 line, test completes); `ExplicitNewlines_ProduceLines`; `EmptyText_ReturnsNoLines`; `LineHeightEm_ChangesBaselineSpacing`.
- [ ] **Step 2: Run, expect FAIL (type missing).**
- [ ] **Step 3: Implement both methods in the new static class, lifting `WrapText` and the vertical/horizontal math out of `CustomSlideSpecBuilder.DrawTextElement` unchanged where it already agrees with the tests.**
- [ ] **Step 4: Run `CustomSlideTextLayoutTests`, expect PASS.**
- [ ] **Step 5: Commit** `feat: shared custom slide text layout`.

### Task 4: CustomSlideDrawer, builder delegates to it

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/Builders/CustomSlideDrawer.cs`, `HandsLiftedApp.Core/Render/Skia/SlideImageCache.cs`
- Modify: `HandsLiftedApp.Core/Render/Skia/Builders/CustomSlideSpecBuilder.cs`
- Test: `HandsLiftedApp.Tests/Render/CustomSlideDrawerTests.cs` (create)

**Interfaces:**
- Consumes: `CustomSlideTextLayout.Layout/CreateFont` (Task 3).
- Produces:
  - `static void CustomSlideDrawer.Draw(SKCanvas canvas, CustomSlide slide, TextElement? hideTextOf = null)` - clears nothing; draws background (graphic or colour), then elements in true z-order (iterate `SlideElements` from last to first so `[0]` is on top), text box fill + text (`Italic`, `Underline` via `font.GetFontMetrics`' underline position/thickness, fallback `FontSize*0.08`), clipped to the box. `hideTextOf` skips that element's text but still draws its fill.
  - `static SKBitmap? SlideImageCache.Get(string path)` - handles `avares://` via `AssetLoader`, `File.Exists`, returns null on any failure, caches by path + last-write time, bounded (evict oldest beyond 32).
- Behaviour change to call out in the commit: element z-order now follows the list (previously all text was drawn above all images).

- [ ] **Step 1: Write failing tests** drawing to a 1920x1080 `SKBitmap`: `EmptySlide_DrawsOnlyBackground` (pixel at (10,10) equals background colour); `TextBoxFill_IsDrawnAtElementRect`; `HideTextOf_SkipsGlyphsButKeepsFill` (count of non-fill pixels inside the box is 0 when hidden, >0 when shown); `Underline_AddsPixelsBelowBaseline` (underline on vs off differ in the row at baseline+~underline offset); `Italic_ChangesRenderedPixels`; `ZOrder_FrontElementWinsOverlap` (two filled boxes overlapping, element 0's colour at the overlap); `MissingImageFile_IsSkippedWithoutThrowing`; `SlideImageCache_Get_ReturnsNullForMissingFile_AndSameInstanceForRepeatedHit`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement `SlideImageCache` and `CustomSlideDrawer`; change `CustomSlideSpecBuilder.Build` to create the surface, call `CustomSlideDrawer.Draw(canvas, slide)`, and snapshot as today (keep its try/catch and logging).**
- [ ] **Step 4: Run `CustomSlideDrawerTests` and the existing render/spec tests (`FullyQualifiedName~Render`), expect PASS.**
- [ ] **Step 5: Commit** `feat: shared custom slide drawer used by the projector path`.

### Task 5: Designer canvas draws through the shared drawer

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/CustomSlideEditorCanvas.cs`
- Modify: `HandsLiftedApp.Core/Views/Editors/MediaGroupItemEditor/SlideItemEditor/Parts/CustomSlideDesigner.axaml` and `.axaml.cs`, `.../CustomSlideEditor.axaml` (remove the Preview tab and its `CustomSlideRender Name="Render"`)
- Test: `HandsLiftedApp.Tests/Render/CustomSlideEditorCanvasTests.cs` (create)

**Interfaces:**
- Consumes: `CustomSlideDrawer.Draw` (Task 4).
- Produces:
  - `sealed class CustomSlideEditorCanvas : Control` with `CustomSlide? Slide { get; set; }` and `TextElement? HiddenTextElement { get; set; }`; renders via `ICustomDrawOperation` calling `CustomSlideDrawer.Draw(canvas, Slide, HiddenTextElement)` scaled to its bounds; invalidates on slide property, element property, element collection and background changes (unsubscribes on detach).
  - `CustomSlideDesigner.HiddenTextElement` passthrough (used by Task 9).

- [ ] **Step 1: Write failing tests** (`DispatcherTestThread.Run`): `Canvas_InvalidatesWhenElementTextChanges` (hook a counter via `InvalidateVisual` override or a `RenderRequested` internal counter exposed for tests - choose the simplest; assert increments after `Text`, `X`, `Italic`, `SlideElements.Add`, `BackgroundAvaloniaColour` changes); `Canvas_StopsListening_AfterDetach` (no increments after `Slide = null`).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement the control. In `CustomSlideDesigner`: place a `CustomSlideEditorCanvas` (1920x1080) under `Root` inside the existing grid; change `Render(CustomSlide)` to rebuild only transparent proxy `Border`s (one per element, same reverse order, `Background="Transparent"`, `DataContext` = element, `Canvas.Left/Top/Width/Height` two-way bound to X/Y/Width/Height exactly as `CreateControlForElement` does today, `PointerPressed` selects, calls `ZoomBorder.Focus()` and sets `e.Handled`). `SelectElement`/`AddSelected` keep working because they look up the proxy by `DataContext`. Remove the Aquamarine/SlateBlue backgrounds (use the theme background) and set scrollbar visibility to `Auto`. Leave `CustomSlideRender` untouched.**
- [ ] **Step 4: Build, run `CustomSlideEditorCanvasTests` and `CustomSlideEditRecorderTests`, expect PASS. Manual check: open a custom slide, select/drag/resize an element, confirm the canvas text shows the chosen font and the handles still follow.**
- [ ] **Step 5: Commit** `feat: designer canvas renders through the shared drawer`.

### Task 6: SlideElementCommands (element actions, no UI)

**Files:**
- Create: `HandsLiftedApp.Core/Views/Editors/MediaGroupItemEditor/SlideItemEditor/SlideElementCommands.cs` (namespace `HandsLiftedApp.Core.Views.Editors`)
- Modify: `.../CustomSlideEditor.axaml.cs` (Duplicate/Delete/MoveUp/MoveDown/Add handlers call the commands)
- Test: `HandsLiftedApp.Tests/Views/SlideElementCommandsTests.cs` (create)

**Interfaces:**
- Produces (all `static`, on `SlideElementCommands`):
  - `SlideElement? Duplicate(CustomSlide slide, SlideElement source)` - XML round-trip copy, offset +20/+20 (clamped inside the slide), inserted directly above the source (index of source, i.e. in front of it), returns the copy.
  - `bool Delete(CustomSlide slide, SlideElement? element)` - false when null/not present.
  - `bool BringForward(CustomSlide slide, SlideElement element)` / `bool SendBackward(...)` - swap toward index 0 / the end; false at the end of range.
  - `bool Nudge(SlideElement? element, int dx, int dy)` - false when null; no clamping.
  - `TextElement AddText(CustomSlide slide)` / `ImageElement AddImage(CustomSlide slide)` - new element centred (`SlideWidth/2 - Width/2`), inserted at index 0 (frontmost); `AddText` defaults `Text = "Text"`.

- [ ] **Step 1: Write failing tests:** `Duplicate_CopiesAllTextStylesIncludingItalic`; `Duplicate_PlacesCopyInFrontOfSource`; `Delete_Null_ReturnsFalse`; `BringForward_AtFront_ReturnsFalse`; `SendBackward_SwapsWithNextElement`; `Nudge_Null_ReturnsFalse`; `Nudge_MovesByDelta`; `AddText_IsCentredAndFrontmost`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement; replace the code-behind bodies (keep method names so the XAML wiring still compiles) with calls into the commands.**
- [ ] **Step 4: Run `SlideElementCommandsTests`, expect PASS.**
- [ ] **Step 5: Commit** `refactor: element commands extracted from the editor code-behind`.

### Task 7: Contextual toolbar control

**Files:**
- Create: `.../SlideItemEditor/Parts/CustomSlideToolbar.axaml` and `.axaml.cs`, `.../Parts/EditorToggleStyles.axaml`
- Test: `HandsLiftedApp.Tests/Views/CustomSlideToolbarTests.cs` (create)

**Interfaces:**
- Consumes: `TextElement.IsBold/Italic/Underline`, alignment helpers (Tasks 1-2), `SlideElementCommands` (Task 6).
- Produces: `CustomSlideToolbar : UserControl` with `SlideElement? SelectedElement { get; set; }` (styled property), `CustomSlide? Slide`, and events `AddTextRequested`, `AddImageRequested`, `ReplaceImageRequested`, `FitImageRequested`, `DuplicateRequested`, `DeleteRequested`, `BringForwardRequested`, `SendBackwardRequested`, `UndoRequested`, `RedoRequested`; methods `SetUndoState(bool canUndo, bool canRedo, string? undoText, string? redoText)`. Sections visible by selection: none = slide background colour + background graphic; `TextElement` = font family, size, B/I/U, h-align (Left/Centre/Right/Justify with tooltip on Justify), v-align (Top/Centre/Bottom), text colour, box fill, line height; `ImageElement` = Replace, Fit. Always: Undo, Redo, Add Text, Add Image; with a selection also Duplicate, Delete, Forward, Back. Every `Button`/`ToggleButton` sets `Focusable="False"`. `EditorToggleStyles.axaml` holds the single shared icon-toggle style that replaces the three copy-pasted `UniformGrid.Styles` blocks.
- Implementation note: set the inner formatting panel's `DataContext` imperatively when `SelectedElement` changes (CLAUDE.md element-name binding gotcha); font list reuses the `FontManager.Current.SystemFonts` pattern from `FontComboBox_OnLoaded`.

- [ ] **Step 1: Write failing tests** (`DispatcherTestThread.Run`, control created in code): `TextSelected_ShowsTextSection_HidesImageSection`; `NothingSelected_ShowsSlideSection`; `ImageSelected_ShowsReplaceAndFit`; `AllButtons_AreNotFocusable` (walk the visual/logical tree); `BoldToggle_WritesFontWeight` (set `IsChecked` on the bold toggle, assert `FontWeight == Bold`); `DeleteButton_RaisesDeleteRequested`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement control and style file with `x:DataType` set; no logic beyond binding to the element's properties and raising events.**
- [ ] **Step 4: Run `CustomSlideToolbarTests`, expect PASS.**
- [ ] **Step 5: Commit** `feat: contextual custom slide toolbar control`.

### Task 8: Editor layout with toolbar, layers, status bar

**Files:**
- Modify: `.../CustomSlideEditor.axaml` and `.axaml.cs`, `.../Parts/CustomSlideDesigner.axaml` and `.axaml.cs`
- Test: `HandsLiftedApp.Tests/Views/CustomSlideEditorLayoutTests.cs` (create)

**Interfaces:**
- Consumes: `CustomSlideToolbar` (Task 7), `SlideElementCommands` (Task 6).
- Produces: `CustomSlideDesigner.ZoomPercent { get; }`, `ZoomIn()`, `ZoomOut()`, `FitToView()`, event `ZoomChanged`; editor layout: row 0 toolbar, row 1 = layers list | canvas | slim properties, row 2 status bar (zoom -, percent, +, Fit; replaces the zoom controls in the designer).

- [ ] **Step 1: Write failing tests:** `ToolbarSelectedElement_FollowsLayersSelection` (selecting in the list sets `Toolbar.SelectedElement`); `ToolbarDelete_RemovesElementThroughCommands`; `ZoomPercent_ReflectsZoomBorder_AndZoomIn_Increases`; `PropertiesPanel_NoLongerContainsFontOrAlignmentControls` (guards the slimming).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Restructure the XAML: move Undo/Redo/Add buttons into the toolbar (keep `RefreshUndoButtons` feeding `SetUndoState`); rename the list to "Layers" (keep its context menu); properties panel keeps Text box, X/Y/Width/Height, image path, slide background; delete the font/size/alignment/colour blocks and the three toggle-group copies (now in the toolbar); wire toolbar events to `SlideElementCommands`, `ChangeImageElement_OnClick`/`FitElementToImage`, and `ListBox.SelectedItem`.**
- [ ] **Step 4: Build; run `CustomSlideEditorLayoutTests`. Manual: open the editor and walk the toolbar once per element type (this is a UI wiring change that tests cannot fully prove).**
- [ ] **Step 5: Commit** `feat: custom slide editor layout with toolbar and status bar`.

### Task 9: Inline text editing overlay

**Files:**
- Create: `.../SlideItemEditor/Parts/InlineTextEditor.cs`
- Modify: `.../Parts/CustomSlideDesigner.axaml` and `.axaml.cs`, `.../CustomSlideEditor.axaml.cs` (new text element enters edit mode)
- Test: `HandsLiftedApp.Tests/Views/InlineTextEditorTests.cs` (create)

**Interfaces:**
- Consumes: `CustomSlideEditorCanvas.HiddenTextElement` (Task 5), `SlideElementCommands.AddText` (Task 6).
- Produces:
  - `sealed class InlineTextEditor : TextBox` with `static void ApplyStyle(TextBox box, TextElement el)` (FontFamily from `FontFamilyAsText`, `FontSize`, `FontWeight`, `FontStyle` from `Italic`, `Foreground`/`Background` from the element colours, `TextAlignment`, `LineHeight`, `VerticalContentAlignment` from `VerticalAlignment`, `Padding=0`, `BorderThickness=0`, `TextWrapping.Wrap`, `AcceptsReturn=true`, `Width/Height` = element Width/Height), `void Begin(TextElement el, bool selectAll)`, event `Committed(TextElement el, string newText)`, `bool IsEditing`.
  - `CustomSlideDesigner.BeginEdit(TextElement el, bool selectAll = false)` / `CommitEdit()`: overlay lives in a canvas layer above the proxies inside the zoomed 1920x1080 grid, positioned at `Canvas.Left/Top = el.X/Y`; sets `HiddenTextElement` while editing; `Esc` or `LostFocus` commits; commit assigns `el.Text` only when changed (one recorder "Edit Text" step); while editing, element property changes (size, colour, family, italic...) re-run `ApplyStyle`.
  - Entry points: double-click on a text proxy, `Enter`/`F2` when a text element is selected and the canvas has focus, and `ButtonAddTextElement` / toolbar Add Text calls `BeginEdit(newEl, selectAll: true)`.

- [ ] **Step 1: Write failing tests:** `ApplyStyle_MapsEveryTextProperty` (assert each mapped property incl. italic -> `FontStyle.Italic`, element width/height); `Commit_WithUnchangedText_AddsNoUndoStep`; `Commit_WithNewText_IsOneEditTextStep` (use `CustomSlideEditRecorder` + `UndoStack`); `Begin_HidesCanvasText_CommitRestoresIt`; `FormatChangeDuringEdit_ReappliesStyle` (set `FontSize` on the element while editing, box `FontSize` follows); `AddedTextElement_StartsEditingWithAllSelected`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement as described. Overlay must also stop pointer events from reaching the proxies underneath (set `e.Handled`).**
- [ ] **Step 4: Run `InlineTextEditorTests`, expect PASS. Manual (required, this is the "button does nothing" class): double-click, type, Esc; Enter/F2; Add Text; change font size from the toolbar mid-edit; confirm the text does not visibly jump for left/centre/right and top/centre/bottom.**
- [ ] **Step 5: Commit** `feat: edit text on the canvas`.

### Task 10: Keyboard shortcuts

**Files:**
- Modify: `.../Parts/CustomSlideDesigner.axaml.cs` (the existing `ZoomBorder_KeyDown`)
- Test: `HandsLiftedApp.Tests/Views/CustomSlideDesignerKeyboardTests.cs` (create)

**Interfaces:**
- Consumes: `SlideElementCommands.Delete/Nudge/Duplicate` (Task 6), `InlineTextEditor.IsEditing` (Task 9).
- Produces: `internal bool CustomSlideDesigner.HandleKey(Key key, KeyModifiers modifiers)` (returns whether handled) called from `ZoomBorder_KeyDown`. Bindings: `Delete`/`Back` remove the selected element; arrows nudge 1px, `Shift`+arrow 10px; `Ctrl+D` duplicates and selects the copy; `Esc` deselects; `Enter`/`F2` start edit on a selected text element. All no-ops (return false) with nothing selected or while `IsEditing`. Existing `F/U/R/T` zoom keys keep working when not editing.

- [ ] **Step 1: Write failing tests:** `Delete_RemovesSelected_NoSelection_IsNoOp`; `Arrow_Nudges1_ShiftArrow_Nudges10`; `CtrlD_DuplicatesAndSelectsCopy`; `Esc_Deselects`; `Keys_AreIgnoredWhileEditing`; `Nudge_IsOneUndoStepForRepeatedPresses` (clock injected as in `DraggingAnElement_MergesIntoOneStep`).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement `HandleKey` and call it; mark the event handled when it returns true.**
- [ ] **Step 4: Run `CustomSlideDesignerKeyboardTests` plus `CustomSlideEditRecorderTests`, expect PASS.**
- [ ] **Step 5: Commit** `feat: keyboard shortcuts for the slide designer`.

### Task 11: Edge and element snapping

**Files:**
- Create: `HandsLiftedApp.Core/Views/Editors/SnapCalculator.cs`
- Modify: `HandsLiftedApp.Core/Views/Editors/SelectedAdorner.axaml.cs` (`OnPointerMovedDrag`, `UpdateSnapLines`), designer sets the new `SnapTargets`/`SlideSize` inputs
- Test: `HandsLiftedApp.Tests/Views/SnapCalculatorTests.cs` (create)

**Interfaces:**
- Produces: `record SnapResult(Rect Rect, double? GuideX, double? GuideY)`; `static SnapResult SnapCalculator.Snap(Rect dragged, IReadOnlyList<Rect> others, double slideWidth, double slideHeight, double threshold = 10)` - snaps the dragged rect's left/centre/right edges to the slide's 0/centre/width and to each other rect's left/centre/right (and top/centre/bottom likewise), nearest candidate within the threshold per axis, returns the guide line coordinate for each snapped axis. `SelectedAdorner` gains `IReadOnlyList<Rect> SnapTargets` and `Size SlideSize` styled/plain properties; the designer fills them (other elements' rects, 1920x1080) when selection starts and on element change; `UpdateSnapLines` draws guides at the returned coordinates instead of fixed centre lines.

- [ ] **Step 1: Write failing tests:** `SnapsCentreToSlideCentre_WithinThreshold`; `SnapsLeftEdgeToSlideLeft`; `SnapsToOtherElementsRightEdge`; `NoSnap_OutsideThreshold_ReturnsOriginalRectAndNoGuides`; `PicksNearestCandidate`; `AxesSnapIndependently`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement `SnapCalculator`; replace the centre-snap block in `OnPointerMovedDrag` with a call to it (the dragged element's own rect is excluded from `others`).**
- [ ] **Step 4: Run `SnapCalculatorTests`, expect PASS. Manual: drag near slide edge, centre and another element; guide lines appear and the rect snaps.**
- [ ] **Step 5: Commit** `feat: edge and element snapping in the slide designer`.

### Task 12: Docs and manual checklist

**Files:**
- Modify: `CLAUDE.md` (Key file locations table, a short "Custom slide editor" note), create `docs/superpowers/plans/2026-11-22-custom-slide-editor-manual-checklist.md`

- [ ] **Step 1: Add to CLAUDE.md: `CustomSlideDrawer` is the single custom slide drawer (projector and editor); `CustomSlideRender` is thumbnails only; the designer uses transparent proxy controls so `SelectedAdorner` keeps working; element z-order is list order with `[0]` frontmost; the inline editor commits `Text` once on commit.**
- [ ] **Step 2: Write the checklist** covering each manual check named in Tasks 5, 8, 9, 10, 11 plus: projector output matches the editor canvas for a slide using family, bold, italic, underline, colours, an image and a background graphic.
- [ ] **Step 3: Commit** `docs: custom slide editor notes and manual checklist`.
