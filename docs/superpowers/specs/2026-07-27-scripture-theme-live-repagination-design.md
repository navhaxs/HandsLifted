# Scripture Live Theme-Edit Repagination Design

**Date:** 2026-07-27
**Status:** Approved, ready for planning
**Depends on:** the scripture paragraph rendering plan (merged on `feature/scripture-slide-type`) — `ScriptureParagraphLayoutEngine`, `ScriptureItemInstance.ResolvedDesignTheme`, `ScriptureSlideInstance`.

## Background

`ScriptureParagraphLayoutEngine.Paginate` computes both word-wrap and how many verses fit per slide as a function of the resolved theme's `FontSize`/`LineHeightEm` — unlike Song, where a slide always maps to one fixed stanza regardless of font size, Scripture's slide *count* is itself font-size-dependent.

Two ways a scripture item's theme can change:
1. **Switching `Design`** to a different theme object entirely — already handled: the previous plan's final-review fix wave made `ResolvedDesignTheme`'s setter call `GenerateSlidesAsync()`, and `UpdatePages` resets `Cached` when the theme *reference* changes.
2. **Editing an existing theme's properties in place** (e.g. dragging a font-size slider in the theme editor, which binds directly to the same `BaseSlideTheme` object every item pointing at that Design already shares) — **not currently handled**. `ScriptureSlideInstance`'s own `WhenAnyValue(x => x.Theme).Select(t => t?.WhenAnyPropertyChanged()...).Switch()` subscription only triggers a plain re-render (redraw the *existing* `Lines` at the *new* font size), never re-running `Paginate`. Result: text overflows/overlaps, or premature line breaks with mostly-empty pages, depending on whether the font grew or shrank.

## Goal

When any property of a scripture item's currently-resolved theme changes — whether via switching `Design` or editing the theme object in place — the item repaginates (debounced, so a slider drag doesn't repaginate on every tick) and its slides' cached thumbnails are invalidated so the new layout is visible.

## Design

**New subscription in `ScriptureItemInstance`'s constructor**, mirroring `ScriptureSlideInstance`'s existing theme-change pattern but wired to full repagination instead of a plain re-render:

```csharp
private readonly DebounceDispatcher _themeChangeDebounce = new(200);

this.WhenAnyValue(x => x.ResolvedDesignTheme)
    .Select(t => t?.WhenAnyPropertyChanged() ?? Observable.Never<BaseSlideTheme?>())
    .Switch()
    .Subscribe(_ => _themeChangeDebounce.Debounce(() =>
        _ = GenerateSlidesAsync(forceInvalidateCache: true).ContinueWith(
            t => Log.Error(t.Exception, "Failed to generate scripture slides for {Title}", Title),
            TaskContinuationOptions.OnlyOnFaulted)));
```

- `DebounceDispatcher` (200ms) — same class and duration already used by `ScriptureSlideInstance`'s own render-trigger and `SongItemInstance`'s stanza-update debounce.
- Fires on **any** theme property change (background/color included) — the repagination pass is cheap, pure in-memory text layout with no I/O, so filtering to only layout-affecting properties isn't worth the added property-name-list maintenance.
- `WhenAnyValue(x => x.ResolvedDesignTheme).Select(...).Switch()` re-subscribes to whichever theme object is *currently* resolved, so this naturally follows a `Design` switch too without duplicating logic against the setter's own existing trigger.
- No `ObserveOn(RxApp.MainThreadScheduler)` — matches this same constructor's existing documented rationale for the `ActiveSlide` chain (`GenerateSlidesAsync` already does its own UI-thread marshaling internally, in `UpdatePages`'s `Dispatcher.UIThread.Post` block).

**`GenerateSlidesAsync`/`UpdatePages` gain a `forceInvalidateCache` parameter**, default `false` so every existing call site (initial load, verse-range edits, `Design`-switch) is unaffected:

```csharp
public async Task GenerateSlidesAsync(bool forceInvalidateCache = false)
```

In `UpdatePages`'s reused-slide branch:

```csharp
if (existing != null)
{
    existing.Lines = page.Lines;
    if (existing.Text != flatText) existing.Text = flatText;
    if (existing.Label != referenceLabel) existing.Label = referenceLabel;
    bool themeChanged = !ReferenceEquals(existing.Theme, theme);
    if (themeChanged) existing.Theme = theme;
    if (themeChanged || forceInvalidateCache) existing.Cached = null;
    newSlides.Add(existing);
}
```

Forcing invalidation unconditionally when `forceInvalidateCache` is true (rather than relying on content-diffing) closes a rare edge case: a font-size delta that happens to not shift any line-wrap boundary would otherwise leave a stale-sized cached thumbnail even though the theme genuinely changed.

## Non-Goals

- No throttling/coalescing beyond the existing `DebounceDispatcher` pattern — no need for a more sophisticated scheduler.
- No property-name filtering of which theme changes trigger repagination (explicit non-goal per the design decision above).
- No changes to how `Design`-switching itself works — that path is already correct from the prior plan.

## Testing

A test constructing a `ScriptureItemInstance`, generating slides once, mutating `ResolvedDesignTheme.FontSize` directly (or calling `GenerateSlidesAsync(forceInvalidateCache: true)` directly to test the flag's effect without waiting on real debounce time), and asserting: (a) the slide count/content reflects the new font size, and (b) `Cached` was reset on any reused slide instance.
