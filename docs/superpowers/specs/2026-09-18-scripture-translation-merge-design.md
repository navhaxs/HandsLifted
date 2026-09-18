# Scripture Libraries / Scripture Data Merge — Design

**Status:** Approved by user, ready for implementation planning.
**Date:** 2026-09-18

## Problem

Two unrelated "scripture" concepts currently exist side by side, confusingly:

1. **Scripture Data** (`AppPreferences.ScriptureDataPath`) — one global local folder holding
   downloaded USX Bible text (`ScriptureUsxDownloader`, hardcoded to a single translation,
   `eng_bsb` / Berean Standard Bible). This is what scripture slides actually render from
   (`ScriptureItemInstance.GenerateSlidesAsync`).
2. **Scripture Libraries** (`LibraryType.Scripture` in `library.yml`, `ScriptureLibrary.cs`) — a
   library-pane entry that lists `.xml` files in a folder as browsable items. Never actually
   created by any default config, not wired to anything else, and — since a real scripture data
   folder holds `.usx` files, not `.xml` — would show zero items even pointed at real data.

`ScriptureItem.Translation` already exists as a free-text field on the data model and is stamped
onto every new item (`MainViewModel.cs:574`, hardcoded to `ScriptureUsxDownloader.FixedTranslation`),
but nothing ever reads it back — `GenerateSlidesAsync` always loads from the single global
`ScriptureDataPath` regardless of what `Translation` says. There is also no UI anywhere to pick a
translation when adding scripture to a playlist.

## Goal

Let the user configure multiple named scripture translations (e.g. BSB, KJV, WEB — any
public-domain translation [fetch.bible](https://fetch.bible) offers, or a manually-supplied
folder for a translation obtained elsewhere), each downloaded/stored independently, and pick
which one to pull from when adding scripture to a playlist.

Non-goals:
- No support for commercial translations (NIV, ESV, etc.) via download — fetch.bible's catalog is
  public-domain/CC-licensed content only; a user who owns such a translation elsewhere can still
  point a Directory at their own USX files manually.
- No automatic migration of an existing single `ScriptureDataPath` value — clean cutover, users
  re-add/re-download via the new UI.
- Browsing individual scripture book files in the sidebar Library pane is explicitly **not**
  supported (see Decisions) — scripture is added by reference (book/chapter/verse), not by
  clicking a file.

## Decisions (from brainstorming)

- **Scripture entries stop being sidebar-browsable libraries.** `ScriptureLibrary.cs` (the
  `Library` subclass that lists files) is deleted, along with its test file
  (`ScriptureLibraryTests.cs`). `LibraryViewModel.RebuildLibraries()` no longer constructs a
  `Library`/`ScriptureLibrary` for `Type == Scripture` entries — they never enter the sidebar
  `Libraries` collection at all. They remain in `LibraryConfig.LibraryItems` purely as
  configuration, consumed only by the scripture-add flow and rendering.
- **`LibraryConfig.LibraryDefinition` gains one field**: `TranslationCode` (e.g. `"eng_kjv"`),
  meaningful only when `Type == Scripture`. `Label` becomes the translation's display name (e.g.
  "KJV"), `Directory` its local USX folder — reusing exactly the same two fields Media/Song
  libraries already use, no schema restructuring. Old `library.yml` files without this field
  deserialize fine (YamlDotNet leaves it at its default `null`).
- **`AppPreferences.ScriptureDataPath` is removed entirely.** Superseded by the Scripture
  Libraries list. No migration (per Goal/Non-goals above).
- **Translation catalog sourced live from `https://v1.fetch.bible/manifest.json`**, filtered to
  English (`eng_*` codes) entries whose `copyright.licenses` contains `"public"` — confirmed via
  the official docs (`fetch.bible/access/manual/`, `fetch.bible/legal/terms/`) that this is the
  documented catalog endpoint and that public-domain is the only license tier with zero
  attribution/redistribution obligations. ~21 qualifying translations today (KJV, WEB, ASV, BSB,
  BBE, DBY, GNV, YLT, MSB, NWB, OEB/OEBC, WMB/WMBB, RV, NOY, DRAE, JPS, LEE, TOE, WB, plus the
  `*B`/`*U` editions of a few of these).
- **Book fetch URL confirmed against live API**: `https://v1.fetch.bible/bibles/{code}/usx/{book}.usx`
  works identically for any translation code (tested against `eng_kjv`), matching the existing
  `eng_bsb`-only implementation exactly — only the hardcoded translation code needs to become a
  parameter.
- **No bulk/archive endpoint exists** (confirmed in docs) — downloading a translation stays one
  HTTP request per book, sequential, matching `ScriptureUsxDownloader.DownloadAllBooksAsync`'s
  existing (already polite, non-parallel) behavior.
- **Add Translation is a dedicated dialog**, not the generic blank-row "+ Add" the Media/Song
  sections use. It combines: translation dropdown (from the filtered manifest), a folder picker
  (default suggested path `Constants.APP_DATA_DIR/ScriptureData/{ABBREV}`), and a Download button
  reusing the existing progress-bar pattern from the current "Download Bible Data" button
  (`SetupWindow.axaml.cs:126-166`). On success it appends the row (`Label`, `Directory`,
  `TranslationCode`, `Type = Scripture`) through the same live-persist path the other two sections
  already use. The generic manual Label/Directory row editing (already built) stays available
  underneath, for BYO folders.
