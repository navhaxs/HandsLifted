# ItemSlidesView grid density - short design

## Intent / why
Maximise thumbnail size in `ItemSlidesView` by removing wasted empty space between and beside slide thumbnails. Keep the `ThumbnailSizeSlider` (`SlideThumbnailSizeMultiplier`).

## Problem
- `JustifiedWrapPanel` stretches column width to fill the row, but row height stays `ItemHeight * multiplier`. Thumbnail is a 16:9 `Viewbox` (left-aligned) so it is height-limited: extra width becomes a dead strip right of each thumbnail.
- Slot is 290x200 (1.45:1) vs 16:9 thumbnail: dead height even at default.
- Stacked margins: `ListBoxItem` Margin `4 8` + panel Spacing 8 (16px horiz / 24px vert gap), panel Margin `8,0`, ListBox Padding 4.

## Goals
- Thumbnails fill their slot edge to edge (no right-hand dead strip, no dead height).
- Tighter gaps and outer margins.
- Slider still works.

## Non-goals
- No change to slide rendering, label strip content, selection/hover borders, or non-16:9 slide support (template already hardcodes 1280x720).
- No new UI (no column-count control).

## Approaches considered
- A. Derive row height from final column width (chosen).
- B. Margins only - leaves dead strip.
- C. Slider = column count - loses fine-grained slider.

## Design (A)
- Slider value = **minimum** thumbnail slot width (`ItemWidth * multiplier`).
- `JustifiedWrapPanel`: columns = fit at min width; column width stretched to fill row; item height computed per width: `(w - ExtraWidth) * 9/16 + ExtraHeight`. New properties: `AspectRatio` (16/9), `ExtraWidth`, `ExtraHeight` (label strip + margins). Panel `ItemHeight` becomes unused for ItemSlidesView (kept/ignored when aspect mode enabled).
- Trim: `ListBoxItem` Margin to ~2, panel Spacing to ~4, reduce panel Margin / ListBox Padding.
- Empty-state placeholder (`ItemSlidesView.axaml.cs` `UpdateEmptyState`) must use the same size math.

## Calls I made (builder can adjust)
- Exact trimmed margin/spacing values (2 / 4); tune visually.
- `ExtraHeight` for the label strip measured from actual layout, not guessed.
- Other users of `JustifiedWrapPanel` / `ListBoxItem` style (e.g. `PlaylistSlidesView`) must be checked; aspect mode is opt-in so they are unaffected.

## Verify
Run app; check several window widths, slider min/max, empty item, selected/hover border, multi-row alignment.
