# ItemSlidesView Grid Density Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Slide thumbnails in `ItemSlidesView` fill their grid slot edge to edge, with tighter gaps, while the thumbnail-size slider still works.

**Architecture:** `JustifiedWrapPanel` gets an opt-in aspect mode: columns are chosen from a minimum slot width (`ItemWidth`), column width is stretched to fill the row, and row height is derived from the final width (`(w - ExtraWidth) / AspectRatio + ExtraHeight`). Shared layout constants live in one static class used by XAML and the empty-state placeholder.

**Tech Stack:** Avalonia 11, C#, MSTest (`HandsLiftedApp.Tests`).

**Spec:** `docs/superpowers/specs/2026-11-20-item-slides-grid-density-design.md`

## Global Constraints

- Aspect mode is opt-in: `AspectRatio == 0` (default) must keep today's `JustifiedWrapPanel` behaviour exactly.
- Slider (`AppPreferences.SlideThumbnailSizeMultiplier`, 0-100 scale, `* 0.01` where used in code) now means *minimum* slot width = `MainViewModel.ItemWidth * multiplier`.
- Thumbnail is 16:9 (template hardcodes 1280x720).
- The `ListBoxItem` style in `HandsLiftedApp.Controls/ListBoxItem.axaml` is global (also used by `MediaLibraryQueryView`, `SongEditorControl`, `PlaylistSlidesView`): do NOT change its Margin; override Margin only inside `ItemSlidesView`.
- No `find`/`grep` shell commands; use Glob/Grep tools. No em dash in Serilog calls (n/a here).

## Review Focus

- Available width narrower than min `ItemWidth` -> 1 column, item width = `ItemWidth` (overflow like today), never negative/zero.
- `availableSize.Width` infinite (measured inside a horizontal scroller) -> no NaN/infinity heights.
- Zero children -> size (0,0), no divide-by-zero.
- `ExtraWidth >= itemWidth` (tiny slider) -> height never negative.
- Empty-state placeholder lines up with where the first real slide would sit (same left inset and similar size).

---

### Task 1: Aspect mode in `JustifiedWrapPanel`

**Files:**
- Modify: `HandsLiftedApp.Controls/JustifiedWrapPanel.cs`
- Create: `HandsLiftedApp.Tests/Controls/JustifiedWrapPanelTests.cs`

**Interfaces:**
- Produces: styled properties on `JustifiedWrapPanel`: `double AspectRatio` (default 0 = legacy mode), `double ExtraWidth` (default 0), `double ExtraHeight` (default 0); all `AffectsMeasure` + `AffectsArrange`. In aspect mode `ItemHeight` is ignored.

- [ ] **Step 1: Write failing tests** in `JustifiedWrapPanelTests` (MSTest, plain `[TestMethod]`, children are `Border`s; call `panel.Measure(new Size(w, 10000)); panel.Arrange(new Rect(0,0,w,10000))` and assert on `child.Bounds`). Panel config for all: `ItemWidth=100, Spacing=10, AspectRatio=2, ExtraWidth=0, ExtraHeight=0` unless stated.
  - `AspectMode_StretchesWidthAndDerivesHeight`: 7 children, width 350 -> 3 columns, item width `(350-20)/3 = 110`, height `55`; child[3].Bounds is `(0, 55+10, 110, 55)`.
  - `AspectMode_HeightIncludesExtras`: ExtraWidth=10, ExtraHeight=20, width 350 -> height `(110-10)/2 + 20 = 70`.
  - `AspectMode_NarrowerThanItemWidth_UsesOneColumnAtItemWidth`: width 60 -> child width 100.
  - `AspectMode_SingleColumnFillsWidthWhenWiderThanItemWidth`: width 150 -> 1 column, child width 150, height 75.
  - `AspectMode_MeasureReturnsTotalDerivedHeight`: 7 children, width 350 -> `DesiredSize.Height == 3*55 + 2*10`.
  - `AspectMode_InfiniteWidth_NoNaN`: Measure with `double.PositiveInfinity` width -> finite DesiredSize.
  - `AspectMode_ExtraWidthLargerThanItem_HeightNotNegative`.
  - `LegacyMode_Unchanged`: AspectRatio 0, ItemHeight 40, width 350 -> child height 40, width 110.
