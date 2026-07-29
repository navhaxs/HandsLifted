# Per-Item Fade Transition Override — Design

## Problem

Slide cross-fade duration is currently a single playlist-wide value
(`Playlist.SlideTransitionDurationMs`, see
`docs/superpowers/plans/2026-05-28-slide-transition-duration.md`), set via a
slider in `LivePane`. There is no way to make one item (e.g. a video-heavy
song, or a scripture item where a slower fade reads better) use a different
fade duration than the rest of the playlist.

## Goal

Add an optional per-item override for the fade duration. When set, it wins
over the playlist default for every slide belonging to that item. When unset
(null), the item inherits the playlist default exactly as today.

Scope is the **item** (the unit in the playlist list — song, scripture
passage, media group, etc.), not the individual slide. All slides within one
item share the same effective duration.

## Data Model

Add a nullable property to the shared serializable base class every item type
inherits:

**`HandsLiftedApp.Data/Models/Items/Item.cs`**

```csharp
private double? _slideTransitionDurationMs;
[DataField]
public double? SlideTransitionDurationMs
{
    get => _slideTransitionDurationMs;
    set => this.RaiseAndSetIfChanged(ref _slideTransitionDurationMs, value);
}
```

- `[DataField]` matches the existing pattern on `Title` — it drives the
  dirty/unsaved-changes indicator via `ItemInstanceProxy`.
- No `[XmlIgnore]` — this must round-trip through the playlist XML file.
  `XmlSerializer` handles `Nullable<double>` natively (omits the element when
  null, no converter needed) — same reasoning as the existing
  `Playlist.SlideTransitionDurationMs` (non-nullable) property.
- Runtime item instances (`SongItemInstance`, `ScriptureItemInstance`, etc.)
  all inherit from their corresponding data-model type (`SongItem`,
  `ScriptureItem`, ...) which inherits `Item`, so the property is available
  on every runtime instance automatically — no changes needed to the
  `*Instance` classes themselves.

## Serialization — explicit per-type mapping

Item (de)serialization in this codebase is **not** reflection-based like
`Slide`. Both directions manually rebuild a fresh object per concrete type:

- `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs` → `SerializeItem(Item item, ...)`
- `HandsLiftedApp.Core/ItemInstanceFactory.cs` → `ToItemInstance(Item deserializedItem, ...)`

Each is a chain of `if (item is XItem) return new XItem { ... };` branches.
Missing a branch means the override silently reverts to null (inherit) on
the next save/load cycle — no compiler error, no exception.

`SlideTransitionDurationMs = item.SlideTransitionDurationMs` (or the
deserialized-side equivalent) must be added to **every** explicit branch in
both methods:

**`SerializeItem`** (8 branches): `LogoItem`, `SongItem`, `ScriptureItem`,
`SlidesGroupItem`, `MediaGroupItem`, `PowerPointPresentationItem`,
`GoogleSlidesGroupItem`, `PDFSlidesGroupItem`.

**`ToItemInstance`** (7 branches): `LogoItem`, `SongItem`, `ScriptureItem`,
`PowerPointPresentationItem`, `PDFSlidesGroupItem`, `GoogleSlidesGroupItem`,
`MediaGroupItem`.

Types that fall through to the trailing `else return item;` /
`return deserializedItem;` fallback (`CommentItem`, `SectionHeadingItem`,
`BlankItem`) need **no** change — the fallback returns the same object
reference, so the property is already intact.

Before considering serialization done, grep both files for
`SlideTransitionDurationMs` after editing and confirm the count matches the
branch counts above — cheap insurance against the exact failure mode this
section describes.

## Resolution at render time

Three call sites currently duplicate the same fallback expression
(`_vm?.Playlist.SlideTransitionDurationMs ?? 120`):

- `HandsLiftedApp.Core/Views/LivePane.axaml.cs` (`OnActiveSlideChanged`)
- `HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs` (`OnActiveSlideChanged`)
- `HandsLiftedApp.Core/Views/StageDisplayLayout/DefaultLayout.axaml.cs`

