# Design/Theme "Type" grouping — design spec

Date: 2026-09-18

## Problem

`BaseSlideTheme` ("Design") has no concept of what kind of slide it's meant for. `SlideThemeDesigner`'s list is a single flat `ListBox`, and both the song-item and scripture-item theme pickers bind to the same unfiltered `Playlist.Designs` collection — a scripture-only background theme shows up when picking a song theme and vice versa. There's also no scripture preview in the theme designer; only song lyric and song title previews exist.

## Goals

- Add a `Type` field to `BaseSlideTheme`: `General`, `SongTheme`, `ScriptureTheme`.
- Group the `SlideThemeDesigner` list by these three types.
- Song item / song editor theme pickers only offer `General` + `SongTheme` designs.
- Scripture item theme picker only offers `General` + `ScriptureTheme` designs.
- Add a scripture preview toggle to the theme designer, alongside the existing song lyric/title toggles.
- Existing saved designs (no `Type` in their XML) must continue working exactly as before — pickable everywhere.

## Non-goals

- No change to `BaseSlideTheme`'s existing properties, XML file format version, or the `avares://` path-mangling bug (tracked separately in CLAUDE.md).
- No restriction on *which* preview toggles are shown based on the selected design's `Type` — all three toggles (lyric/title/scripture) are always available regardless of the design's `Type`. `Type` only constrains which designs appear in the song/scripture *pickers*, not what can be previewed in the designer.
- No true native grouped-`ListBox` (`GroupStyle`/`ICollectionView`) — not used anywhere else in this codebase (Avalonia 11, no such precedent found), and `SlideThemeDesigner` has no ViewModel today. Three stacked, separately-filtered `ListBox` controls is the chosen mechanism (see "Listbox grouping" below).

## A. Data model

`HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs`:

```csharp
[Serializable]
public enum SlideThemeType
{
    General = 0,
    SongTheme,
    ScriptureTheme,
}
```

Add to `BaseSlideTheme`:

```csharp
[DataMember]
public SlideThemeType Type { get; set; } = SlideThemeType.General;
```

`General` is enum value `0`, so any existing serialized `BaseSlideTheme` XML with no `<Type>` element deserializes to `General` with no serializer changes and no migration step — matches current behavior (pickable from both song and scripture items).

`CopyFrom(BaseSlideTheme other)` already reflection-copies every property; no change needed. The standalone single-theme export/import path (`SlideThemeXmlSerializer`, using `XmlSerializer(typeof(BaseSlideTheme))`) also picks up the new property automatically.

## B. Filtering helper

New static class (e.g. `HandsLiftedApp.Data/Models/SlideTheme/SlideThemeTypeExtensions.cs`):

```csharp
public static class SlideThemeTypeExtensions
{
    public static bool AppliesToSongs(this BaseSlideTheme design) =>
        design.Type is SlideThemeType.General or SlideThemeType.SongTheme;

    public static bool AppliesToScripture(this BaseSlideTheme design) =>
        design.Type is SlideThemeType.General or SlideThemeType.ScriptureTheme;
}
```

Used by both the picker filtering (section D) and the `SlideThemeDesigner` grouping (section C) so the "which type applies where" rule lives in exactly one place.

## C. SlideThemeDesigner grouping

`SlideThemeDesigner.axaml` currently has a single `ListBox x:Name="designsListBox"` bound to `Playlist.Designs`. Replace with three sections, each: a header `TextBlock` ("General" / "Song Themes" / "Scripture Themes") followed by its own `ListBox`, each bound to a filtered view of `Playlist.Designs` by `Type`:

- `generalDesignsListBox` — `Type == General`
- `songDesignsListBox` — `Type == SongTheme`
- `scriptureDesignsListBox` — `Type == ScriptureTheme`

The existing item `DataTemplate` (name, default-badges) is shared across all three (`x:Key` reused as `ItemTemplate` on each `ListBox`).

**Selection coordination**: the file's existing logic (`SyncEditorToSelection`, context-menu handlers, `AddItem_OnClick`/`DuplicateItem_OnClick`/etc.) all currently read/write `designsListBox.SelectedItem`. These become a single `SelectedDesign` accessor in code-behind:

```csharp
private BaseSlideTheme? SelectedDesign =>
    generalDesignsListBox.SelectedItem as BaseSlideTheme
    ?? songDesignsListBox.SelectedItem as BaseSlideTheme
    ?? scriptureDesignsListBox.SelectedItem as BaseSlideTheme;
```

Each `ListBox`'s `SelectionChanged` clears the other two's `SelectedItem` (so only one design is ever selected across all three lists) and re-runs `SyncEditorToSelection()`.