- [ ] **Step 2: Run** `dotnet test HandsLiftedApp.Tests --filter JustifiedWrapPanelTests` -> expect compile failure (properties missing).
- [ ] **Step 3: Implement.** Add the three properties. Add private `double SlotWidthFor(double availableWidth)` (`columns>1 || aspect mode ? max(ItemWidth, (w-(cols-1)*Spacing)/cols)`; legacy unchanged) and private `double SlotHeightFor(double slotWidth)` (`AspectRatio > 0 ? max(0, (slotWidth - ExtraWidth)/AspectRatio + ExtraHeight) : ItemHeight`). Use them in both `MeasureOverride` (child constraint, total height; infinite width -> slot width = `ItemWidth`) and `ArrangeOverride` (per-row y advance uses slot height). Legacy path must produce identical numbers to current code.
- [ ] **Step 4: Run tests** -> all pass.
- [ ] **Step 5: Commit** `feat: JustifiedWrapPanel aspect mode (row height derived from stretched width)`.

### Task 2: Wire `ItemSlidesView` + empty-state placeholder

**Files:**
- Create: `HandsLiftedApp.Core/Views/SlideGridMetrics.cs`
- Modify: `HandsLiftedApp.Core/Views/ItemSlidesView.axaml` (ItemsPanel at ~lines 367-396; ListBox `Styles` at ~357-366)
- Modify: `HandsLiftedApp.Core/Views/ItemSlidesView.axaml.cs` (`UpdateEmptyState`, ~lines 633-684)

**Interfaces:**
- Consumes: Task 1 properties.
- Produces: `public static class SlideGridMetrics` in namespace `HandsLiftedApp.Core.Views` with `public const double ItemMargin = 2`, `LabelStripHeight = 24`, `Spacing = 4`, `PanelMarginX = 4`, `Aspect = 16.0/9.0`, `ExtraWidth => 2*ItemMargin`, `ExtraHeight => LabelStripHeight + 2*ItemMargin` (use `const`/static readonly so `x:Static` works).

- [ ] **Step 1: Create `SlideGridMetrics`** as above (`LabelStripHeight`: label row = 14px text + 2px margin top/bottom of the template's bottom `DockPanel`; tune in Step 5).
- [ ] **Step 2: XAML.** On the `JustifiedWrapPanel`: `AspectRatio`/`ExtraWidth`/`ExtraHeight`/`Spacing` from `SlideGridMetrics` via `x:Static`, `Margin="{x:Static ...PanelMargin}"` -> use `Margin="4,0"` literal if a `Thickness` static is awkward; remove the `ItemHeight` MultiBinding; keep the `ItemWidth` MultiBinding (min slot width, slider driven). In the ListBox `Styles`, extend the existing `ListBoxItem` style with `<Setter Property="Margin" Value="2" />` (literal; keep equal to `SlideGridMetrics.ItemMargin`, add a comment saying so).
- [ ] **Step 3: Placeholder.** In `UpdateEmptyState`, size the placeholder to the min slot: `Width = ItemWidth*mult - 2*ItemMargin`, `Height = Width/Aspect + LabelStripHeight`, `Margin = new Thickness(PanelMarginX + ItemMargin, ItemMargin, 0, 0)` so it sits where the first real slide sits.
- [ ] **Step 4: Build** `dotnet build HandsLiftedApp.Core` and run the full `dotnet test HandsLiftedApp.Tests` -> expect no new failures.
- [ ] **Step 5: Manual verify in the running app** (devtools unavailable; use the app): several window widths and the slider min/max show thumbnails filling slots with no right-hand strip; selected/hover borders and label strip not clipped (if clipped or gap under thumbnail, adjust `LabelStripHeight`); empty item placeholder aligns with the left edge/top of where a slide would sit; `PlaylistSlidesView`/media library lists look unchanged. Report what you could not verify.
- [ ] **Step 6: Commit** `feat: ItemSlidesView thumbnails fill slots, tighter margins`.