Adding item-level resolution as a fourth ad-hoc `??` at each site would
triple the new logic and risk the same "missed a call site" failure mode the
project has already hit once with spec-builders (see CLAUDE.md). Instead,
add a single resolution helper on `PlaylistInstance`:

```csharp
// PlaylistInstance.cs
public double GetEffectiveTransitionDurationMs(Item? item) =>
    item?.SlideTransitionDurationMs ?? SlideTransitionDurationMs;
```

Each call site changes from:

```csharp
TimeSpan.FromMilliseconds(_vm?.Playlist.SlideTransitionDurationMs ?? 120)
```

to:

```csharp
TimeSpan.FromMilliseconds(_vm?.Playlist.GetEffectiveTransitionDurationMs(_vm.Playlist.SelectedItem) ?? 120)
```

`Playlist.SelectedItem` is the item that owns the currently active slide —
confirmed via `PlaylistInstance.ActiveSlide`, which is sourced from
`SelectedItemAsIItemInstance.ActiveSlide`. This holds for grouped items too
(`MediaGroupItem` and its `PowerPoint`/`PDF`/`GoogleSlides` subclasses) —
the override applies uniformly to every slide the group produces, which
matches the per-item (not per-slide) scope of this feature.

The three video cross-fade branches (`MotionBackgroundService.CrossFadeIn/OutDuration`)
in `LivePane`/`ProjectorWindow`/`DefaultLayout` are unaffected — those already
use a different, motion-background-specific duration and are out of scope.

## UI

`HandsLiftedApp.Core/Views/ItemSlidesView.axaml` has one shared `Fallback`
`DataTemplate` used for every item type except `CommentItem`,
`SectionHeadingItem`, and `BlankItem` (those have their own dedicated
templates and don't need this control). Because the view uses
`x:CompileBindings="False"`, a binding to the base `Item.SlideTransitionDurationMs`
property works uniformly regardless of the concrete item type shown — one
edit point covers every affected item type.

Add an icon button next to the existing `EditButton`
(`ItemSlidesView.axaml`, `Fallback` template, `x:Name="EditButton"` row):

- Icon reflects override state: filled/highlighted when
  `SlideTransitionDurationMs` is not null, outline when null (inheriting).
- Click opens a `Flyout` containing:
  - A toggle: "Override fade duration" — checked ⇔ property is non-null.
    Turning it on seeds the value from the current effective duration
    (`Playlist.GetEffectiveTransitionDurationMs(item)`, i.e. the playlist
    default) so the slider starts somewhere sensible rather than at 0.
    Turning it off sets the property back to `null`.
  - A `Slider` (`Minimum="0"`, `Maximum="2000"`, `TickFrequency="100"`,
    `SmallChange="50"`, `LargeChange="200"`) bound to
    `SlideTransitionDurationMs`, matching the existing global slider in
    `LivePane.axaml`. Disabled while the toggle is off.
  - A `ms` readout, same `StringFormat='{}{0:0}ms'` pattern as the LivePane
    slider.

No new editor windows are touched — this is the only UI change needed.

## Out of scope

- Per-slide (finer than per-item) overrides.
- The legacy `Slide.PageTransition` (`IPageTransition`) property — dead code
  from the pre-SkiaSharp `XTransitioningContentControl` pipeline, unrelated
  to the current `SlideCanvas.Transition(spec, TimeSpan)` duration-only API.
- Motion background cross-fade duration (`MotionBackgroundService.CrossFadeIn/OutDuration`).

## Testing / Manual Verification

- Set an item-level override, save, close and reopen the playlist — confirm
  the override value round-trips for at least one item of each of the 8
  serialized types (or at minimum: Song, Scripture, and one media-group
  variant, as representative coverage of the branch pattern).
- Navigate onto and off of the overridden item — confirm the fade duration
  visibly differs from the playlist default.
- Toggle the override off — confirm the item reverts to the playlist
  default duration (not 0, not stuck at the last override value).
- Confirm all three windows (MainWindow/LivePane, ProjectorWindow, Stage
  Display) apply the same effective duration for a given active item.
