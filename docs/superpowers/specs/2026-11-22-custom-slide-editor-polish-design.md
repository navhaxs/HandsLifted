# Custom slide editor polish — design

Date: 2026-11-22. Status: draft for approval.

## Intent and why

`CustomSlideEditor` (the editor for free-form slides made of text boxes and images) feels rough next to PowerPoint
and Canva. The user wants it to feel like those tools: tidy layout, a real edit toolbar, and typing into a selected
text box directly on the canvas. Rich text (per-selection formatting) is wanted too, but explicitly as a **future
goal**, not part of this work. Nothing built now should make it harder.

## Current state (what exists today)

- `CustomSlideEditor.axaml`: three columns - element `ListBox` (with Undo/Redo/Add Text/Add Image stacked at its
  bottom), a `TabControl` (Visual / Code / Preview), and a properties panel (selected element + slide settings).
- `CustomSlideDesigner` hosts a `ZoomBorder` > 1920x1080 `Grid` > `Canvas Root`. `Root` holds Avalonia controls built
  by `CustomSlideRender.CreateControlForElement` (a `TextBlock` in a `Border`, or an `Image`). `SelectedAdorner`
  draws 8 resize thumbs + a drag thumb, with centre snapping. Debug colours (Aquamarine, SlateBlue) and permanent
  scrollbars are still in place. Zoom slider sits bottom-right.
- Final output is drawn by `CustomSlideSpecBuilder` (SkiaSharp) into a bitmap background. The designer does **not**
  use it, so the two disagree: the designer ignores font family and weight, the Skia path treats Justify as Left,
  neither supports italic/underline.
- Model `SlideElement` / `TextElement` / `ImageElement` (`HandsLiftedApp.Data/Models/SlideElement/SlideElement.cs`):
  one style per text element. `FontWeight` exists but has no UI; no italic/underline properties.
- Bugs found: the "B / I / U" toggle group in the properties panel is bound to `CalculatedTextAlignment*`
  (does nothing bold/italic/underline); the three vertical-alignment `Calculated*` helpers are registered with the
  *horizontal* property names in `ToProperty`, so their change notifications are wrong; the same ~70-line toggle
  group XAML is copy-pasted three times.
- No keyboard handling on the canvas (no Delete, nudge, Esc, duplicate), no edge snapping, no inline text editing
  (text is edited only in a sidebar `TextBox`).
- Undo/redo already exists via `CustomSlideEditRecorder` (snapshot diff) - see CLAUDE.md "Undo / redo".

## Goals

1. Editor canvas shows what the projector shows (same renderer).
2. A contextual top toolbar for formatting and element actions.
3. Cleaner layout: layers list, canvas, slim properties panel, status bar.
4. Edit text on the canvas: double-click / Enter / F2 to start, Esc or click-away to commit.
5. Keyboard basics: Delete, arrow-key nudge (Shift = larger step), Ctrl+D duplicate, edge snapping.
6. Fix the B/I/U and vertical-alignment bugs; add Italic and Underline to the model and renderer.

## Non-goals

- Rich text / formatted runs inside one text box (future; see below).
- Rotation, multi-select, grouping, shapes, alignment/distribute tools, templates.
- Changing the saved XML format beyond **adding** optional `Italic` / `Underline` fields (old files must load).
- A custom text-editing control (caret, selection, IME, clipboard). That arrives with rich text.

## Anti-goals (failure even if it "works")

- The editor still disagrees with the projector about how text looks.
- Undo/redo for a slide regresses, or one toolbar action becomes several undo steps.
- A new overlay text box that visibly jumps position/size/line breaks between edit and non-edit mode in the common
  case (left/centre/right aligned, top/centre/bottom).
- Code that assumes "one style per text box" in places that rich text will need to change (keep the assumption in
  one small place per layer so it can be swapped).

## Constraints