**Adding a new design**: `AddItem_OnClick` currently does `Playlist.Designs.Add(new BaseSlideTheme())` and selects it in `designsListBox`. It needs to know which group to add into. Simplest: three "+" buttons, one per section header (reusing the same handler with a `SlideThemeType` parameter via `CommandParameter` or three thin wrapper handlers), each constructing `new BaseSlideTheme { Type = <that type> }` and selecting it in the matching `ListBox`. `DuplicateItem_OnClick` keeps the duplicated design's `Type` as-is (via `CopyFrom`) and selects it in the matching list based on its `Type`.

Filtered per-`ListBox` sources are backed by three `ObservableCollection<BaseSlideTheme>`-like filtered views kept in sync with `Playlist.Designs`' `CollectionChanged` (add/remove/reset) plus each design's own `Type` property changes (since `BaseSlideTheme` is `ReactiveObject`, subscribe to `.Changed`/`WhenAnyValue(d => d.Type)` per design to move it between the three lists live if the user edits `Type` in the editor panel — see section G).

## D. Song / scripture pickers

Two ComboBoxes today, both flat and unfiltered:

- `ItemEditDockRoot.axaml` song flyout: `ItemsSource="{Binding ParentPlaylist.Designs}"`
- `ItemEditDockRoot.axaml` scripture flyout: `ItemsSource="{Binding ParentPlaylist.Designs, Converter={StaticResource PrependBlankThemeOptionConverter}}"`
- `SongEditorControl.axaml` "Design" tab: `ItemsSource="{Binding Playlist.Designs}"`

Add a new converter, `FilterThemesByTypeConverter` (parallel to the existing `PrependBlankThemeOptionConverter`), parameterized by which filter to apply (song vs scripture), returning `designs.Where(d => d.AppliesToSongs())` or `.AppliesToScripture()`. Wire it into:

- Song flyout ComboBox: `Converter={StaticResource FilterThemesByTypeConverter}` with a `ConverterParameter` selecting the song filter.
- Scripture flyout ComboBox: chain both converters (filter then prepend-blank), or fold the prepend-blank behavior into a single combined converter call — whichever keeps the XAML simplest; implementer's call, functionally equivalent either way.
- `SongEditorViewModel.SelectedSlideTheme`'s backing candidate list (`Playlist.Designs`) filtered the same way in code (`.Where(d => d.AppliesToSongs())`), so the two song pickers behave identically.

## E. Scripture preview

New `HandsLiftedApp.Core/Views/Designer/ScriptureSlideView.axaml` + `.axaml.cs`, copying `SongSlideView`'s existing structure: hosts a `SlideCanvas`, exposes `SetSlide(ScriptureSlideInstance)`, subscribes to slide/theme changes, rebuilds via `ScriptureParagraphSpecBuilder.Build(slide)`.

`SlideThemeDesigner` gets a third preview-toggle radio button, "Scripture", alongside the existing `previewLyricToggle`/`previewTitleToggle`. `SlideThemeDesigner.axaml.cs` builds one fixed sample `ScriptureSlideInstance` (placeholder reference + verse text, analogous to the existing sample song slide/title instances) once, and `SyncEditorToSelection()`/theme-change handling applies the currently-selected design's theme to all three sample instances (or just the active one — implementer's call for efficiency, behavior is identical either way since only the active preview is visible).

Per the non-goals section, all three toggles are shown regardless of the selected design's `Type`.

## F. Backward compatibility

- Old playlist XML files: `Type` element absent → deserializes to `SlideThemeType.General` (enum default) → design keeps appearing in both song and scripture pickers, identical to pre-feature behavior. No explicit migration code needed.
- `Globals.Instance.AppPreferences.DefaultTheme` (the final fallback in `PlaylistInstance.ResolveSongTheme`/`ResolveScriptureTheme`) is a `BaseSlideTheme` — also defaults to `General`, so it remains valid as a fallback for both song and scripture resolution paths.

## G. Editing a design's Type

`SlideThemeDesigner`'s editor panel (the property-editing side, not explored in depth for this spec since it's the existing generic property editor) needs one new field: a `Type` picker (e.g. a 3-option `ComboBox`/`RadioButton` group) bound to the selected `BaseSlideTheme.Type`. Changing it live-moves the design between the three `ListBox` sections (per section C's reactive subscription).

## Testing

- Unit tests: `AppliesToSongs()` / `AppliesToScripture()` truth table (3 types × 2 methods = 6 cases). XML round-trip test: serialize a `BaseSlideTheme` XML fragment with no `<Type>` element, deserialize, assert `Type == General`.
- Manual in-app verification (per this repo's CLAUDE.md rule on UI changes needing a live click-through): add one design of each type, confirm they land in the correct `SlideThemeDesigner` group; confirm song item picker only offers General+SongTheme designs and scripture item picker only offers General+ScriptureTheme designs; confirm all three preview toggles work for a design of any `Type`; confirm an old playlist (pre-`Type`) loads with its designs appearing in the "General" group and still pickable everywhere.
