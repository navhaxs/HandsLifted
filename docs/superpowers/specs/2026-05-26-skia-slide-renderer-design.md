# Skia Slide Renderer — Design Spec
_Date: 2026-05-26_

## Problem Statement

The current rendering pipeline rasterises slide content by calling `RenderTargetBitmap.Render()` on Avalonia UI controls (`SongSlideView` etc.). This causes two confirmed defects:

1. **Drop shadows not captured.** `DropShadowDirectionEffect` is a post-process Avalonia effect that is skipped during `RenderTargetBitmap` rendering. Shadows are absent from both the projector output and NDI capture.
2. **Cross-fade dip-out.** When transitioning between two song lyric slides with identical lines, the entire slide bitmap (including unchanged text) animates through alpha. At mid-transition, unchanged text drops below full opacity and the motion background bleeds through. Users perceive a noticeable flicker on lines that should not have moved.

## Goals

- Fix drop shadow rendering in all outputs (projector window, NDI).
- Fix cross-fade so that unchanged text lines remain at full opacity throughout a transition; only added/removed lines animate.
- Establish a single rendering path shared by the editor and all outputs, eliminating the WYSIWYG fidelity gap.
- MVP scope only: position animation for lines that shift Y position is explicitly out of scope.

## Architecture Overview

A new `SlideCanvas` custom Avalonia control owns all slide rendering via direct Skia draw calls. Callers pass it a `SlideRenderSpec` (pure data, no Avalonia or Skia references). The control handles drawing and transition animation internally.

```
Slide Model  ──(translator)──▶  SlideRenderSpec
                                      │
                              ┌───────▼────────┐
                              │   SlideCanvas  │  ← custom Avalonia control
                              │   (Skia draw)  │    owns transition state
                              └───────┬────────┘
                   ┌─────────────────┼─────────────────┐
                   ▼                 ▼                  ▼
           ProjectorWindow       Editor view        Thumbnail
           (replaces             (+ OverlayPanel    generation
            ActiveSlideRender)    on top)            (static helper)
```

**What changes:**
- `ActiveSlideRender` + `XTransitioningContentControl` — removed; replaced by `SlideCanvas`
- `SongSlideView` as projector/NDI render target — replaced by `SlideCanvas` Skia drawing
- `SlideRendererWorkerWindow` (off-screen Avalonia bitmap renderer) — removed; thumbnails generated via `SlideCanvas.RenderToSKBitmap()`

**What is unchanged:**
- `MotionBackgroundLayer` (libmpv) — still sits below `SlideCanvas` in Z-order
- `NDISendContainer` — still wraps projector content and captures via `RenderTargetBitmap`
- `VideoLayerRenderer` — unchanged
- Slide data model (`SongSlide`, `SongTitleSlide`, `BaseSlideTheme`, etc.)

## Component Designs

### 1. SlideRenderSpec — render data model

Pure data records; no Avalonia or Skia dependencies. Lives in `HandsLiftedApp.Core/Render/Skia/`.

```csharp
record SlideRenderSpec(
    BackgroundSpec Background,
    IReadOnlyList<RenderElement> Elements
);

abstract record BackgroundSpec;
record TransparentBackground() : BackgroundSpec;
record SolidBackground(SKColor Color) : BackgroundSpec;
record ImageBackground(string FilePath) : BackgroundSpec;

abstract record RenderElement(SKRect Bounds);

record TextLineElement(
    string Text,
    SKRect Bounds,
    SKTypeface Typeface,
    float FontSize,
    SKColor Color,
    DropShadowSpec? Shadow
) : RenderElement(Bounds);

record DropShadowSpec(float OffsetX, float OffsetY, float BlurRadius, SKColor Color);
```

**Identity key for cross-fade diffing:** `TextLineElement.Text`. Elements with matching text between old and new specs are considered unchanged and are never animated.

### 2. Spec translators

One static builder per slide type, living in `HandsLiftedApp.Core/Render/Skia/Builders/`.

- `SongSlideSpecBuilder.Build(SongSlide, BaseSlideTheme) → SlideRenderSpec`
- `SongTitleSlideSpecBuilder.Build(SongTitleSlide, BaseSlideTheme) → SlideRenderSpec`

Builders read text content and theme properties (font family, size, weight, colour, shadow settings). Element bounds are computed using `SKFont.MeasureText` so that measurement and drawing always agree.

### 3. SlideCanvas — custom Avalonia control

Lives in `HandsLiftedApp.Core/Render/Skia/SlideCanvas.cs`.

```csharp
class SlideCanvas : Control
{
    SlideRenderSpec? _current;
    SlideRenderSpec? _previous;
    float _transitionProgress; // 0.0 → 1.0
    DispatcherTimer? _timer;

    // Set spec immediately with no animation (used in editor bindings)
    public SlideRenderSpec? Spec { get; set; }

    // Set spec with cross-fade animation (used in projector code-behind)
    public void Transition(SlideRenderSpec next, TimeSpan duration);

    protected override void Render(DrawingContext ctx);
}
```

