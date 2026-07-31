# Portable Playlists — Design

## Problem

Playlists should be "portable": prepared on one computer, then copied to a church PC and just work. Today this only partly holds:

- `PlaylistWorkingDirectory` (the folder containing `playlist.xml`) is already used as the base for relative-path resolution in [HandsLiftedDocXmlSerializer.cs](../../../HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs) for the logo, theme background graphics, and plain media items — but only when the referenced file *happens* to already live under that folder. Nothing copies files there.
- The only `File.Copy` in the codebase is an unrelated library-export feature ([LibraryQueryView.axaml.cs:186](../../../HandsLiftedApp.Core/Views/LibraryView/LibraryQueryView.axaml.cs:186)). Directly-imported images/video/audio, theme background graphics, and the logo are referenced wherever the user originally browsed them from — often outside the playlist folder — so `ToRelativePath` produces useless `..\..\Users\...` style paths that don't resolve on another machine.
- PDF/PPTX/Google Slides import ([PDFSlidesGroupItemInstance.cs](../../../HandsLiftedApp.Core/Models/RuntimeData/Items/PDFSlidesGroupItemInstance.cs), [PowerPointPresentationItemInstance.cs](../../../HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs), [GoogleSlidesGroupItemInstance.cs](../../../HandsLiftedApp.Core/Models/RuntimeData/Items/GoogleSlidesGroupItemInstance.cs)) exports rendered PNG slides into a timestamped subfolder of `PlaylistWorkingDirectory` on every `Sync()`, and bakes the resulting per-slide paths into `playlist.xml` as `MediaItem.SourceMediaFilePath` entries. Old export folders are never cleaned up. The *original* source `.pdf`/`.pptx` stays wherever it was picked from and is never copied in — so re-import has no reliable source once moved to another PC.
- There's an existing, currently unused, per-machine cache service ([ImportCacheService.cs](../../../HandsLiftedApp.Core/Services/ImportCacheService.cs)) keyed by hashing the *absolute path* of the source file — not wired to anything, and the path-based key wouldn't survive a move to another machine anyway (same file, different absolute path).
- Screen configuration (monitor assignment etc.) is explicitly **not** in scope — it's correctly machine-local today (`AppPreferences`) and should stay that way.

## Goals

- Copy the whole playlist folder to another PC and have every slide, image, video, theme, and logo resolve correctly with no manual re-linking.
- Re-import of PDF/PPTX presentations works on the new PC without needing the original file's original location to still exist.
- Keep the playlist folder itself lean — don't ship large, regenerable rendered-PNG exports inside it.

## Non-goals

- Screen/monitor configuration portability (stays machine-local, unchanged).
- Packaging into a single archive/container file format — the portable unit is a plain folder the user copies/zips themselves.
- Deduplicating identical file content across separate imports — re-adding the same file twice creates two copies. Not worth the complexity.
- Making PDF/PPTX re-conversion work on a machine without the required tooling (PowerPoint/interop) installed — confirmed the church PC always has the same tooling as the authoring PC, so this is out of scope.

## Folder layout

```
MyPlaylist/
  playlist.xml
  Media/
    Images/
    Video/          ← includes motion-background videos referenced by songs
    Audio/
  Themes/
    Backgrounds/    ← theme BackgroundGraphicFilePath copies
    Logo/           ← playlist LogoGraphicFile copy
  Sources/          ← original .pdf/.pptx copies (flat, one per import)
```

`PlaylistWorkingDirectory` remains the folder containing `playlist.xml` (unchanged from today — see [PlaylistInstance.cs:441](../../../HandsLiftedApp.Core/Models/PlaylistInstance.cs:441) and [MainViewModel.cs:201](../../../HandsLiftedApp.Core/ViewModels/MainViewModel.cs:201)).

Rendered PDF/PPTX/Google-Slides PNG exports do **not** live under this folder — see Cache section below.

## Copy-on-add for media, theme graphics, logo

**Media (images/video/audio) and PDF/PPTX sources — one central hook.** Every route for adding a media/presentation item (the "Add Item" browse dialog, drag-and-drop, and picking a result from a Library — including a media-bin `Library`, see [Library.cs:34](../../../HandsLiftedApp.Core/Models/Library/Library.cs:34)) already funnels through the single `AddItemByFilePathMessage` handler in [PlaylistInstance.cs:168](../../../HandsLiftedApp.Core/Models/PlaylistInstance.cs:168), which calls `CreateItem.GenerateItem(filePath)` ([CreateItem.cs:114](../../../HandsLiftedApp.Core/CreateItem.cs:114)). That function currently sets `SourceMediaFilePath`/`SourcePresentationFile` directly to whatever `filePath` it's given — a library folder path and a raw disk path are indistinguishable to it. The copy-into-playlist-folder step belongs here, in this one place, not scattered across individual pickers — it then automatically covers Library-sourced media too, with no separate design needed for that case.