- **Resolution at render/add time is by `Label` match**: `ScriptureItemInstance.Translation`
  (already a free string field, unchanged) is matched against configured Scripture Library
  `Label`s. Falls back to the first configured Scripture Library if no match (e.g. stale data from
  before this change, or the matching library was since removed); the existing "not found"
  placeholder behavior (`MakeMissingDataPlaceholder`) covers the case where none are configured at
  all.

## Architecture

### Data model — `HandsLiftedApp.Core/Models/Library/Config/LibraryConfig.cs`

```csharp
public class LibraryDefinition : INotifyPropertyChanged
{
    // ... existing Label, Icon, Directory, Type ...

    private string? _translationCode;
    public string? TranslationCode { get => _translationCode; set => SetField(ref _translationCode, value); }
}
```

### Preferences — `AppPreferencesViewModel.cs:167-171`

Remove the `ScriptureDataPath` property (and its `[DataMember]`) entirely — the last of its 5
reference sites (`SetupWindow.axaml`/`.axaml.cs`, `ScriptureItemInstance.cs`,
`ScriptureAddDialog.axaml.cs`, and this declaration) are all covered by the sections below.

### Downloader — `HandsLiftedApp.Importer.Scripture/ScriptureUsxDownloader.cs`

- Remove `FixedTranslation` constant.
- `DownloadAllBooksAsync(string rootPath, ...)` gains a `string translationCode` parameter, used
  in `DownloadOneBookAsync`'s URL construction instead of the constant.
- New: a small manifest-fetching helper (new class, e.g. `ScriptureTranslationCatalog`) that
  fetches `https://v1.fetch.bible/manifest.json`, deserializes just the `bibles` map, and exposes
  the filtered (`eng_*`, `license == "public"`) list as `(string Code, string Name, string
  Abbrev)` entries for the picker.

### Setup UI — `SetupWindow.axaml` / `.axaml.cs`, `SetupWindowViewModel.cs`

- Remove the "Scripture Data" section (`SetupWindow.axaml:289-298` and the
  `DownloadScriptureDataButton_OnClick` handler / `ScriptureDownloadStatusText`) entirely.
- Scripture Libraries section's "+ Add Scripture Library" button becomes "+ Add Translation",
  opening a new small dialog (new View, e.g. `AddScriptureTranslationDialog`) instead of appending
  a blank row directly like Media/Song do.
- `SetupWindowViewModel.AddScriptureLibraryRow()` is replaced by a method that takes the dialog's
  result (translation code/name, chosen folder) and appends a fully-populated row, going through
  the existing `PersistLibraryRows()` path unchanged.

### Rendering — `ScriptureItemInstance.cs:165-167`

```csharp
var store = _injectedStore ?? new ScriptureLocalUsxStore(ResolveTranslationDirectory(Translation));
```

New private helper `ResolveTranslationDirectory(string translation)`: finds the Scripture Library
(`Globals.Instance.MainViewModel.LibraryViewModel.LibraryConfig.LibraryItems`) whose `Type ==
Scripture` and `Label == translation`; falls back to the first `Type == Scripture` entry if no
match; returns `""` if none configured (existing `ScriptureLocalUsxStore`/`LoadBookAsync` error
handling already produces the "not found" placeholder in that case).

### Add-scripture UI — `ScriptureAddDialog.axaml` / `.axaml.cs`

- New translation `ComboBox`, `ItemsSource` = configured Scripture Library `Label`s, defaulting to
  the first one. `_store` is constructed from the *selected* library's `Directory` instead of the
  removed global `ScriptureDataPath`.
- `Result` tuple gains a `Translation` (selected Label) field.
- `MainViewModel.cs:572-576` sets `Translation = result.Translation` instead of the current
  hardcoded `ScriptureUsxDownloader.FixedTranslation`.

## Testing

- New: `ScriptureTranslationCatalog` manifest-filtering logic (unit test against a fixture JSON
  snippet, not a live network call).
- New: `ResolveTranslationDirectory` — match found, no match (fallback to first), none configured.
- `ScriptureUsxDownloader`'s existing tests (if any) updated for the new `translationCode`
  parameter.
- No changes expected to `ScriptureItemInstanceTests.cs`, `PlaylistInstanceScriptureNavigationTests.cs`,
  `ScriptureItemTests.cs` — all construct `ScriptureItemInstance` via the `_injectedStore`
  constructor overload, which bypasses `ResolveTranslationDirectory` entirely.
  `HandsLiftedDocXmlSerializerTests.cs`'s three call sites use the plain constructor but never call
  `GenerateSlidesAsync`, so are unaffected too.
- Delete `ScriptureLibraryTests.cs` (tests the class being removed).

## Out of scope / future

- Per-book availability (`books_ot`/`books_nt` in the manifest) isn't used to skip known-missing
  books before requesting them — the existing per-book try/catch + failed-count already handles a
  404 gracefully; skipping them proactively would just save a few wasted requests for NT-only
  translations.
- A "refresh/update" action reusing the stored `TranslationCode` to re-download missing books
  later isn't built now, just left possible by persisting the code.
