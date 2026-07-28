# Scripture Paragraph Rendering Design

**Date:** 2026-07-27
**Status:** Approved, ready for planning
**Depends on:** Phases 1–4a, the local-USX-source plan, and the scripture add-item entry plan (all merged on `feature/scripture-slide-type`).

## Background

Today, `ScriptureItemInstance` generates one `Slide` per verse (`UpdateVerseSlides` in `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`), each a plain single-font-size string rendered by `ScriptureSlideSpecBuilder` — no superscripts, no paragraph structure, no reflow. Slide identity is `slideId = "{chapter}:{verse}"`, a stable 1:1 mapping used to preserve slide object identity (and cached thumbnails) across regeneration.

This design replaces that entirely with flowing paragraph-style rendering: a reference header on the first slide, verse text in continuous paragraph form with superscript verse numbers, and automatic reflow across as many slides as the passage needs at the user's chosen font settings.

## Goal

`ScriptureItemInstance` renders as: slide 1 has a reference header ("1 Peter 1:13-25") followed by paragraph-style verse text beginning underneath it; verse numbers appear as superscripts; text reflows (splitting mid-verse where necessary) across however many slides are needed given the resolved theme's font family, font size, and paragraph alignment. One-verse-per-slide is removed, not kept as an alternate mode.

## Non-Goals

- No rich markup from the source text (bold/italic/red-letter) — only the verse-number superscript gets special styling.
- No manual override of where a page break falls — fully automatic.
- No user-configurable header-size ratio or superscript size/baseline-offset ratios in this pass — fixed constants (see below).
- No multi-translation support (unchanged from earlier phases).
- No full Phase 4b passage-entry/rich editor — this design adds only the minimal Design/theme-picker UI described below.

## Design Storage: `ScriptureItem.Design`

Font family, font size, and paragraph alignment are **not** new per-item fields — they come from the app's existing shared theme system, reused exactly the way `SongItem` already does it:

- **`ScriptureItem.Design`** (`Guid`, default `Guid.Empty`) — new `[DataMember]` property, an exact mirror of `SongItem.Design` (`HandsLiftedApp.Data/Models/Items/SongItem.cs:26-27`). Points at one of the playlist's shared `Designs` (`BaseSlideTheme` presets).
- The resolved theme supplies `FontFamily`, `FontSize`, `TextAlignment`, and `LineHeightEm` — all of which already exist on `BaseSlideTheme` (`HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs`). No new font-config fields anywhere.
- Selecting a `Design` also carries that preset's **background** (solid color or image) along with the font settings, since it's the same shared theme object — exactly as it already works for Song slides today (`ScriptureSlideSpecBuilder`/`SongSlideSpecBuilder` both build background from `slide.Theme`). This is inherited behavior, not a new decision.
- **`AutofitEnabled`/`AutofitMinFontSizeRatio` are not read by the new paragraph layout path.** Pagination always uses the theme's `FontSize` as-is — the whole point of reflow is a fixed, predictable font size with as many slides as needed; autofit (shrink-to-fit-one-slide) is a one-verse-per-slide-era concept that doesn't apply here.

### Theme resolution

`ScriptureItemInstance` gets a `ResolvedDesignTheme` property mirroring `SongSlideInstance.ResolveTheme` (`HandsLiftedApp.Core/Models/RuntimeData/Slides/SongSlideInstance.cs:28-33,47`): looks up `Design` in `ParentPlaylist.Designs`, falling back to `Globals.Instance.AppPreferences?.DefaultTheme` when `Design == Guid.Empty` or not found — replacing today's always-default fallback (`ScriptureSlideInstance.cs:31`). Resolved once per item, not per-page — there's no stanza-equivalent sub-object for Scripture, so this is simpler than Song's per-stanza override layer.

### Regeneration triggers on Design change too

