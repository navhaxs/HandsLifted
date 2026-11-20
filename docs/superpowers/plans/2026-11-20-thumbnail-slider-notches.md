# ThumbnailSizeSlider Notches Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** In the main slides view, every slider notch, drag step and +/- press changes the grid's column count.

**Architecture:** A pure `SlideGridMetrics.ColumnNotches` function turns the slides-area width into one multiplier per column count. `ThumbnailSizeSlider` gets an opt-in `AvailableWidth`; when set it uses those notches as its range, ticks and +/- steps. `MainView` binds it to the slides view width.

**Tech Stack:** Avalonia (`Slider.Ticks` + `IsSnapToTickEnabled`), C#, MSTest.

**Spec:** `docs/superpowers/specs/2026-11-20-thumbnail-slider-notches-design.md`

## Global Constraints

- Stored value stays `AppPreferences.SlideThumbnailSizeMultiplier` (width multiplier, 0-100 scale). No preference migration.
- `AvailableWidth` default 0 = old behaviour exactly (Minimum 50, Maximum 150, +/-10): `MediaLibraryQueryView` and `SongEditorControl` sliders must be unchanged.
- No automatic rewrite of the stored value on window resize.
- Notch for c columns = `((W + Spacing) / c - Spacing) / ItemWidth * 100`; `maxColumns` = 8; `Spacing = SlideGridMetrics.Spacing` (4).
- Available width = slides view width minus insets: ListBox padding 8 + panel margin 8 + scrollbar allowance 16 (constants in `SlideGridMetrics`). Underestimating W is safe, overestimating is not.
- Use Glob/Grep tools, never shell find/grep.

## Review Focus

- W smaller than one `ItemWidth` -> still at least one notch (1 column), no empty/NaN list.
- Window resize leaves the stored value between notches -> +/- still move to the next notch above/below (never stuck, never skip).
- Value already at the first/last notch -> -/+ do nothing, no exception.
- `AvailableWidth` unset or 0 -> legacy 50-150 behaviour and +/-10 steps.
- Notches ascending, distinct, and each notch, fed to the panel's `ColumnsFor` formula, yields exactly c columns.

---

### Task 1: `SlideGridMetrics.ColumnNotches` + stepping helpers

**Files:**
- Modify: `HandsLiftedApp.Core/Views/SlideGridMetrics.cs`
- Create: `HandsLiftedApp.Tests/Views/SlideGridMetricsNotchTests.cs`

**Interfaces:**
- Produces (all `public static` on `SlideGridMetrics`):
  - `const double PanelInsetWidth = 8 + 8 + 16;` (ListBox padding + panel margin + scrollbar allowance)
  - `double[] ColumnNotches(double availableWidth, double itemWidth, int maxColumns = 8)` - ascending notch multipliers (0-100 scale), one per column count 1..maxColumns, using `availableWidth` as the panel width (callers subtract `PanelInsetWidth` first); never empty (W < itemWidth gives a single notch for 1 column, clamped to a minimum of 10).
  - `double NextNotch(double[] notches, double value, int direction)` - direction +1 returns the smallest notch strictly greater than `value` (+0.001 tolerance), -1 the largest strictly smaller; returns the nearest end notch if none; `value` itself if `notches` is empty.
  - `double NearestNotch(double[] notches, double value)`.

- [ ] **Step 1: Write failing tests** (MSTest, `[TestMethod]`):
  - `ColumnNotches_FeedingEachNotchToPanelFormulaGivesExactColumns`: W=900, itemWidth=290; for each notch n for c columns, `(int)((W + Spacing) / (itemWidth*n/100 + Spacing)) == c`.
  - `ColumnNotches_AreAscendingAndDistinct`.
  - `ColumnNotches_NarrowWidth_StillHasOneNotch`: W=100 -> length 1, value >= 10.
  - `ColumnNotches_UsesMaxColumns`: W=3000, maxColumns=8 -> at most 8 notches.
  - `NextNotch_StepsPastValueBetweenNotches`: notches {50,75,100}, value 80: +1 -> 100, -1 -> 75.
  - `NextNotch_AtEndsStays`: value 100 +1 -> 100; value 50 -1 -> 50.
  - `NextNotch_EmptyReturnsValue`; `NearestNotch_PicksClosest`.
- [ ] **Step 2: Run** `dotnet test HandsLiftedApp.Tests --filter SlideGridMetricsNotchTests` -> compile failure.
- [ ] **Step 3: Implement** the members above in `SlideGridMetrics.cs`. Drop notches that fall below the minimum clamp 10 except keep at least one; remove duplicates after clamping.
- [ ] **Step 4: Run tests** -> pass.
- [ ] **Step 5: Commit** `feat: SlideGridMetrics column notches and stepping helpers`.

### Task 2: Notched mode in `ThumbnailSizeSlider` + MainView binding

**Files:**
- Modify: `HandsLiftedApp.Core/Controls/ThumbnailSizeSlider.axaml.cs`, `HandsLiftedApp.Core/Controls/ThumbnailSizeSlider.axaml`
- Modify: `HandsLiftedApp.Core/Views/MainView.axaml` (slider at ~line 352; `PlaylistSlidesView` at ~line 442)

**Interfaces:**
- Consumes: Task 1 `ColumnNotches`, `NextNotch`, `NearestNotch`, `PanelInsetWidth`.
- Produces: `StyledProperty<double> AvailableWidth` on `ThumbnailSizeSlider` (default 0).

- [ ] **Step 1: Slider behaviour.** Add `AvailableWidth`. Name the inner `Slider` already exists (`ThumbnailSlider`). When `AvailableWidth > 0`: compute `notches = SlideGridMetrics.ColumnNotches(AvailableWidth - PanelInsetWidth, Globals.Instance.MainViewModel.ItemWidth)`; set `ThumbnailSlider.Minimum/Maximum` to first/last notch, `Ticks` to the notches, `IsSnapToTickEnabled` true; recompute whenever `AvailableWidth` changes. Zoom buttons call `NextNotch(notches, Value, ±1)`. When `AvailableWidth <= 0`: restore the current XAML values (Min 50, Max 150, TickFrequency 0.05, `Ticks` cleared) and the existing +/-10 code path unchanged. Do not write `Value` when notches recompute, except Avalonia's own clamp to the new range, which is acceptable.
- [ ] **Step 2: XAML.** In `MainView.axaml` give `PlaylistSlidesView` `x:Name="SlidesView"` and bind the main slider `AvailableWidth="{Binding #SlidesView.Bounds.Width}"`. Other two sliders untouched.
- [ ] **Step 3: Build and test** `dotnet build HandsLiftedApp.Core`, then full `dotnet test HandsLiftedApp.Tests`; report pre-existing failures separately.
- [ ] **Step 4: Human visual checks to report as pending** (do NOT launch the GUI): drag and +/- at several window widths always change the grid; value between notches after a resize still steps correctly; media library and song editor sliders unchanged.
- [ ] **Step 5: Commit** `feat: notched thumbnail slider for main slides view`.
