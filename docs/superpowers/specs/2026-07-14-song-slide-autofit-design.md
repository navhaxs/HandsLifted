# Song Lyric Slide Autofit — Design

Date: 2026-07-14

## Problem

Song lyric slides (`SongSlideInstance`, rendered by `SongSlideSpecBuilder`) use a
fixed `theme.FontSize`. Long stanzas word-wrap mid-line, producing ugly line
breaks instead of shrinking to fit. Song title slides
(`SongTitleSlideInstance` / `SongTitleSlideSpecBuilder`) are unaffected by this
problem and are explicitly out of scope.

## Goal

Add an opt-out "Autofit" theme setting that, when enabled, picks the largest
font size for a given stanza such that:

1. Every raw lyric line fits on one line at the slide's text width (no
   word-wrap needed), and
2. The whole stanza block fits within the canvas height (with margin).

If no size at or above a configured floor satisfies both conditions, fall back
to today's word-wrap behavior at the floor size — text is never clipped or
silently dropped.

## Scope

- In scope: `HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs`,
  `HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs`,
  `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml` (+ `.cs`).
- Out of scope: `SongTitleSlideSpecBuilder.cs` (untouched), `CustomSlideSpecBuilder.cs`
  (element-based slides, untouched), the parallel/legacy
  `HandsLiftedApp.Models/Models/SlideTheme/BaseSlideTheme.cs` copy (not
  referenced by `HandsLiftedApp.Core`, so irrelevant to the render pipeline
  this feature touches).

## Design

### 1. Theme fields (`BaseSlideTheme.cs`)

Two new `[DataMember]` properties, following the existing plain
`RaiseAndSetIfChanged` pattern used by `FontSize` / `LineHeightEm` (no
clamping in the setter, consistent with current fields):

```csharp
private bool _autofitEnabled = true;

[DataMember]
public bool AutofitEnabled
{
    get => _autofitEnabled;
    set => this.RaiseAndSetIfChanged(ref _autofitEnabled, value);
}

private decimal _autofitMinFontSizeRatio = 0.5M;

[DataMember]
public decimal AutofitMinFontSizeRatio
{
    get => _autofitMinFontSizeRatio;
    set => this.RaiseAndSetIfChanged(ref _autofitMinFontSizeRatio, value);
}
```

Default `AutofitEnabled = true` so existing themes (no serialized value falls
back to the field default) get the fixed behavior automatically after
update, matching the screenshot expectation out of the box.
`AutofitMinFontSizeRatio` is the floor as a fraction of `theme.FontSize`
(default 0.5 = never shrink below half the configured size).

### 2. `SongSlideSpecBuilder.BuildTextElements`

Before the existing word-wrap loop, compute the effective font size:

```csharp
float effectiveFontSize = theme.FontSize;
float effectiveLineHeight = theme.LineHeight;

if (theme.AutofitEnabled)
{
    (effectiveFontSize, effectiveLineHeight) =
        ComputeAutofitSize(rawLines, theme, maxWidth, CanvasHeight - 2 * VerticalMargin);
}
```

`ComputeAutofitSize`:
- Starts at `theme.FontSize`, steps down by a fixed decrement (e.g. 4pt) each
  iteration.
- At each candidate size, measures every raw line's width (no wrapping) and
  the total block height (`rawLines.Count * candidateSize * (float)theme.LineHeightEm`).
- Stops and returns the first candidate where all raw lines fit `maxWidth`
  AND total height fits the available vertical space.
- Floors at `theme.FontSize * (float)theme.AutofitMinFontSizeRatio`; if even
  the floor doesn't fit, returns the floor size anyway — the existing
  word-wrap logic downstream still runs unconditionally and will wrap any
  line that's still too wide, so nothing is clipped or lost.

A new `VerticalMargin` constant (mirroring `HorizontalMargin = 80f`) bounds
the height check — today's code has no vertical margin and can already
overflow `CanvasHeight` for tall stanzas; this fixes that as a side effect.

The rest of `BuildTextElements` (wrap loop, line positioning, `TextLineElement`
construction) is unchanged except it now uses `effectiveFontSize` /
`effectiveLineHeight` instead of `theme.FontSize` / `theme.LineHeight`
directly.

`SongTitleSlideSpecBuilder` is not touched — title slides keep the fixed
`theme.FontSize` behavior unconditionally.

### 3. Designer UI (`SlideThemeDesigner.axaml`)

Add a checkbox bound to `AutofitEnabled` near the existing `FontSize` control
(line ~152), and a slider bound to `AutofitMinFontSizeRatio` (range 0.1–1.0),
following the existing `LineHeightEm` slider pattern (line ~306).

## Error handling

- No theme / empty slide text: unchanged early-return in `Build()`.
- Autofit finds no fitting size at/above the floor: falls back to floor size
  + existing word-wrap (safety net, no exception, no invisible text).
- `AutofitEnabled = false`: behavior is byte-for-byte identical to today's
  fixed-size path.

## Testing (manual)

Using `SlideThemeDesigner` + live preview:
1. Short stanza (2 lines) — renders at full theme size, no shrink.
2. Long stanza (per screenshot, 4 lines with long lines) — visibly shrinks,
   no mid-line wraps.
3. Extreme single long line — shrinks to floor, then wraps (doesn't overflow
   or clip).
4. Song title slide with same theme — unaffected, still fixed size.
5. Toggle `AutofitEnabled` off — output matches pre-change rendering exactly.
