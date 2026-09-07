# Playlist songs as library references (not copies)

Date: 2026-09-07
Status: Approved (design), not yet implemented

## Problem

Adding a song to a playlist currently deep-copies the song's data
(title, stanzas, arrangement, copyright, theme reference) out of the
library into a standalone `SongItemInstance`. The playlist item has no
lasting connection to the library song it came from. This means:

- Editing a song's lyrics/arrangement/theme from one playlist never
  updates the library master, or any other playlist using "the same"
  song — they silently diverge.
- Every song lives twice: once in the library XML file, once inline
  in each playlist XML file that uses it.

Goal: playlist items hold a **reference** (by song UUID) to a shared
library song, resolved live, with edits writing through to the shared
object. A future "playlist export" feature may still want a
full-copy/detach mode — this design keeps that door open but does not
build it now.

## Current state (for reference)

- Library-side song: `SongItem` (`HandsLiftedApp.Data/Models/Items/SongItem.cs`),
  identified by `Item.UUID` (Guid), never used today as a lookup key —
  every library read re-parses the song's XML file from disk.
- Playlist-side: `SongItemInstance` (`HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs`)
  inherits `SongItem`. Populated by `ItemInstanceFactory.ToItemInstance`
  via manual field-by-field copy from a freshly-deserialized `SongItem`
  (`ItemInstanceFactory.cs:23-49`).
- Playlist XML serialization (`HandsLiftedDocXmlSerializer.SerializeItem`,
  lines 128-162) writes the **full song content inline** into the
  playlist file — lyrics included.
- `SongLibrary` (`HandsLiftedApp.Core/Models/Library/SongLibrary.cs`)
  is file/path-based: enumerates a directory, keeps a search index
  keyed by file path, holds no long-lived `SongItem` objects.
- Existing precedent for reference-style resolution already exists in
  this codebase: `SongItem.Design`/`ScriptureItem.Design` is a Guid
  resolved against `PlaylistInstance.Designs` via
  `SongItemInstance.ResolvedDesignTheme` (`SongItemInstance.cs:32-36`)
  and more fully in `ScriptureItemInstance.ResolvedDesignTheme`
  (`ScriptureItemInstance.cs:127-150`), including reactive
  re-resolution when the referenced object changes
  (`ScriptureItemInstance.cs:64-106`). This is the template for the
  new song-reference mechanism, extended to resolve against an
  app-level library service instead of a playlist-owned collection.
- `ScriptureItemInstance` is a softer existing precedent for "hold a
  reference, regenerate content on demand" — it stores only a
  scripture range, not the text, and calls
  `ScriptureLocalUsxStore` to regenerate slides.

## Decisions made during design

1. **Edit semantics: write-through.** Editing a song's content from
   within a playlist (lyrics, arrangement, theme) edits the shared
   library song directly. The change is visible in every other
   playlist referencing the same song. This is a deliberate behavior
   change from today (where playlist edits are currently isolated
   copies that never touch the library).
2. **Resolution: in-memory UUID-keyed cache.** A library-level service
   maintains `Dictionary<Guid, SongItem>` (built/refreshed alongside
   the existing path-keyed search index), so playlist items resolve a
   `SongId` (Guid) against a live shared object, not a file path.
3. **Missing song: broken placeholder, not silent fallback snapshot.**
   If a referenced song can't be resolved (deleted, playlist opened on
   another machine without that library file), the playlist item shows
   a clear "missing song" placeholder slide and stays flagged broken.
   No shadow copy of last-known content is carried in the playlist
   XML. If the song reappears (re-imported, file restored with the
   same UUID), it resolves again automatically.
4. **`SongItemInstance` shape: proxy/facade, not a rewritten reference
   type.** `SongItemInstance` keeps its current public property
   surface (`Title`, `Stanzas`, `Arrangement`, `Design`, `Copyright`,
   etc.) — same names, same types — but these become forwarding
   properties onto the resolved shared `SongItem` instead of
   locally-owned fields. This means every existing XAML binding, the
   Skia spec builders, the song editor viewmodel, and
   `SongImporter.songItemToFreeText` continue to work unchanged. Only
   `ItemInstanceFactory`, `HandsLiftedDocXmlSerializer`, and the new
   library index service change.

## Components

### `SongLibraryIndex` (new; or an extension of `SongLibrary`)

App-level singleton. Responsibilities:

- `SongItem? Resolve(Guid id)` — returns the cached song, or
  lazily deserializes it from its library file on first reference,
  or `null` if no library file matches this UUID.
- Maintains `Dictionary<Guid, SongItem>` built/refreshed on library
  scan (`TriggerRefresh`), alongside the existing path-keyed search
  index in `SongLibrary`.
- `IObservable<Guid> SongChanged` — fires whenever a cached song's
  content is mutated (via write-through edit) or its backing file
  changes on disk, carrying the changed song's UUID.