- Avalonia 12 (12.0.5; do not bump without re-checking third-party package floors, see
  `AVALONIA_12_UPGRADE_NOTES.md`), C#, SkiaSharp. Avalonia 12 specifics that touch this work: compiled bindings are
  stricter (give new views `x:DataType`); never cast `VisualRoot` to `Window`, use `TopLevel.GetTopLevel(...)`;
  window chrome is drawn by `WindowDrawnDecorations`. Project rules in `CLAUDE.md` apply. Notably: resolve the owning `Window` by walking the
  logical tree from inside flyouts/popups; `ReactiveObject` `WhenAnyValue` without selector caps at 7 properties;
  use `SKTypeface` fresh per returned element; test host quirks around dispatcher threads.
- Undo: use the existing `CustomSlideEditRecorder`; toolbar actions that change several properties use
  `BeginBatch(...)`. New model properties must be covered by the recorder snapshot. Tests call
  `UndoStack.Clear()` after setup.
- Dependency direction: model changes live in `HandsLiftedApp.Data`; the Skia path lives in `HandsLiftedApp.Core`.
  Check the .csproj a file actually compiles into, not its namespace.
- No UI in-session verification is possible via devtools MCP (paywalled); the user needs to click through flows.
  Bugs of the "button does nothing" class are the main risk.

## Approaches considered

- **A. Overlay `TextBox` only.** Smallest, but leaves the canvas not matching the output, no model fixes, and
  doubles the work when rich text arrives.
- **B. Custom Skia text editor now.** Pixel exact and rich-text ready, but caret/selection/IME/clipboard make it a
  project of its own. Too much for a polish pass.
- **C. Shared renderer now, overlay editor now, custom editor later.** *Chosen by the user.* Same overlay editor as
  A, but the canvas is drawn by the same Skia code as the final output, model/bug fixes land first, and the later
  swap to B touches one renderer instead of two.

Trade-off accepted: a bigger first phase; text may shift slightly entering/leaving edit mode because `TextBox`
cannot do Justify or vertical alignment, until the custom editor lands.

## Design (phases; each independently reviewable)

### Phase 1 - Foundation

- Add `Italic` and `Underline` (bool) to `TextElement`; round-trip in XML as optional; include in the recorder
  snapshot. Wire `FontWeight` bold toggle to the toolbar later.
- Fix the vertical-alignment `ToProperty` names; replace the B/I/U-bound-to-alignment group.
- Extend `CustomSlideSpecBuilder` text drawing: font family, bold, italic, underline, Justify (real justification
  or documented fallback), line height - single source of truth for text layout. Factor the layout/measure part into
  something the overlay editor and tests can also call.
- Make the designer canvas render through that Skia path (via `SlideCanvas` or equivalent) instead of building
  Avalonia controls per element. `SelectedAdorner` stays as an Avalonia overlay on the `ZoomBorder`, driven by
  element bounds (model coordinates, 1920x1080), with hit-testing against element rectangles in z-order
  (`SlideElements[0]` is frontmost).
- Remove the Preview tab. Keep the Code tab. Remove the debug colours and the permanent scrollbars.
- Extract the toggle-group XAML into one reusable control or style.

### Phase 2 - Layout and toolbar

- Layout: toolbar on top; left = layers list (rename of today's element list: drag-to-reorder is a stretch goal,
  existing context menu stays); centre = canvas; right = slim properties (position/size, slide background);
  bottom = status bar with zoom (fit, +/-, percentage).
- Contextual toolbar:
  - always: Undo, Redo, Add Text, Add Image, and for a selected element Duplicate, Delete, Bring Forward / Send Back.
  - text selected: font family, font size, Bold, Italic, Underline, horizontal alignment, vertical alignment,
    text colour, box fill colour, line height.
  - image selected: Replace image, Fit to image.
  - nothing selected: slide background colour and graphic.
- Properties panel keeps exact numeric X / Y / Width / Height and anything that does not fit the toolbar.
- Toolbar edits to the selected element use the same model properties/undo path as the panel today.

### Phase 3 - On-canvas editing and keyboard

- Edit mode: double-click, Enter or F2 on a selected text element opens an overlay `TextBox` positioned and scaled
  in the zoom-border space, sized to the element box, styled to match (family, size, weight, colour, alignment,
  line height, box fill). The underlying element's text is not drawn while editing so it does not double up.
  Esc or click elsewhere commits; the whole edit is one undo step. Empty text on commit keeps the (empty) element.
