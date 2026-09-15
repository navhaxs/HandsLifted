# Media Library "Home" Browser — Design

**Status:** Approved by user, ready for implementation planning.
**Date:** 2026-09-15

## Problem

The Media Library folder introduced in the prior change (`AppPreferencesViewModel.MediaLibraryPath`,
commit `1648768`) has no way to browse it from the app — media only gets referenced against it
implicitly when adding/importing. The user wants to see and pick files from it directly, the same
way they browse Song/Scripture libraries today via the Library pane.

Separately, the existing "Media Bin" library type (`LibraryType.Media` in `library.yml`, rendered by
`LibraryQueryView`'s `IsMediaBin` branch) only ever shows a flat, top-directory-only grid — no
subfolders — even though nothing stops a user from organizing a Media Bin folder into subfolders on
disk today.

## Goal

1. Add a selectable "Home" entry to the Library pane's library list, backed by
   `AppPreferences.MediaLibraryPath`, shown only when that path is configured.
2. Give every `LibraryType.Media` library (Home included) a folder-drill-down browser: breadcrumb
   navigation, folder and file tiles in the same thumbnail grid, current-folder-only search, and the
   same drag / right-click "Add to playlist" behavior Media Bin already has.
3. Retire the old flat (non-recursive-only, no navigation) Media Bin view, since the new browser
   fully supersedes it — it is the browser for `LibraryType.Media`, not an alternative to it.

Read-only: no folder create/rename/delete from this view.

## Decisions (from brainstorming)

- **Home is not a special case — it's a synthetic `LibraryType.Media` library.** `LibraryViewModel`
  appends one extra `Library` (`Label="Home"`, `Config.Directory=MediaLibraryPath`) to the same
  `Libraries` collection populated from `library.yml`, only when `MediaLibraryPath` is non-empty. No
  placeholder/disabled entry when unconfigured — it's simply absent.
- **One hierarchical browser for all Media libraries**, not one for Home and a separate flat one for
  everything else. This means the existing flat Media Bin code path (`IsMediaBin` in
  `LibraryQueryView.axaml` / `LibraryQueryViewModel.cs`, `Library.isMediaBin`) becomes dead once
  routing changes, and gets removed rather than left unreachable.
- **New, separate ViewModel/View pair** (`MediaLibraryQueryViewModel` / `MediaLibraryQueryView`),
  not a third mode bolted onto `LibraryQueryViewModel`. That VM already juggles two modes
  (flat Media Bin vs. Song list+preview); hierarchical navigation state (breadcrumbs, current
  subfolder) has no business living alongside Song-library search/preview state. Song/Scripture
  libraries keep using `LibraryQueryViewModel` exactly as today — untouched.
- **Non-recursive, per-folder, on-demand scan** — mirrors `Library.Refresh()`'s existing
  top-directory-only pattern (`Directory.GetDirectories`/`GetFiles`), just re-run against
  `RootPath/CurrentRelativePath` on navigation instead of once at load. No upfront full-tree walk.
- **Search scoped to the current folder only** — typing filters what's already shown, not a
  recursive search across the whole library. Consistent with how a folder browser normally behaves.
- **New standalone model `HomeLibraryEntry { FullPath, Title, IsDirectory }`**, not an extension of
  the existing `LibraryItem` (`Library.cs`). `LibraryItem` is used by Song/Scripture flows
  (`SongPreview`, drag payloads, etc.) that have no folder concept — bolting `IsDirectory` onto it
  would leak folder-awareness into code that should never see one.
- **File selection behavior**: same as today's Media Bin — click selects/previews, drag or
  right-click → "Add to playlist" adds it. Since the file is already under the Media Library, adding
  it goes through the existing `PortableAssetCopier.ResolveOrCopyIntoMediaLibrary` path added in the
  prior change, which references it in place (no copy).

## Architecture

### Routing: `LibraryViewModel.cs`

The single `SelectedLibrary` subscription (`LibraryViewModel.cs:164-174`, currently the only call
site of `new LibraryQueryViewModel(...)`) branches on `SelectedLibrary.Config.Type`:

```csharp
this.WhenAnyValue(t => t.SelectedLibrary)
    .Subscribe(x =>
    {
        if (x == null) return;
        ActiveQuery = x.Config.Type == LibraryType.Media
            ? new MediaLibraryQueryViewModel(x)
            : new LibraryQueryViewModel(new List<Library> { x });
    });
```

`ReloadLibraries()` (`LibraryViewModel.cs:68-135`) additionally appends the synthetic Home library
after building the `library.yml`-sourced list, when `Globals.Instance.AppPreferences.MediaLibraryPath`
is non-empty:

```csharp
if (!string.IsNullOrWhiteSpace(Globals.Instance.AppPreferences?.MediaLibraryPath))
{
    libraries.Add(new Library(new LibraryConfig.LibraryDefinition
    {
        Label = "Home",
        Type = LibraryType.Media,
        Directory = Globals.Instance.AppPreferences.MediaLibraryPath,
        Icon = "Home" // whatever icon key this codebase's Library icon lookup expects
    }));
}
```

(Exact construction call shaped to match `Library`'s actual constructor/`LibraryConfig` fields —
confirm during planning.)

### `LibraryPaneView.axaml` — one more `DataTemplate`

The `ContentControl` at `LibraryPaneView.axaml:86-93` already picks a view by the `ActiveQuery`
VM's runtime type via `ContentControl.DataTemplates`. Add a second entry:

```xml
<ContentControl.DataTemplates>
    <DataTemplate DataType="viewModels:LibraryQueryViewModel">
        <library:LibraryQueryView />
    </DataTemplate>
    <DataTemplate DataType="viewModels:MediaLibraryQueryViewModel">
        <library:MediaLibraryQueryView />
    </DataTemplate>
</ContentControl.DataTemplates>
```

### `MediaLibraryQueryViewModel` (new, `HandsLiftedApp.Core/ViewModels/`)

- `RootPath` — `library.Config.Directory` (absolute).
- `CurrentRelativePath` — `""` at root; updated on navigation.
- `Breadcrumbs` — `ObservableCollection<string>` of path segments from root to current, each with a
  navigate-to-here command; always starts with the library's own `Label`.
- `Entries` — `ObservableCollection<HomeLibraryEntry>`, folders first (alphabetical) then files
  (alphabetical), rescanned on every navigation.
- `SearchTerm` — filters `Entries` in place (case-insensitive `Title.Contains`), current folder only.
- `NavigateInto(HomeLibraryEntry folder)` / `NavigateToBreadcrumb(int index)` — update
  `CurrentRelativePath` and rescan.
- Add-to-playlist plumbing for a file entry reuses whatever `LibraryQueryView`'s Media Bin branch
  already sends today (the `AddItemByFilePathMessage` / drag payload) — confirm exact call during
  planning so behavior is byte-for-byte identical to current Media Bin add.

### `HomeLibraryEntry` (new, small model)

```csharp
public class HomeLibraryEntry
{
    public string FullPath { get; init; }
    public string Title { get; init; } // Path.GetFileName(FullPath)
    public bool IsDirectory { get; init; }
}
```

### `MediaLibraryQueryView.axaml` (new)

- Top: `TextBox` bound to `SearchTerm` (reuse the `Filter`-watermark styling from
  `LibraryQueryView.axaml:21-26`) + breadcrumb bar (`ItemsControl` over `Breadcrumbs`, each item a
  clickable `TextBlock`/`Button` separated by `/` or `>`).
  Alternatively, breadcrumbs are shown below the filter box.
- Below: the `ListBox`/`WrapPanel` thumbnail grid, essentially the cell markup lifted from
  `LibraryQueryView.axaml:30-107` (`ItemHeight=120 ItemWidth=200`, `asyncImageLoader:ImageLoader`
  thumbnail, loading/no-preview states), with:
  - A folder-tile variant when `IsDirectory == true` (folder `MaterialIcon`, no
    `asyncImageLoader` binding, click navigates in via `NavigateInto`).
  - The existing file-tile markup unchanged for `IsDirectory == false`, including the
    `DockPanel_PointerPressed` drag-start and "Add to playlist" context menu item.

### Cleanup: retire the old flat Media Bin path

Once routing sends every `LibraryType.Media` library through `MediaLibraryQueryViewModel`, these
become dead:

- `LibraryQueryView.axaml:30-107` (the `IsMediaBin`-gated `ListBox`) and its `IsVisible` bindings at
  lines 32 and 110.
- `LibraryQueryViewModel.cs`'s `IsMediaBin` property (lines 22-25, 86-87, 118-122) and whatever
  computes it.
- `Library.isMediaBin` (`Library.cs:33-39`) and the inference logic at `Library.cs:104-105`, *if*
  nothing else reads it — `SongLibrary.cs:31` and `ScriptureLibrary.cs:16` only ever set it `false`,
  so removing the property should just mean deleting those two assignment lines too. Confirm no
  other reader exists before deleting (a fresh grep at plan/implementation time, since this design
  was written before the routing change existed).

## Error handling / edge cases

- Folder scan wrapped in try/catch mirroring `Library.Refresh()`'s existing robustness — a
  permission error or a folder deleted out from under a live navigation just yields an empty
  `Entries` list; the breadcrumb still lets the user navigate back up.
- Symlinks/junctions: not specially handled, out of scope.
- Home entry is simply absent from the library list when `MediaLibraryPath` is unset — no error
  state to design for.

## Testing

- `MediaLibraryQueryViewModel`'s scan/breadcrumb/search logic is plain C#, testable against a real
  temp directory tree (create nested folders + files, assert `Entries`/`Breadcrumbs` after
  `NavigateInto`/`NavigateToBreadcrumb`/`SearchTerm` changes) — no Avalonia dependency needed for
  that part, following the pattern already used by `PortableAssetCopierTests.cs` etc.
- The actual view (breadcrumb bar, grid, drag/drop, "Add to playlist") needs a manual click-through
  in a running app — no Avalonia UI test harness exists in this repo.
