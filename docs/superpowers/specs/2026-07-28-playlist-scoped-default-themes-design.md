# Playlist-scoped default themes — design

## Problem

Today there is a single app-wide default theme (`Globals.Instance.AppPreferences.DefaultTheme`), consulted whenever a song or scripture item has no explicit per-item `Design` override. Every slide type shares this one default regardless of content type or whether the song has a motion background set.

## Goal

Three independent default-theme slots, **stored per playlist** (not app-wide):

1. Default theme for songs **without** a motion background set
2. Default theme for songs **with** a motion background set
3. Default theme for scripture slides

Per-item explicit `Design` overrides (already existing on `SongItem`/`ScriptureItem`) continue to take priority over any default.

## Data model

Add three nullable `Guid` fields, alongside the existing `Designs` collection:

- `HandsLiftedApp.Data.Models.Playlist` (serialized doc model)
- `HandsLiftedApp.Core.Models.PlaylistInstance` (runtime model)

```csharp
public Guid? DefaultSongThemeId { get; set; }        // songs, no motion bg
public Guid? DefaultSongMotionThemeId { get; set; }   // songs, motion bg set
public Guid? DefaultScriptureThemeId { get; set; }    // scripture
```

`XmlSerializer` already round-trips `Guid`/`Guid?` without custom handling (see `SongItem.Design`). Wire the three fields through `HandsLiftedDocXmlSerializer` save/load the same way `Designs` and `LogoGraphicFile` are handled today.

## Resolution logic

New methods on `PlaylistInstance`:

```csharp
public BaseSlideTheme ResolveSongTheme(Guid explicitDesignId, bool hasMotionBackground)
{
    if (explicitDesignId != Guid.Empty)
    {
        var explicitTheme = Designs.FirstOrDefault(d => d.Id == explicitDesignId);
        if (explicitTheme != null) return explicitTheme;
    }
    var defaultId = hasMotionBackground ? DefaultSongMotionThemeId : DefaultSongThemeId;
    var byDefault = defaultId.HasValue ? Designs.FirstOrDefault(d => d.Id == defaultId) : null;
    return byDefault ?? Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
}

public BaseSlideTheme ResolveScriptureTheme(Guid explicitDesignId)
{
    if (explicitDesignId != Guid.Empty)
    {
        var explicitTheme = Designs.FirstOrDefault(d => d.Id == explicitDesignId);
        if (explicitTheme != null) return explicitTheme;
    }
    var byDefault = DefaultScriptureThemeId.HasValue
        ? Designs.FirstOrDefault(d => d.Id == DefaultScriptureThemeId)
        : null;
    return byDefault ?? Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
}
```

Fallback chain: explicit per-item `Design` → playlist category default → legacy app-wide `AppPreferences.DefaultTheme` → `new BaseSlideTheme()`. The legacy app default is retained permanently as the final fallback (not just a one-time seed), per explicit decision.

### Call sites to replace

All four existing `?? Globals.Instance.AppPreferences?.DefaultTheme` fallbacks route through the new resolver methods instead:

- `SongSlideInstance.ResolveTheme` (`HandsLiftedApp.Core/Models/RuntimeData/Slides/SongSlideInstance.cs`)
- `SongTitleSlideInstance.ResolveTheme` (`HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs`)
- `ScriptureItemInstance.ResolvedDesignTheme` getter (`HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`)
- `ScriptureSlideInstance` constructor (`HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs`)

## Reactive re-render

Two gaps beyond today's existing `Design`-change subscriptions:

1. **Motion background toggling.** Song theme resolution now depends on `HasMotionBackground`, not just `Design`. `SongSlideInstance` and `SongTitleSlideInstance` must re-run theme resolution (not just re-render) whenever `parentSongItem.MotionBackgroundVideoPath` changes, since crossing the no-motion/motion boundary can flip which default slot applies. (`SongTitleSlideInstance` already subscribes to this path for re-render only; extend it to also recompute `Theme`. `SongSlideInstance` needs the subscription added.)
2. **Playlist default changed.** When the playlist's `DefaultSongThemeId`, `DefaultSongMotionThemeId`, or `DefaultScriptureThemeId` changes (user repoints a default in the designer), every slide/item currently using that slot (i.e. explicit `Design == Guid.Empty`) must recompute `Theme`. Add an observable on `PlaylistInstance` for these three fields; `SongSlideInstance`, `SongTitleSlideInstance`, and `ScriptureItemInstance` subscribe and re-resolve when their own explicit `Design` is empty.

## UI

Extend the existing `SlideThemeDesigner` theme list (`HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml(.cs)`), which already renders a single "default" indicator via `IsDefaultThemeConverter`:

- Add a right-click context menu per theme row with three actions: **"Set as default for Songs"**, **"Set as default for Songs (motion bg)"**, **"Set as default for Scripture"**.
- Row indicator becomes three independent badges/icons (a theme can be the default for more than one slot at once), replacing the single star.
- `RemoveItem_OnClick`'s existing "can't remove the default theme" guard extends to check all three playlist slots in addition to the legacy `AppPreferences.DefaultTheme.Id` check already there.

## Back-compat / migration

Old playlist files load with all three new fields `null`. Resolver falls through to the legacy `AppPreferences.DefaultTheme`, i.e. byte-for-byte identical behavior to today until the user explicitly sets a playlist-level default. No migration step required.

## Out of scope

- No separate theme slot for a song's title slide vs. its lyric slides — both share the same two song slots (no-motion / motion), matching the single `Design` override per song today.
- No new UI surfaced for the legacy `AppPreferences.DefaultTheme` beyond what exists.
- No per-item motion-background-aware override UI beyond the existing single `Design` picker — the motion/no-motion split only affects which *default* applies when `Design` is unset.

## Testing

- Unit tests for `PlaylistInstance.ResolveSongTheme` / `ResolveScriptureTheme` covering: explicit override present, explicit override missing/stale (deleted theme), category default present, category default unset, playlist default deleted from `Designs` (falls to app default), full fallback to `new BaseSlideTheme()`.
- Existing `ScriptureItemInstanceTests.ResolvedDesignTheme_DesignEmpty_FallsBackToDefaultTheme` needs updating for the new fallback chain (playlist scripture default now sits between `Design` and legacy app default).
- Manual click-through in designer: set each of the 3 defaults via context menu, confirm live slides re-theme without explicit `Design` set, confirm toggling a song's motion background live-swaps between the two song defaults.
