# Global Slide Themes - Design

## Goal
Slide themes ("designs") become app-wide instead of playlist-specific. Each theme is stored as its own XML file in a user-configurable folder. Playlists with embedded themes migrate them into the app-level store on open, without overwriting existing themes that share an Id.

## Confirmed decisions
- One XML file per theme in a configurable folder (new app preference + Set Up UI).
- Migration on playlist open: copy embedded themes to the library; never overwrite when the Id already exists.
- Default song / song-motion / scripture theme Ids: app-level, with optional per-playlist override.
- Theme assets (background images, motion videos) are copied into the Media Library; theme XML stores paths relative to the Media Library.

## Assumptions
- `Playlist.Designs` is no longer written on save; it is still read on load, for migration only.
- Built-in `AppPreferences.DefaultTheme` remains the last-resort fallback (not a file).

## 1. Storage: `SlideThemeLibrary`
- Singleton on `Globals.Instance`. Holds `ObservableCollection<BaseSlideTheme>` and an Id -> file path index. Modeled on `SongLibraryIndex`.
- Folder: new `AppPreferences.SlideThemesPath`, default `%APPDATA%\HandsLifted\SlideThemes`; created if missing.
- File per theme: `<sanitized name>.xml`, written via `SlideThemeXmlSerializer`. Name collisions get a numeric suffix. Identity is the Id inside the XML.
- Rename: delete old file, write new one.
- Saves debounced 500ms per theme; flushed on shutdown (`Globals.OnShutdown`).
- Load: scan `*.xml`. Corrupt files logged and skipped. Duplicate Id across files: first wins, warning logged.
- Library exposes `AppPreferences.DefaultTheme` as first entry (as `Designs` does today).
- Changing the path in Set Up reloads from the new folder; existing files are not moved.

## 2. Consumers
- Replace `Playlist.Designs` as a binding source with the library collection: Designer lists (`SlideThemeDesigner`), `ItemEditDockRoot` pickers, `SongEditorControl`, `SongLyricBlockEditor`, `SongEditorViewModel`.
- `PlaylistInstance.ResolveSongTheme` / `ResolveScriptureTheme` and `SongItemInstance` / `ScriptureItemInstance` lookups use the library.
- Resolution order: explicit item Id -> playlist override -> app default -> `AppPreferences.DefaultTheme` -> `new BaseSlideTheme()`.
- Designer edits library themes directly; changes mark dirty and trigger a debounced save.
- Removing a theme deletes its file; items pointing at that Id fall back to the default.
- Designer asset picking copies into the Media Library via `PortableAssetCopier` (no more `<playlistDir>/Themes/Backgrounds`).
- Remove Id special-casing of the default theme only where it no longer applies; keep it for `AppPreferences.DefaultTheme`.

## 3. App-level defaults
- `AppPreferences`: add `DefaultSongThemeId`, `DefaultSongMotionThemeId`, `DefaultScriptureThemeId`.
- Playlist keeps its three Id properties as nullable overrides.

## 4. Set Up UI
- New row on the Library tab, copied from the Media Library row (`SetupWindow.axaml:233-250`): header, description, TextBox bound to `AppPreferences.SlideThemesPath`, Browse... button.
- Browse handler mirrors `BrowseMediaLibraryButton_OnClick`, then reloads the library.
- Save appstate immediately when the path changes (preferences otherwise persist only on exit).

## 5. Migration
Runs in `MainViewModel` open-playlist handler, right after `DeserializePlaylist`.
- For each embedded theme: if library has the Id -> skip (never overwrite, log if content differs). Else resolve asset paths against the playlist directory, copy assets into the Media Library, add theme to the library.
- If app default Ids are unset, adopt the playlist's `Default*ThemeId` values (only if that theme exists in the library). Then clear the playlist's own default Ids so the app default governs.
- Mark the playlist dirty so the next save drops `Designs`.
- Ids are never rewritten, so song library XMLs and scripture items keep resolving.

## 6. Serializer
- `HandsLiftedDocXmlSerializer.SerializePlaylist`: stop writing `Designs`.
- `DeserializePlaylist`: still reads legacy `Designs`.

## 7. Testing
- `SlideThemeLibrary`: load, save, rename, delete, duplicate Ids, corrupt file.
- Migration: no overwrite on same Id, asset copy, default adoption.
- Serializer: no `Designs` written; legacy `Designs` read.
- Resolution order.
- Set Up path change.
- Manual: click through Set Up Browse and the Designer in a running app (UI cannot be verified in-session).

## Risks
- Same-Id themes with differing content across playlists: first migrated wins, later skipped (logged).
- `HandsLiftedApp.Models/` is a stale duplicate project; ignore it.