Song and Scripture library items need no such change: [CreateItem.cs:151](../../../HandsLiftedApp.Core/CreateItem.cs:151) already parses `.txt`/`.xml` library files straight into fully-embedded `SongItem`/`ScriptureItem` data (lyrics, stanzas, arrangements inline) — no file reference is kept at runtime, so they're already portable/decoupled today. Confirmed no changes needed there.

**Theme background graphic and logo pickers** are separate, dedicated code paths (not part of the media-add flow above), so they each get their own copy-on-add call into `Themes/Backgrounds` / `Themes/Logo` respectively.

All copies land in the appropriate subfolder (`Media/Images`, `Media/Video`, `Media/Audio`, `Themes/Backgrounds`, `Themes/Logo`, `Sources/`), and the item/theme is updated to reference the copy from then on. The original picked-from location (library folder or arbitrary disk path) is never referenced again.

**Naming/collision rule:** the copy keeps the original filename. Only if a *different* file already exists at that name is a short content-hash suffix appended (`IMG_0001_a3f9.jpg`). Re-adding the exact same file a second time just creates a duplicate with the same name (harmless, simplest behavior — no dedup logic).

This makes the existing `ToRelativePath`/`ToAbsolutePath` round-trip in `HandsLiftedDocXmlSerializer` correct by construction, since these paths are now always under `PlaylistWorkingDirectory`.

## PDF/PPTX/Google Slides: source portability + non-portable render cache

**Source file, copied in:**
- On adding a PDF/PPTX presentation, the original file is copied into `Sources/` (same naming/collision rule as above).
- `SourcePresentationFile` now always resolves under the playlist folder, so [HandsLiftedDocXmlSerializer.cs:206](../../../HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs:206) (and the equivalent PDF/GoogleSlides branches) must be fixed to call `RelativeFilePathResolver.ToRelativePath` on save — today it calls `ToAbsolutePath`, which was previously a no-op workaround for a path that was never under the playlist folder in the first place.
- Google Slides items have no local original file (fetched by presentation ID via API) — unaffected by this part; already effectively portable since it's an ID reference, not a file path.

**Rendered PNGs move to a per-machine cache, not the playlist folder:**
- `ImportCacheService` is rewired into the three `Sync()` methods ([PDFSlidesGroupItemInstance.cs:135](../../../HandsLiftedApp.Core/Models/RuntimeData/Items/PDFSlidesGroupItemInstance.cs:135), equivalent PowerPoint/GoogleSlides methods) in place of writing into `PlaylistWorkingDirectory`. This also incidentally fixes the existing bug where every `Sync()` run leaves behind an orphaned timestamped export folder that's never cleaned up.
- `ImportCacheService.GetFileImportCacheDirectory` changes its key from hashing the source file's **absolute path** to hashing the source file's **content** (SHA256 of bytes), so the same original file produces the same cache directory regardless of which machine or which absolute path it's copied to. `GetKeyedCacheDirectory` (used for Google Slides IDs) is unaffected — a presentation ID is already a stable, path-independent key.
- `Items` (the list of per-slide `MediaItem` exports) stops being serialized into `playlist.xml` for PDF/PPTX/Google Slides groups — it's fully derived and rebuilt from the cache directory's contents at load time, not baked into the document.

## Load flow (eager regeneration)

On playlist open, for every PDF/PPTX/Google Slides item:
1. Compute the cache key (content hash of `SourcePresentationFile`, or the existing ID-based key for Google Slides).
2. Look up `ImportCacheService`'s directory for that key.
3. If empty/missing (first time on this PC, or the playlist was just moved), run the conversion now — same worker/progress-reporting path as today's `Sync()` — writing PNGs into the cache directory instead of the playlist folder.
4. Populate `Items`/`Slides` from the cache directory's contents.

This runs eagerly for all such items right after load (background thread, progress UI), not lazily on first activation — confirmed acceptable to trade a slower first-open for the item being instantly ready when clicked live. Subsequent opens on the same machine hit a warm cache and are fast.

## Back-compat

Playlists saved before this change have `Items` baked into `playlist.xml`, pointing at PNGs under old-style timestamped folders inside the playlist directory. On load, if legacy `Items` are present and the files still exist, use them as-is — no forced migration or one-time conversion step. New saves stop writing `Items` for these three group types. A playlist naturally transitions to the new cache-based approach the next time it's re-synced and re-saved.

## Testing

- Round-trip test: save a playlist with each asset type (image, video, audio, theme background, logo, PDF, PPTX) from one simulated "playlist directory", copy the folder to a different path, reload, assert every reference resolves.
- Adding media via a Library (media-bin) selection copies the file into the playlist folder identically to a raw disk drag-drop, using the same `CreateItem.GenerateItem` code path.
- `ImportCacheService` content-hash key: same bytes at two different absolute paths → same cache directory.
- Legacy-format playlist (pre-existing baked-in `Items`) still loads and plays correctly without forcing a re-sync.
- `Sync()` no longer creates any files under `PlaylistWorkingDirectory` for PDF/PPTX/GoogleSlides items.