- Owns writing edited songs back to their XML file (replaces the
  save-path currently in `SongEditorWindow.DoSaveToLibrary` for the
  in-place-edit case).

### `SongItemInstance` (modified)

- Holds `SongId` (Guid) — the only playlist-item-specific identity
  left; this is the reference key.
- `ResolvedSong => SongLibraryIndex.Resolve(SongId)` (nullable).
- Property getters/setters for `Title`, `Stanzas`, `Arrangement`,
  `Arrangements`, `SelectedArrangementId`, `Copyright`, `Design`,
  `StartOnTitleSlide`, `EndOnBlankSlide`,
  `MotionBackgroundVideoPath`, `SlideTransitionDurationMs` all proxy
  to `ResolvedSong`. Setters mutate the shared cached object
  (write-through) and trigger `SongLibraryIndex`'s save + notify.
- Subscribes to `SongLibraryIndex.SongChanged` filtered to its own
  `SongId`; on fire, re-raises `PropertyChanged` for forwarded
  properties and sets `Cached = null` to force slide regen (per the
  existing "reassigning a property doesn't always trigger a
  re-render" gotcha already documented in CLAUDE.md for this
  codebase's batch-render sweep).
- When `ResolvedSong == null` (missing case): properties return
  placeholder values (e.g. `Title = "(Missing Song)"`), and
  `UpdateStanzaSlides()` emits a single "missing song" slide instead
  of stanza-derived slides.

### `SongItem` (library-side data model)

Unchanged shape.

## Data flow

### Add song to playlist

1. User adds a song from the library. The library scan already knows
   each song's UUID (from `SongLibraryIndex`), so the "add" message
   carries the UUID directly — no XML re-parse needed at add time.
2. `PlaylistInstance` creates `new SongItemInstance(playlist) { SongId
   = uuid }` and inserts it. No field-by-field copying.
3. `SongLibraryIndex.Resolve(uuid)` ensures the song is cached
   (parsing from disk on first reference if not already cached).

### Playlist XML serialization

`HandsLiftedDocXmlSerializer.SerializeItem`/`DeserializePlaylist`:
the song item block shrinks to just `SongId`, replacing the current
full inline `Title`/`Stanzas`/`Arrangement`/`Copyright`/`Design`/etc.
block.

### Migration of existing playlist files

Old-format playlist XML (inline song data, no `SongId`) is detected on
load by the presence of inline `Stanzas`/`Title` rather than a bare
`SongId`:

- If the item's `UUID` matches an existing library song → adopt the
  reference, discard the inline copy (library content wins).
- If no match (song was deleted from the library, or the playlist
  predates that song existing as a library file) → import the inline
  data as a new library song (write it to the library directory,
  assign/keep its UUID), then reference it.

This avoids data loss and avoids playlists breaking into "missing
song" placeholders immediately after upgrading.

### Future: playlist export (not built now)

The current "write full song inline" serialization code path is kept
as an opt-in alternate mode (rather than deleted) so a future
"Detach/Export" action can re-serialize a `SongItemInstance` back to a
standalone inline block for a one-off exported file, without needing
to re-derive that logic later.

## Error handling

- `SongLibraryIndex.Resolve` never throws; returns `null` on any
  failure to locate/parse. All consuming code null-checks.
- Write-through save failures (disk full, permissions, locked file)
  surface in the UI the same way as the existing failure-surfacing
  pattern used for Google Slides import failures (commit `f5d8e99`),
  not swallowed to a log.
- Two playlists editing the same shared song concurrently: last-write
  wins at the in-memory cache/disk level. Both UIs update reactively
  via `SongChanged`. No locking or merge — accepted risk for this
  app's single-user desktop scope, not solved by this design.

## Testing

- Unit: `SongLibraryIndex.Resolve` (found/missing paths), write-through
  mutation visible to a second `SongItemInstance` referencing the same
  `SongId`, migration path (old-format XML → library import →
  reference).
- Integration: add a song to two playlists, edit in one, verify both
  reflect the change; delete a song from the library, verify the
  playlist shows the missing-placeholder and does not crash
  rendering; reload an old-format playlist file, verify round-trip.
- Manual: actually render a playlist containing a referenced song
  end-to-end in the running app before calling implementation done —
  structural/unit tests alone don't prove the Skia render pipeline
  picks up the resolved song correctly.

## Open items for the implementation plan

- Decide whether `SongEditorWindow.DoSaveToLibrary` is deleted (since
  in-place edits now write through automatically) or repurposed
  specifically for a "save as new song" fork/duplicate action.
- Confirm exact debounce/timing for write-through disk saves during
  active editing (e.g. per-keystroke vs. on-blur/on-close), to avoid
  excessive file I/O.
- Confirm whether `SongLibraryIndex` is a new class or an extension of
  the existing `SongLibrary`/`SongLibrarySource` types.