Previously a theme change only needed re-rendering existing slides in place (font/background swap, same slide count). Now, since font size/family directly determines how many slides the passage needs, a `Design` change must re-run the full layout+pagination pass — same as a `Book`/`StartChapter`/etc. change already does. `ScriptureItemInstance`'s existing `WhenAnyValue(...)` subscription (currently used only for the `ItemDataModified` dirty-flag) gets `Design` added to its watched properties and calls `GenerateSlidesAsync()` again (same fire-and-forget-with-fault-logging convention already established at the `ItemInstanceFactory`/`MainViewModel` call sites) whenever it fires.

### Persistence

- `HandsLiftedDocXmlSerializer.SerializeItem`'s `ScriptureItemInstance` branch gains `Design = scriptureItemInstance.Design`.
- `ItemInstanceFactory.ToItemInstance`'s `ScriptureItem` branch gains `Design = scriptureItem.Design` on the constructed instance.
- Both follow the exact pattern already used for the other 6 copied fields at each site.

### Minimal UI

`ItemEditDockRoot.axaml` gets a new `DataTemplate x:Key="ScriptureItemInstance"`, reusing the exact same "Theme" button + `Flyout` + `ComboBox` (bound to `ParentPlaylist.Designs`/`SelectedItem="{Binding ResolvedDesignTheme}"`) already used for `SongItemInstance` (`ItemEditDockRoot.axaml:16-42`). This is the only new UI this design adds — it also happens to close part of an earlier-flagged gap (Scripture's Edit button being a dead affordance) with a small, targeted fix rather than building the full Phase 4b editor.

## Reference Header

- Text: book title + chapter:verse range (e.g. `"1 Peter 1:13-25"`) — same format the existing per-verse label logic already produces for a range, just emitted once instead of per-slide.
- Styling: theme `FontSize × 1.3`, bold weight, same `FontFamily`/`TextAlignment`/color as body text — no separate configurable header style.
- Placement: top of the content area on slide 1 only (fixed, not part of the reflow pass) — the layout engine treats it as consuming fixed height off the top of page 0's usable height before running the normal line-wrapping pass for that page. Continuation slides (page 1+) have no header, using the full content height.

## Superscript Verse Numbers

- Every verse shows its number, including the first verse of the passage — matching typical Bible-app/printed-Bible convention, and avoiding a special case that would misbehave if reflow ever shifted where the first verse lands relative to the header.
- Rendered as a superscript run: `0.6×` body `FontSize`, baseline raised `0.35×` line height, immediately preceding the verse's text with no space (e.g. `¹³Be sober-minded...`).
- **Chapter-change marker:** if the passage spans more than one chapter, the first verse of a new chapter shows `"{chapter}:{verse}"` instead of a bare verse number (e.g. a passage running 1:25–2:3 shows `¹Follow peace...` for 2:1) — reuses data already available per-verse (`ScriptureVerseRef.Chapter`), no new extraction work.

## Layout / Pagination Algorithm

A single greedy pass over the whole passage produces pages directly — no separate word-wrap-then-paginate steps.

New component: **`ScriptureParagraphLayoutEngine`** (new, alongside the render builders in `HandsLiftedApp.Core/Render/Skia/Builders/`). Input: the flat verse list (from the existing, unchanged `ScriptureVerseRangeExtractor.Extract`), the resolved theme's font settings, and slide canvas dimensions. Output: `List<Page>`, each a `List<Line>`, each a `List<Run>` (text, font size, baseline-offset).

1. **Tokenize:** for each verse, emit its (possibly chapter-prefixed) superscript marker glued to the verse's first word as one atomic wrap-unit — so a verse number can never end up orphaned alone at the end of a line with its text pushed to the next line — then the verse's remaining words as normal wrap-units, each separated by a space. Verse boundaries are just token positions; mid-verse splits need no special handling since a token boundary is a token boundary regardless of which verse it belongs to. Allowing mid-verse breaks (a verse's text can continue on the next slide) was an explicit choice over always moving a whole verse to the next slide.
2. **Line-fill:** walk tokens, measuring each candidate addition via `SKPaint.MeasureText` (reusing the existing word-wrap primitive already used by `ScriptureSlideSpecBuilder`/`SongSlideSpecBuilder`) against `maxWidth` (`CanvasWidth - 2×HorizontalMargin`, same constant as today). When a token would overflow the line, finalize the current line and start a new one.
3. **Page-fill:** line height = `theme.LineHeightEm × theme.FontSize` (the body size — a superscript-containing line uses the same line height as any other line, since the superscript is smaller/raised, not dominant). When a new line would overflow the remaining page height, finalize the current page and start a new one. Only page 0's available height is reduced by the header's height + spacing; every later page uses the full content area.
4. **Safety:** same pathological-input handling as the existing word-wrap code — a single token wider than `maxWidth` still gets placed alone on its own line rather than looping forever.

## Rendering Changes

- **New `RenderElement` subtype**, e.g. `MultiRunTextLineElement(IReadOnlyList<TextRun> Runs, SKRect Bounds, DropShadowSpec? Shadow)` where `TextRun(string Text, float FontSize, SKColor Color, float BaselineOffsetY)` — today's `TextLineElement` (`HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs:25-32`) hard-codes exactly one font size/color per whole line and can't represent superscript-plus-body-text on one baseline. Added alongside the existing type — `TextLineElement`, `SongSlideSpecBuilder`, and Song's rendering path are not touched at all.
- **`SlideRenderer`** gains a draw path for the new element type (multiple `canvas.DrawText` calls per line, one per run, at the run's own font size/baseline offset) alongside its existing `DrawTextElement` for `TextLineElement`.
- **`ScriptureParagraphSpecBuilder`** (new, replaces `ScriptureSlideSpecBuilder`) — turns one page (from the layout engine) into a `SlideRenderSpec`: builds background from the resolved theme (unchanged logic, reused as-is), adds the header element (page 0 only), and turns that page's lines into `MultiRunTextLineElement`s.

## Data Model & Slide Identity Changes

- **`ScriptureItemInstance.UpdateVerseSlides`'s per-verse loop is removed**, replaced by: run the layout engine over the extracted verse list → resolved theme → get pages → diff pages against existing `Slides` by **page index** (`slideId = "page{N}"`) rather than the old `"{chapter}:{verse}"` scheme. Same index reuses the existing slide instance (preserving `Cached`/`Thumbnail` unless that page's content actually changed); extra trailing slides from a previous longer pagination are removed; new trailing slides are added. This replaces the old chapter:verse identity scheme, which assumed a 1:1 verse-to-slide mapping that no longer holds.
- **`ScriptureSlideInstance`**'s `Text`/`Label` model changes to hold a page's laid-out lines/runs (from the layout engine) rather than a single plain string — exact shape decided during planning, but conceptually it now stores "this page's content" (header-or-not, list of lines of runs) instead of "one verse's text."

## Testing

- **`ScriptureParagraphLayoutEngine`** (the centerpiece to test): pure function, deterministic given real `SKPaint.MeasureText` numbers, verifiable without pixel rendering. Covers: single-page passages, multi-page reflow, mid-verse splits landing correctly, verse-marker-never-orphaned-from-its-first-word, chapter-boundary markers getting the `chapter:verse` prefix, and the pathological too-wide-single-token case.
- **`ScriptureParagraphSpecBuilder`**: no automated test — consistent with this codebase's established precedent for `SlideRenderSpec` builders (visual/graphics code, verified by manual/visual check, not unit tests).
- **Design/persistence wiring**: a round-trip test (serialize → deserialize a `ScriptureItem` with a non-default `Design`, confirm it survives) — same pattern already used for the other 6 fields' round-trip test.

## Migration

None needed. `ScriptureItem`'s persisted fields (book/chapter/verse range, now plus `Design`) are backward-compatible additions; existing saved items just render differently (paragraph mode) the next time they're loaded and regenerated, same as any other rendering-logic change.
