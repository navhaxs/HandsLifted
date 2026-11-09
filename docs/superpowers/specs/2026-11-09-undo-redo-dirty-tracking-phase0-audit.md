# Undo/Redo + Dirty Tracking — Phase 0 Audit

Date: 2026-11-09. Read-only inventory; no code changed. Confidence notes: items marked **(verify)** were
read from code but not confirmed by running the app.

## Decisions already made

- "Dirty" tracks **the playlist file only**. Song library and theme library save independently and are out of scope.
- Dirty indicator already exists: `Ellipse` in `MainView.axaml:242-251` bound to `Playlist.IsDirty`.
- Closing / New / Load must still prompt about unsaved changes **when auto-save is off**.
- Undo scopes: one stack for the playlist, one per editor window.
- Undo works across saves/auto-saves.

---

## Part A — Dirty tracking

### A1. How it works today

| Piece | Location | Behaviour |
|---|---|---|
| Flag | `PlaylistInstance.cs:462` | Plain bool, initial value `true` (`:457`). |
| Set `true` (top-level props) | `PlaylistInstance.cs:290-302` | `WhenAnyValue(Title, LogoGraphicFile, Items, 3× DefaultThemeId)`. **No `Skip(1)`**, so it fires on construction. |
| Set `true` (transition ms) | `:304-310` | Has `Skip(1)`. Inconsistent with the line above. |
| Set `true` (Items add/remove/move) | `:368-374` `OnItemsCollectionChanged` | Fires for the playlist's top-level item list only. |
| Set `true` (item data) | `:421-426` ← `PlaylistItemInstanceCollection.ItemDataModified` | Fired by (a) `[DataField]` props via reflection, (b) each item's own `IItemDirtyBit.ItemDataModified`. |
| `Changed` only, **no `IsDirty`** | `:376-394` designs | Design add/remove and theme edits raise `Changed` (autosave hint) but never set `IsDirty`. |
| Set `false` | `PlaylistDocumentService.cs:21,77,88,106`; `MainWindow.axaml.cs:361`; `MainViewModel.cs:421` | Manual save, 3 autosave outcomes, New playlist, end of load. |
| Read | `MainWindow.axaml.cs:214` (exit), `:312` (new), `MainViewModel.cs:309` (load), ellipse | Drives prompts + indicator. |

### A2. Ground truth that already exists

`PlaylistDocumentService.AutoSaveToFile` serializes (`HandsLiftedDocXmlSerializer.SerializePlaylistToBytes`),
SHA-256s the bytes and compares to the last-saved hash. This is already a reliable "has anything
serializable changed" signal. **But it is gated by `AutoSaveEnabled`** (`MainViewModel.AutoSaveNow`
returns early when off), so with auto-save off nothing uses it and the ellipse/prompts rely only on the
event plumbing in A1.

### A3. Gaps found

| # | Gap | Evidence | Impact |
|---|---|---|---|
| G1 | **Media-group slide edits don't raise dirty.** `MediaGroupItemInstance` watches `WhenAnyValue(Items, Title, AutoAdvanceTimer)`, which only fires when the `Items` *reference* is replaced. The author's own comments say `// TODO not working` (`:42`, `:50-52`). Add/delete/move/duplicate/change-media of slides inside a group mutate `Items` in place. **(verify)** | `MediaGroupItemInstance.cs:41-52` | With auto-save **off**, deleting or reordering slides may not show the ellipse or trigger the exit prompt → lost work. |
| G2 | **`[DataField]` coverage is thin and by-reflection.** Only `Item.Title` and `Item.SlideTransitionDurationMs` carry it (+1 in `SongItemInstance`). `GetProperties()` + `IsDefined(inherit:false)` on an *override* (e.g. `SongItemInstance.Title`) won't see an attribute declared on the base. | `PlaylistItemInstanceCollection.cs:54-64`, `Item.cs:24,35` | Any new serialized property is silently untracked until someone remembers the attribute (already bitten once: `MotionBackgroundVideoOverride`). |
| G3 | **Mutations of shared reference objects** — `AutoAdvanceTimer` and `MediaItem.Meta` are copied by *reference* in the serializer, and edits to their inner properties don't raise a change on the owner. **(verify)** | `HandsLiftedDocXmlSerializer.cs:164,176,186,198,209` | Timer / slide-meta edits likely not flagged. |
| G4 | **Designs raise `Changed` but not `IsDirty`.** | `PlaylistInstance.cs:376-393` | Mostly moot (themes are app-wide now, saved separately) — but the inconsistency should be resolved deliberately. Per decision, theme edits should **not** dirty the playlist. |
| G5 | **Initial state wrong.** `_isDirty = true` at construction + un-skipped first emission. | `PlaylistInstance.cs:290-302,457` | New/unsaved playlist is "dirty" before anything happens; load code compensates by force-setting at `MainViewModel.cs:421`. |
| G6 | **Writers scattered across 4 files**; nothing guarantees the flag matches reality. | See A1 "Set false" | Undo-to-clean impossible; stale flags (existing tests even simulate this: `PlaylistDocumentServiceTest.cs:93,134`). |
| G7 | **Prompt decisions trust the flag.** Exit/New/Load call `AutoSaveNow` (no-op when off) then read `IsDirty`. | `MainWindow.axaml.cs:210-214,308-312`; `MainViewModel.cs:306-309` | A missed event skips the prompt. |
| G8 | **Serializer coverage gaps** (not saved at all → no dirty signal is *possible* without a serializer fix). `SerializeItem` is an explicit per-type `if` chain; unknown types `return item` as-is. `OnlineVideoItemInstance` has no branch. **(verify)** whether it is serializable as-is. | `HandsLiftedDocXmlSerializer.cs:104-217` | Edits on unhandled types may never persist. Out of scope to fix here but the Phase 1 coverage test will expose it. |
| G9 | `IItemDirtyBit` is implemented by 7 item types (Song, Scripture, MediaGroup, PPT, PDF, GoogleSlides, OnlineVideo) with hand-picked property lists each. | grep `ItemDataModified` | Same "forgot a property" risk repeated per type. |