Core draw logic is extracted into a static `SlideRenderer` class with a single `Draw(SKCanvas, SlideRenderSpec, float alpha)` method. Both `SlideCanvas.Render()` and the thumbnail helper call `SlideRenderer` — no duplication of drawing code.

```csharp
// Thumbnail generation — no UI thread required, no Avalonia control needed
static class SlideRenderer
{
    public static void Draw(SKCanvas canvas, SlideRenderSpec spec, float alpha = 1f);
    public static SKBitmap RenderToSKBitmap(SlideRenderSpec spec, int width, int height);
}
```

**Drawing order per frame:**

1. Draw `Background` (solid colour / image / nothing for transparent).
2. Compute per-element alpha from `_transitionProgress`:
   - Text matches in both old and new → alpha = 1.0
   - Element only in old (being removed) → alpha = `1 - progress`
   - Element only in new (being added) → alpha = `progress`
3. For each element, configure `SKPaint`:
   - `SKPaint.Color` with computed alpha
   - `SKPaint.ImageFilter = SKImageFilter.CreateDropShadow(...)` if shadow specified (inherits same alpha)
4. Draw text via `SKCanvas.DrawText()`.

**Animation driver:**

`Transition(next, duration)` stores `_previous = _current`, sets `_current = next` and `_progress = 0`, then starts a `DispatcherTimer` at 16ms intervals. Each tick increments progress by `dt / duration`, calls `InvalidateVisual()`, and stops when progress ≥ 1.0. On stop: `_previous = null`.

The control owns its animation state entirely. Callers only call `Transition()`.

**Why drop shadows are now captured by RenderTargetBitmap:**
`SKImageFilter.CreateDropShadow()` is a Skia draw-time operation, not an Avalonia post-process effect. It executes inside `Render(DrawingContext ctx)` and is therefore included in both on-screen rendering and `RenderTargetBitmap` capture.

### 4. Editor integration

```xml
<Grid>
    <views:SlideCanvas Spec="{Binding RenderSpec}" />
    <Canvas x:Name="OverlayPanel">
        <ResizeHandle ... />   <!-- shown when element selected -->
        <TextBox ... />        <!-- activated on double-click -->
    </Canvas>
</Grid>
```

- The editor view model holds `SlideRenderSpec` as a bindable property.
- Edits update the spec and rebind directly — no transition animation triggered.
- The `TextBox` overlay is positioned to match `TextLineElement.Bounds` from the spec, so it aligns with what Skia drew.
- Selection handles (`ResizeHandle`) use the same `Bounds` values.
- `SlideCanvas.Render()` is the same code path as the projector: zero fidelity gap.

### 5. Projector & NDI integration

`ProjectorWindow.axaml` change is minimal:

```xml
<!-- Before -->
<render:ActiveSlideRender ActiveSlide="{Binding Playlist.ActiveSlide}" />

<!-- After -->
<views:SlideCanvas x:Name="SlideCanvas" />
```

`MotionBackgroundLayer` and `VideoLayerRenderer` are unchanged above and below in Z-order.

The code-behind observes `Playlist.ActiveSlide`, translates to `SlideRenderSpec` via the appropriate builder, and calls `SlideCanvas.Transition(spec, TimeSpan.FromMilliseconds(120))`.

`NDISendContainer` is unchanged. Because `SlideCanvas` emits Skia draw calls inside `Render()`, `RenderTargetBitmap.Render(SlideCanvas)` captures everything — including shadows — faithfully.

## Out of Scope (MVP)

- Position animation for text lines that shift Y when line count changes between slides.
- Element types beyond `TextLineElement` (images, shapes) — `SlideRenderSpec` is designed to accommodate them but builders are not required for MVP.
- `AltSlideRenderer` (lyrics-only NDI output) — can be migrated in a follow-up.

## Files Affected

| Action | Path |
|---|---|
| Add | `HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs` |
| Add | `HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs` |
| Add | `HandsLiftedApp.Core/Render/Skia/SlideCanvas.cs` |
| Add | `HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs` |
| Add | `HandsLiftedApp.Core/Render/Skia/Builders/SongTitleSlideSpecBuilder.cs` |
| Modify | `HandsLiftedApp.Core/Views/ProjectorWindow.axaml(.cs)` |
| Modify | `HandsLiftedApp.Core/Views/Designer/SongSlideView.axaml(.cs)` |
| Remove | `HandsLiftedApp.Core/Render/ActiveSlideRender.axaml(.cs)` |
| Remove | `HandsLiftedApp.Core/Views/SlideRendererWorkerWindow.axaml.cs` |
| Remove | `HandsLiftedApp.XTransitioningContentControl/` (project) |
