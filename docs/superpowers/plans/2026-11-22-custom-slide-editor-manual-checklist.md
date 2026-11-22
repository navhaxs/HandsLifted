# Custom slide editor - manual click-through checklist

Run in the real app (open a custom slide in the slide editor window). Check each item in light and dark theme where noted.

## End to end

- [ ] A slide with font family, bold, italic, underline, colours, an image and a background graphic looks the same in the editor canvas and on the projector output.

## Canvas

- [ ] Clicking an element selects it and the handles land on it; clicking empty canvas deselects.
- [ ] Dragging and resizing update the drawing live.
- [ ] Changing the background colour or graphic shows immediately and is not drawn twice.
- [ ] Canvas surface colour looks right in light and dark theme.
- [ ] Selecting a row in the layers list selects the element on the canvas.
- [ ] Add / delete / reorder updates the drawing and the click-through order on the canvas.
- [ ] On open the slide is fitted, including when the window is maximised or resized.

## Toolbar

- [ ] Toolbar wraps correctly when the window is narrow and wide.
- [ ] Material icon glyphs render; checked, hover and pressed states look right in light and dark.
- [ ] Undo and Redo are disabled when there is nothing to undo/redo; tooltips show; state follows when switching slides.
- [ ] Add Text and Add Image place the new element frontmost and centred; one Undo removes it.
- [ ] Bring forward / Send backward in the toolbar keep the selection (press twice to confirm).
- [ ] Replace image (toolbar button and the "..." button) works, and one Undo restores the old image.
- [ ] Fit to image works, and one Undo restores the old size.
- [ ] Font list shows each font in its own face.
- [ ] Colour pickers do not steal focus (an open inline edit stays open).
- [ ] Typing a number into a NumericSliderBox applies it.
- [ ] Changing the background graphic from the toolbar is applied.

## Layers

- [ ] Per-element Up / Down in the layers list reorder elements (selection kept, press twice).
- [ ] Per-element Duplicate and Delete work from the layers list.
- [ ] Duplicating an italic or underlined text element keeps its italic/underline.

## Properties

- [ ] Typing in the properties TextBoxes and sliders works normally (canvas shortcuts do not interfere).
- [ ] Generate XML in the Code tab produces the XML.

## Zoom / status bar

- [ ] F, U, R, T zoom keys work after clicking on the canvas.
- [ ] Mouse wheel zooms with the pointer over elements and over empty canvas; the % label updates.
- [ ] Status-bar zoom -, +, and Fit work.

## Inline editing

- [ ] Double-click an unselected text element opens the inline editor; so does double-click on an already selected one.
- [ ] Type, then Esc: handles return, canvas is focused, and Ctrl+Z undoes the whole edit in one step.
- [ ] Clicking empty canvas, another element, or a layers row while editing commits the text.
- [ ] Add Text opens the inline editor with the placeholder selected; the first keystroke replaces it.
- [ ] Bold, Italic, alignment and colour while editing restyle the text and keep the editor open.
- [ ] Font size, line height and font box end the edit, then apply.
- [ ] 3x3 alignment at line height 1.0 and 1.5 does not jump between edit and display; first-line ascenders are not clipped at 1.5 or more.
- [ ] Pointer over the inline box causes no pan, selection, or :focus/:pointerover flash.
- [ ] Type in a text box and close the slide editor window without Esc: text is committed, and kept after reopening.

## Keyboard

- [ ] After clicking an element: arrow keys nudge, Shift+arrows nudge 10px.
- [ ] Delete and Backspace delete the selected element.
- [ ] Ctrl+D duplicates (copy appears selected on canvas and in the layers list).
- [ ] Esc deselects.
- [ ] Enter / F2 starts inline edit on a text element and does nothing on an image.
- [ ] A burst of nudges then Ctrl+Z undoes as one step.
- [ ] Adorner handles follow a nudge and a copy.
- [ ] Typing in the inline box is unaffected by the canvas shortcuts.

## Snapping

- [ ] Dragging near slide edges, centre and other elements snaps, and guide lines appear.
- [ ] Dragging slowly away from a snap point lets you leave it.
- [ ] An element starting at x=0 can be dragged.
- [ ] Guides vanish after release.
- [ ] Alignment is exact when zoomed in and out.

## Undo

- [ ] Undo/redo of a move or resize moves both drawing and handles.
- [ ] Undoing a drag is one step.
- [ ] Add Text / Add Image: one Undo removes it.

## Added after the final review

- [ ] Open a playlist saved BEFORE this change that has a custom slide where an image overlaps text (image added first): check the projector output, the editor canvas and the slide thumbnails. Elements now draw in list order with index 0 frontmost, so an image earlier in the list than the text now covers it on the projector (previously all text was drawn above all images). Decide whether to accept this or add a one-time normalization on load.
- [ ] Snapping at the fitted zoom (not 100%): drag an element until its edge snaps to the slide centre, then drag slowly away; it must leave the snap point after about 10 slide px of total travel. Repeat with a diagonal drag.
- [ ] Release a drag while snapped with the pointer ending outside the element: the red guide lines must disappear.
- [ ] Drag responsiveness with a large photo element (10+ MP) and a background graphic on the slide: dragging any element stays smooth, memory does not spike.
- [ ] While the inline editor is open, click the toolbar buttons Duplicate, Delete, Bring forward, Add Image and Undo: the copy contains the typed text, no selection handles cover the box, and Undo undoes the typing as one "Edit Text" step first.
- [ ] Switch to another slide in the media group editor's thumbnail strip while typing inline: the text lands on the original slide and is one Undo step there when you switch back.
- [ ] Switch Visual -> Code -> Visual with an element selected, and again while editing inline: no stray handles are left behind, the canvas still redraws and the proxies still select/drag.
- [ ] Thumbnails and the stage display's next-slide view for a slide that uses italic, underline, bold or a custom font: they use the older Avalonia renderer and are known not to match the projector for font family/weight/italic/underline (follow-up: render them through CustomSlideDrawer).
- [ ] Select a row in the layers list, then press Delete / Ctrl+D: known limit - canvas shortcuts do nothing until the canvas has focus (follow-up: forward keys from the layers list).
- [ ] Change a toolbar formatting option (bold, colour, size) and confirm the slide thumbnail strip and the projector output both update through the shared drawer and image cache.

## Known limits / deferred

- Underline is not shown while typing in the inline box (shown once committed).
- Right-aligned wrapped lines sit about one space further left while typing.
- The font size, line height and font boxes in the toolbar end the inline edit before applying.
- Canvas keyboard shortcuts need canvas focus; they do not fire after clicking a row in the layers list.
- Justify renders as Left.
- SlideImageCache is bounded by entry count (32), not bytes - many very large photos can hold a lot of memory.
- CustomSlideRender (thumbnails, stage next-slide view) ignores font family, weight, italic and underline.
