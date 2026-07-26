# Scripture Slide Type — Design Spec

**Date:** 2026-07-26
**Status:** Approved

---

## Summary

Add a new `Scripture` item/slide type, parallel to the existing `Song` item/slide type, so a user can enter a passage reference (e.g. "John 3:16-21"), generate one slide per verse, preview it, and project it — reusing HandsLifted's existing Skia slide-render pipeline. Ports the known-working `UsxBibleParser` and USX 3.0 parsing logic from the sister app MyBibleApp (`C:\Users\Jeremy\RiderProjects\MyBibleApp\MyBibleApp\Services\UsxBibleParser.cs`), adapted to this repo's net8.0/Avalonia 11.3.18 stack and `Scripture*` naming.

Rejected alternative: reusing `SongItem`/`SongSlide` directly with verse text stuffed into `Lyrics`. Rejected because it loses reference metadata (book/chapter/verse/translation), is confusing in the library UI (a "song" that's actually scripture), and blocks future scripture-specific features (translation switching, verse-range re-generation).

---

## Architecture

### New project: `HandsLiftedApp.Importer.Scripture`

Class library, `net8.0`, Avalonia `$(AvaloniaVersion)` (11.3.18) — matches the shape of `HandsLiftedApp.Importer.PDF`/`HandsLiftedApp.Importer.OnlineSongLyrics`. Referenced by `HandsLiftedApp.Core.csproj` only (alongside the other importer refs, `Core.csproj:72-83`); `Desktop` does not reference it directly, same as other importers.

Contents:
- `UsxScriptureParser.cs` — ported from `UsxBibleParser.cs`, logic unchanged (framework-agnostic `XDocument` → model parse), renamed for this repo's naming convention.
- Models: `ScriptureBook`, `ScriptureParagraph`, `ScriptureFootnote` — ported from `BibleBook`/`BibleParagraph`/`BibleFootnote`, same shape, renamed. `BibleVerse` (unused in source) and `BibleInkStroke` (Avalonia-coupled, dead code in source app) are dropped — not ported.
- `ScriptureSourceLoader.cs` — fetch + disk cache for USX by translation+book code, ported from `UsxBibleApiLoader.cs`'s approach (`https://v1.fetch.bible/bibles/{translation}/usx/{book}.usx`, memory + disk cache), decoupled from MyBibleApp's app-specific cache path.

**Decision — SIL.Scripture NuGet**: `HandsLifted.FetchBible` (an existing unused scaffold project) already references the `SIL.Scripture` package, a Paratext-grade USX/versification library. This spec does **not** adopt it — it ports the already-validated `UsxBibleParser` instead, to avoid open-ended API research inside this spec. Swapping to `SIL.Scripture` later is a self-contained follow-up if the hand-rolled parser proves insufficient (e.g. combined-verse or non-Protestant-canon edge cases).

### Data model — new files in `HandsLiftedApp.Data`

- `Models/Items/ScriptureItem.cs` (`ScriptureItem : Item`) — mirrors `SongItem.cs:14-107`. Stores: `Translation` (string, e.g. "eng_bsb"), `Book` (string, USX code e.g. "JHN"), `StartChapter`, `StartVerse`, `EndChapter`, `EndVerse`, and a cached `IReadOnlyList<ScriptureParagraph> CachedVerses` (populated on generate/refresh, persisted so the library doesn't re-fetch on every load).
- `Models/Slides/ScriptureSlide.cs` (`ScriptureSlide : Slide`) — mirrors `SongSlide.cs:7-63`. Holds `Text` (single verse text), `Label` (e.g. "John 3:16"), `ParentScriptureItem`.

### Runtime + slide generation — new `ScriptureItemInstance.cs`

`HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`, mirrors `SongItemInstance.cs:181-336`:
- `GenerateSlides()` → `UpdateVerseSlides()`.
- **Splitting rule (MVP): one verse per slide.** No existing splitting precedent in this codebase applies (`SongItemInstance` splits on blank lines; PDF/Google Slides importers are strict 1:1 — neither fits verse text). One-verse-per-slide is the simplest correct behavior for congregational readability and matches how churches typically project scripture. A "verses per slide" grouping option can be added later purely inside `UpdateVerseSlides()` — it does not require touching the parser, model, or render layers.
- Diffs new slide list against previous `Slides` the same way `SongItemInstance` does, to preserve slide identity/order across regeneration (e.g. user edits the verse range and regenerates).

### UI — new `ScriptureEditorWindow`/`ScriptureEditorViewModel`

`HandsLiftedApp.Core/Views/Editors/ScriptureEditorWindow.axaml(.cs)` + `HandsLiftedApp.Core/ViewModels/Editor/ScriptureEditorViewModel.cs`, mirrors `SongEditorWindow`/`SongEditorViewModel`:
- Fields: translation picker (dropdown, hardcoded initial list e.g. BSB/KJV/NASB — matching whatever translations the sister app's fetch endpoint serves), book picker, chapter/verse range entry.
- "Generate" action: calls `ScriptureSourceLoader` + `UsxScriptureParser`, populates `ScriptureItemInstance`, calls `GenerateSlides()`, refreshes the live slide list shown in the editor (same list-of-slides UI pattern `SongEditorWindow` already has for stanzas/slides).
- Entry point: `LibraryQueryView.axaml.cs:93-107`'s `AddItem_OnClick` gets a Scripture case, gated on active library type the same way the Song case checks `vm.ActiveSongLibrary` (line 96-97) — requires a corresponding `ScriptureLibrary` type and an `ActiveScriptureLibrary` accessor on `LibraryQueryViewModel`, mirroring `LibraryQueryViewModel.cs:49-51` (see `2026-06-16-add-new-song-to-library-design.md`).
- Save/discard/unsaved-changes-confirmation flow mirrors the Song new-item flow from `2026-06-16-add-new-song-to-library-design.md` exactly (same `IsNewSongMode`-equivalent `IsNewScriptureMode` pattern, same confirmation dialog shape, new `NewScriptureUnsavedConfirmationWindow` modelled on `NewSongUnsavedConfirmationWindow`).

### Preview + projection — reuse existing Skia render pipeline

- New `HandsLiftedApp.Core/Render/Skia/Builders/ScriptureSlideSpecBuilder.cs`, mirrors `SongSlideSpecBuilder.cs:28-50` — builds background + text `RenderElement`s from `ScriptureSlideInstance.Text`/`.Theme`.
- New switch arm for `ScriptureSlideInstance` in both `LivePane.axaml.cs:76-88` and `ProjectorWindow.axaml.cs` (both currently switch on slide type per `docs/superpowers/README`/architecture notes — both must be updated, not just `LivePane`).
- No new rendering engine, no new preview control — same `SlideRenderer`/`SlideCanvas` infra Song already uses. This gives editor live preview and actual projector output from the same code path, so they can't visually drift apart.

---

## File Changes

### New project
1. `HandsLiftedApp.Importer.Scripture/HandsLiftedApp.Importer.Scripture.csproj` (net8.0, Avalonia 11.3.18)
2. `HandsLiftedApp.Importer.Scripture/UsxScriptureParser.cs` (ported from MyBibleApp `UsxBibleParser.cs`)
3. `HandsLiftedApp.Importer.Scripture/Models/ScriptureBook.cs`, `ScriptureParagraph.cs`, `ScriptureFootnote.cs` (ported from `BibleBook.cs`/`BibleParagraph.cs`/`BibleFootnote.cs`)
4. `HandsLiftedApp.Importer.Scripture/ScriptureSourceLoader.cs` (ported/adapted from `UsxBibleApiLoader.cs`)

### `HandsLiftedApp.Data`
5. `Models/Items/ScriptureItem.cs`
6. `Models/Slides/ScriptureSlide.cs`
7. `Models/Library/ScriptureLibrary.cs` (mirrors `SongLibrary`)

### `HandsLiftedApp.Core`
8. `Models/RuntimeData/Items/ScriptureItemInstance.cs`
9. `Views/Editors/ScriptureEditorWindow.axaml` + `.axaml.cs`
10. `ViewModels/Editor/ScriptureEditorViewModel.cs`
11. `Views/Confirmation/NewScriptureUnsavedConfirmationWindow.axaml` + `.axaml.cs`
12. `Render/Skia/Builders/ScriptureSlideSpecBuilder.cs`
13. `HandsLiftedApp.Importer.Scripture` project reference added to `HandsLiftedApp.Core.csproj`

### Edits to existing files
14. `Views/LibraryView/LibraryQueryView.axaml.cs` — `AddItem_OnClick` gets a Scripture case
15. `ViewModels/LibraryQueryViewModel.cs` — add `ActiveScriptureLibrary` accessor
16. `Views/LivePane.axaml.cs` — switch statement gets `ScriptureSlideInstance` arm
17. `Views/ProjectorWindow.axaml.cs` — switch statement gets `ScriptureSlideInstance` arm
18. `HandsLiftedApp.sln` — register new project

### Repo mechanics
19. New git worktree off HandsLifted `master` (working tree confirmed clean — safe to branch), branch name `feature/scripture-slide-type`.

---

## Edge Cases

| Case | Behaviour |
|---|---|
| Invalid/out-of-range passage reference (e.g. chapter doesn't exist) | Generate action shows inline validation error, does not create slides |
| Network fetch fails (translation not cached, offline) | Same error handling as MyBibleApp's `UsxBibleApiLoader` — surfaces a failure, existing cached translations still usable |
| Verse range spans a chapter boundary (e.g. John 3:34 - John 4:2) | Supported — `ScriptureItem` stores start/end chapter independently, `ScriptureSourceLoader` fetches all needed chapters |
| Empty/whitespace-only verse text (shouldn't occur from validated USX) | Slide skipped, not created blank |
| Regenerating an existing `ScriptureItem` with a changed verse range | Full re-diff via `UpdateVerseSlides()`, same identity-preservation approach as `SongItemInstance` |
| Non-scripture library active when "Generate"/Add Scripture invoked | `ActiveScriptureLibrary` returns null, action does nothing (mirrors Song's null-guard) |

---

## What Does Not Change

- `SongItem`/`SongSlide`/`SongItemInstance` and all existing Song flows — untouched.
- `SlideRenderer`/`SlideCanvas`/`SlideRenderSpec` core rendering engine — untouched, only gains a new builder.
- MyBibleApp repo — read-only source for porting, no changes made there.
- No shared/published package between MyBibleApp and HandsLifted — this is a one-time port, not a maintained dependency (per earlier decision).
