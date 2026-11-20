# ThumbnailSizeSlider notches - short design

## Intent / why
After the grid-density change, `SlideThumbnailSizeMultiplier` is a *minimum* slot width, so column count (an integer) only changes at thresholds. Slider drag and +/- often do nothing visible, then jump. Make every slider position / button press visibly change the Playlist slides grid.

## Goals
- Every notch, drag step and +/- press in the main slides slider changes the column count.
- Stored value stays `SlideThumbnailSizeMultiplier` (width multiplier, 0-100 scale): no preference migration, `PlaylistSlidesView` anchor logic and the empty-state placeholder unchanged.
- Other `ThumbnailSizeSlider` users (`MediaLibraryQueryView`, `SongEditorControl`) behave exactly as today.

## Non-goals
- No change to the panel's layout rule or margins. No new preference.

## Approaches
A. Notched slider (chosen). B. Smart +/- only (drag keeps dead zones). C. Continuous, leftover to gaps (brings back wasted space).

## Design (A)
- `SlideGridMetrics` gains `static double[] ColumnNotches(double availableWidth, double itemWidth, int maxColumns)`: for c = maxColumns..1, notch multiplier = `((W + Spacing) / c - Spacing) / itemWidth * 100` (the min width at which exactly c columns fit); returned ascending. Pure, unit-tested.
- `ThumbnailSizeSlider` gets opt-in `AvailableWidth` (double, default 0 = off, old behaviour: range 50-150, +/-10). When > 0: slider Minimum/Maximum = first/last notch, `Ticks` = notches, `IsSnapToTickEnabled`; +/- move to the adjacent notch (nearest notch above/below current value); the value snaps to the nearest notch when notches change (window resize) only when the user next interacts - no write on resize.
- Main slides slider (`MainView.axaml:352`) binds `AvailableWidth` to the slides view width minus insets (ListBox padding 8 + panel margin 8 + scrollbar allowance 16, constants in `SlideGridMetrics`). Under-estimating W is safe (notch still yields c columns, slightly more stretch); over-estimating could give c-1.
- `maxColumns` = 8 (so smallest thumbnails ~ W/8); notches below the old 50 floor are allowed here (range follows the window).

## Calls I made
- maxColumns 8; scrollbar allowance 16px.
- Resize behaviour: no automatic rewrite of the stored value (avoids fighting the user / saved prefs changing on resize).

## Verify
Unit tests for `ColumnNotches` (ordering, exact-fit property with the panel formula, tiny/huge W, W<itemWidth). Manual: drag and +/- at several window widths always change the grid; other two sliders unchanged.