### A4. Existing tests that touch dirty (must be kept/adapted in Phase 1)

- `PlaylistInstanceTests.cs:176-208` — setting each `Default*ThemeId` marks dirty (`new PlaylistInstance { IsDirty = false }`).
- `PlaylistInstanceTests.cs:285-290`.
- `PlaylistDocumentServiceTest.cs:81,93,98,134` — auto-save clears flag; stale-flag simulation.
- `PlaylistItemInstanceCollectionTests.cs:59` — `MotionBackgroundVideoOverride` → `ItemDataModified`.

These assign `IsDirty` directly, so Phase 1 must either keep a setter-compatible surface or update them.

### A5. Phase 1 design implications (summary)

1. New `DirtyTracker` owned per playlist: `IsDirty := hash(serialize(playlist)) != savedHash`.
   Events (`Changed`, collection changes, etc.) become **hints** that schedule a check.
2. Runs **regardless of `AutoSaveEnabled`**; auto-save then simply reacts to dirty.
3. `RefreshDirty()` synchronous variant used by Exit / New / Load before prompting (closes G7).
4. Saved baseline set by: manual save, auto-save, successful load, new playlist. Untitled/never-saved
   playlist: dirty iff non-empty (matches the existing `isPlaylistEmpty` checks).
5. Fixes G5/G6 by construction; G1/G2/G3/G9 stop being correctness bugs (only latency), because the
   periodic check catches missed events. Event wiring for G1 should still be repaired so the ellipse is prompt.
6. Add a coverage test: for each serializable item type × property, mutate → serialized bytes change AND a
   change hint fires. Surfaces G2/G3/G8 at build time.
7. Must run on the UI thread (serializer walks runtime objects — same constraint as `AutoSaveToFile`).

---

## Part B — Undo/redo mutation-site inventory

Legend: **P** = playlist stack, **E** = editor-window stack, **X** = deliberately not undoable (decide in Phase 6).
Phase numbers refer to the revised plan (1 = dirty tracking, 2 = undo infra, 3 = items, 4 = group slides, 5 = property edits, 6 = editor windows, 7 = hard cases, 8 = hardening).

### B1. Playlist item list (Phase 3, scope P)

| Operation | Site | Notes |
|---|---|---|
| Add item(s) from Add Item dialog (incl. scripture) | `MainViewModel.cs:629-651` (`AddItemMessage` handler) | Insert at index / after item / append. Scripture slides generated async (`:619`). |
| Add items by dropping files | `PlaylistInstance.cs:198-231` | Copies file into media library (side effect: **X**, keep file). `Items.Insert/Add`, then `Sync()`. |
| Delete item | `MainViewModel.cs:177-184` → `MoveItemCommand{REMOVE}` → `:678` | Needs snapshot (serialize item, restore via `ItemInstanceFactory`). |
| Duplicate item | `:680-689` | Already uses serialize→clone→`ToItemInstance`. Undo = remove by UUID. |
| Move up / down | `:663-676` | Trivial reversible. |
| Drag reorder | `MoveItemMessage` handler `:812-826` | Trivial reversible. |
| Initial demo content | `MainViewModel.cs:89-96` | Design-mode only; ignore. |

### B2. Slides inside group items (Phase 4, scope P)