- Adding a new text element enters edit mode immediately with placeholder text selected.
- Toolbar formatting while editing applies to the whole element (single-style model) and stays enabled.
- Keyboard (canvas focused, not in edit mode): Delete removes, arrows nudge 1px / Shift 10px, Ctrl+D duplicates,
  Esc deselects. Ctrl+Z/Y handling already in `CustomSlideEditor.OnKeyDown` must still work, and must not fire
  while the overlay `TextBox` has focus (it keeps its own).
- Snapping: today's centre snap plus snapping to slide edges and to other elements' edges/centres; red guide lines
  as now.

### Phase 4 (future, not in this work) - Rich text

Replace the overlay `TextBox` with a custom editor on the shared layout, and add formatted runs to the model and
XML (migration for existing single-style elements). Phases 1-3 should keep "one style per element" confined to the
model's style properties, the spec-builder's single draw call, and the toolbar's "apply to whole element".

## Decided by the user

- Approach C as above; rich text deferred to the future.
- Drop the Preview tab; keep the Code tab.

## Calls made by the designer (Claude) - please check

- Added Italic/Underline to the model now (needed for B/I/U to mean anything).
- Layers list is the existing element list renamed; no drag-reorder required.
- Edge/element snapping and nudge step sizes (1 / 10 px) are my defaults.
- Keep the 1920x1080 coordinate system; zoom is view-only.
- **Edit-mode fidelity:** the overlay is a normal visible `TextBox` whose font, size, weight, colour, alignment, line
  height, padding and wrap width are set to match the Skia layout. Some drift is accepted; the bar is "no visible
  jump in the left/centre/right x top/centre/bottom cases". Justify and vertical stretch are not matched while typing.
  Tests cover those 3x3 cases at the metric level; the user confirms by eye.
- **Designer draws the live element list directly** (each element as its own render item, not the builder's single
  flattened bitmap), so an element's text can be skipped during editing and drags redraw cheaply. Image and text
  drawing code is shared with `CustomSlideSpecBuilder`. The model has no element opacity, so none is added;
  anything the builder lacks today beyond font/italic/underline/justify is out of scope.
- **Line height:** `LineHeightEm` is the source of truth; the toolbar edits it. The int `LineHeight` rounding stays
  as the shared value both the designer and the builder read, so they cannot diverge.
- **Undo while editing:** typing is the `TextBox`'s own undo (Ctrl+Z inside it stays local). Commit makes one slide
  undo step for the text. A toolbar change made mid-edit is its own separate step, applied immediately.
- **Selection and focus:** clicking empty canvas deselects; clicking anywhere inside an element's rectangle selects it
  even if its fill is transparent (topmost wins). The canvas host is the focus target for Delete/nudge/Ctrl+D.
  Toolbar buttons are non-focusable so clicking them does not steal focus from the canvas or the overlay.
- **Image toolbar:** "Fit to image" resizes the element to the picture's own proportions, scaled to fit the slide,
  keeping its centre (existing `FitElementToImage`). "Replace image" reuses today's change-image flow, which already
  fits the box to the new picture, as one undo step.
- **Code tab:** stays exactly as is (shows the slide XML; its edit-to-canvas push is already unimplemented).
  No new round-tripping is in scope.
- **Justify:** the Skia builder falls back to Left and the toolbar button stays, with a tooltip noting it is not yet
  supported; real justification is deferred (likely with rich text, which needs a proper layout anyway).

## Left to the builder

- How the Skia-drawn designer canvas is hosted (extend `SlideCanvas`, or a dedicated control), including how to
  skip drawing an element's text during editing and how to redraw cheaply while dragging/resizing.
- Exact toolbar control choices (Avalonia/Material icons already in use), icon set, and responsive behaviour when
  the window is narrow.
- Test strategy: model/XML round trip, spec-builder output for each style flag, recorder snapshot coverage, plus a
  manual click-through checklist for the UI flows (toolbar, double-click edit, keyboard).
- Whether to split `CustomSlideEditor.axaml` into smaller parts.