| Operation | Site | Notes |
|---|---|---|
| Delete slide | `MainViewModel.cs:143-175` | Also restores selected slide. |
| Duplicate slide | `:257-281` (`Clone()`) | |
| Split group from here | `:120-141` (`slice`) | Two-item change: new item inserted + source truncated. Single transaction. |
| Change media file | `:186-246` | Sets `MediaItem.SourceMediaFilePath`; may copy into library (**X** for the copy). |
| Move slide within / across groups | `MoveSlideCommand` `:698-777` | Cross-group = insert + remove; selection-restoration logic must be preserved. |
| Add dropped files to group | `AddFilesToGroupItemCommand` `:779-810` | Library copy = **X**. |
| **Bypass sites (not through MainViewModel)** | `ItemSlidesView.axaml.cs:360,368` (insert media/slide from gap buttons) | Must be rerouted through the undo service. |
| **Group editor window** | `GroupItemsEditor.axaml.cs:81,98,144,160,175,197,231`; `MediaGroupItemEditor.axaml.cs:187,284,295,304,312,323` | Direct `Items.*` mutation. Scope: these edit the *same* playlist data, so they belong on the **playlist stack (P)**, not a separate editor stack — decide in Phase 6. |

### B3. Property edits (Phase 5)

| Property | Where edited | Dirty today? |
|---|---|---|
| `Playlist.Title`, `LogoGraphicFile` | playlist info / settings UI | yes (`WhenAnyValue`) |
| `Playlist.SlideTransitionDurationMs` | settings UI | yes |
| `Playlist.Default{Song,SongMotion,Scripture}ThemeId` | `SlideThemeDesigner.axaml.cs:292,303,314` + theme pickers | yes |
| `Item.Title` | item title edit | yes (`[DataField]`) |
| `Item.SlideTransitionDurationMs` (per-item fade) | `ItemEditMenuButton.axaml.cs:119,124` | yes (`[DataField]`) |
| `SongItemInstance.MotionBackgroundVideoOverride` | `ItemEditDockRoot.axaml.cs:26` | yes (test-pinned) |
| Scripture reference (title + verses) | `ItemEditMenuButton.axaml.cs:92` | via `ScriptureItemInstance` `:125` |
| `AutoAdvanceTimer` (group/PPT/PDF/GSlides) | timer UI | **uncertain — G3** |
| Section heading / comment text | item text boxes | verify (Data types, no instance class) |

### B4. Song editing (Phase 6, scope E — song editor window)

Edits operate on the **shared library song** (`SongItemInstance.ResolvedSong`), saved independently of the playlist.
Mutation sites: `SongEditorViewModel.cs:106,144,153,156,160,163,180` · `SongLyricBlockEditor.axaml.cs:20,28,40,54` ·
`SongArrangementControl.axaml.cs:108,133,139` · `SingleSongEditorWindow.axaml.cs:20` ·
drag behaviours `StanzaDragControlBehavior.cs:110,510`, `StanzaVerticalDragControlBehavior.cs:350`.
Because song changes don't dirty the playlist (decision), these belong on a per-editor stack; the arrangement
edits made from the *playlist-side* arrangement control (`SongArrangementControl`) change the shared song too —
decide in Phase 6 which stack owns them.

### B5. Out of undo scope / non-undoable (confirm in Phase 7)

- Live state: which slide is on screen, logo/blank toggles, selection indexes (restore sensibly after undo; never record).
- Library file operations: `LibraryQueryView.axaml.cs:210` (`File.Move` rename on disk), media-library copies, song import (`SongImporter`).
- Theme library edits (`SlideThemeLibrary`, own debounced save) — separate from playlist; candidates for their own scope later.
- Playlist load / new (clears all stacks and re-baselines dirty).
- Recent-playlists list (`WelcomeWindow.axaml.cs:186`).

### B6. Cross-cutting hazards for the undo design

1. **Identity**: item instances are rebuilt (load, duplicate, snapshot-restore) → commands reference items by `UUID`, not index/reference.
2. **Slide regeneration**: group edits require `RegerenateSlides`/`GenerateSlides()` after do *and* undo; slide instances are cached by `GroupItem` identity (reorders preserve instances — don't break this).
3. **Reused-instance cache**: when undo re-applies a `Theme`/render-relevant prop to a reused slide instance, reset `Cached = null` (CLAUDE.md).
4. **Async generation** (scripture, PDF, PPT, GSlides, online video): undo/redo mid-generation needs an ignore/cancel path.
5. **Flyout/popup dialogs**: any undo-driven UI must be click-tested in the running app (CLAUDE.md silent-failure class).
6. **Auto-save**: undo/redo mutates the playlist → auto-save writes it. Accepted by decision.

---

## Suggested next step

Start **Phase 1 (dirty tracking)**. Its first slice is small and low-risk:
`DirtyTracker` + hash baseline + `RefreshDirty()` at Exit/New/Load + remove scattered `IsDirty` writes,
keeping existing tests green, then add the coverage test (which will tell us which of G1/G2/G3/G8 are real).
