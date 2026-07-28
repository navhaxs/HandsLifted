# Handover: Scripture Slide Type Feature

**Date:** 2026-07-26
**Repo:** `C:\Users\Jeremy\RiderProjects\HandsLifted`
**Worktree:** `C:\Users\Jeremy\RiderProjects\HandsLifted\.worktrees\scripture-slide-type`
**Branch:** `feature/scripture-slide-type` (branched from `master` at `9ef8aef`)

## What this feature is

Adding a "Scripture" slide type to HandsLifted (a church slide-presentation app), parallel to the existing `Song` item/slide type. Lets a user enter a passage reference (translation/book/chapter/verse range), generate one slide per verse, and project it. Full design context:

- Spec: `docs/superpowers/specs/2026-07-26-scripture-slide-type-design.md`
- Plans (one per phase, read in order): `docs/superpowers/plans/2026-07-26-scripture-parser-phase1.md`, `phase2.md`, `phase3.md`, `phase4a.md`

## Where things stand

Executed via **superpowers:subagent-driven-development** (fresh implementer subagent per task + independent task reviewer + fix loop, then a final whole-branch review per phase). All work happens in this worktree, not the main `HandsLifted` checkout.

**Progress ledger (not committed to git, but persists on disk in this worktree):**
`.superpowers/sdd/progress.md` — full task-by-task history, every commit SHA, every review verdict, every deferred/minor finding. **Read this first** — it's the authoritative record of everything done so far, more detailed than this note.

### Phases 1–3: DONE, committed, reviewed, merged into this branch

- **Phase 1** — `HandsLiftedApp.Importer.Scripture` project: USX 3.0 parser (`UsxScriptureParser`) + HTTP fetch/cache loader (`ScriptureSourceLoader`). No Avalonia dependency.
- **Phase 2** — Data model + runtime: `ScriptureItem`/`ScriptureSlide` (Data), `ScriptureVerseRangeExtractor` (merges verse text the parser split across paragraphs), `ScriptureSlideInstance`/`ScriptureItemInstance` (Core runtime, one-verse-per-slide generation with identity-preserving diffing).
- **Phase 3** — Rendering: `ScriptureSlideSpecBuilder` (word-wrap/autofit/drop-shadow, ported from `SongSlideSpecBuilder`), self-rendering wiring (`IRenderable`/`Render()`), `LivePane`/`ProjectorWindow` switch arms, thumbnail-strip `DataTemplate`.

Each phase's final whole-branch review found and fixed a real bug before moving on (Phase 2: slide labels used raw book code instead of parsed title; Phase 3: newly-generated slides never got their first render — missing an `EnqueueBatch` call).

Last commit of Phase 3: `22f2a19` (fix: enqueue newly generated scripture slides for rendering).

### Phase 4a: IN PROGRESS — Task 1 done, Task 2 done but UNCOMMITTED, Task 3 not started

Plan: `docs/superpowers/plans/2026-07-26-scripture-parser-phase4a.md` — makes `ScriptureItem` a real persistable library item type (no editor UI yet, that's Phase 4b, not yet planned).

- **Task 1 — DONE, committed (`2dd77b3`), reviewed, Approved.** Fixed `CreateItem.GenerateItem`'s `.xml` dispatch, which used to hardcode `SongItem` for every `.xml` file (a real, pre-existing bug — would have silently mis-parsed any `ScriptureItem` XML). Now peeks the root element name. Added `InternalsVisibleTo` so the (internal) `CreateItem` class is testable for the first time. 4 new tests, all passing, backward-compat to Song loading independently re-verified by the task reviewer.

- **Task 2 — CODE COMPLETE, TESTS PASSING, BUT NOT YET COMMITTED.** ⚠️ **Do this first when resuming.** The implementer subagent finished this task (added `LibraryType.Scripture`, `ScriptureLibrary : Library`, wired `LibraryViewModel.ReloadLibraries()`'s dispatch, added 3 passing tests) but the session was interrupted mid-flow before the commit step and before task review. Verified directly (2026-07-26, this session): all changes present on disk, match the plan's Task 2 spec exactly, and `dotnet test` shows 129/129 passing (126 baseline + 3 new). **Uncommitted files right now:**
  - Modified: `HandsLiftedApp.Core/Models/Library/Config/LibraryConfig.cs` (added `Scripture` to `LibraryType` enum)
  - Modified: `HandsLiftedApp.Core/ViewModels/LibraryViewModel.cs` (dispatch switch in `ReloadLibraries()`)
  - New: `HandsLiftedApp.Core/Models/Library/ScriptureLibrary.cs`
  - New: `HandsLiftedApp.Tests/Models/Library/ScriptureLibraryTests.cs`

  **Next steps for Task 2:** run the task-reviewer dispatch (per the `subagent-driven-development` skill — read `p4a-task-2-brief.md` below, generate a review package, dispatch a reviewer) *before* committing, since it hasn't been reviewed yet. If review comes back clean, commit with message `feat: add ScriptureLibrary and LibraryType.Scripture` (matches the plan's Step 8). If the reviewer finds issues, fix then commit.

- **Task 3 — NOT STARTED.** `ItemInstanceFactory` branch for `ScriptureItem → ScriptureItemInstance` + an end-to-end round-trip test (write `ScriptureItem` XML → `CreateItem.GenerateItem` → `ItemInstanceFactory.ToItemInstance` → assert fields match). Fully specified in the plan, ready to extract a brief and dispatch.

- **Phase 4a final whole-branch review — NOT STARTED.** Do after Task 3.

### Scratch files already extracted (reusable, in `.superpowers/sdd/`)

- `p4a-task-1-brief.md`, `p4a-task-2-brief.md` — already extracted task briefs (Task 1's work is done; Task 2's brief is still valid for the review step since the code matches it).
- Various `review-*.diff` files from past reviews — historical, safe to ignore or regenerate.
- To extract Task 3's brief: `bash "path/to/subagent-driven-development/scripts/task-brief" docs/superpowers/plans/2026-07-26-scripture-parser-phase4a.md 3 .superpowers/sdd/p4a-task-3-brief.md` (find the skill's scripts dir via the plugin cache, e.g. `~/.claude/plugins/cache/claude-plugins-official/superpowers/*/skills/subagent-driven-development/scripts/`).

## Key deliberate decisions / deviations to know about (don't "fix" these — they're intentional)

- **Splitting rule is one-verse-per-slide**, even though the parser (Phase 1) preserves paragraph grouping and a mid-session suggestion floated one-paragraph-per-slide instead — user explicitly reconfirmed one-verse-per-slide overrides that. The `IsVerseContinuation` flag Phase 1 built exists specifically so Phase 2's extractor can *merge* a verse's text back together when split across paragraphs, not to group multiple verses onto one slide.
- **`ScriptureItem` does NOT cache parsed verse content** (deviates from the original design spec) — avoids inverting the `Data → Importer.Scripture` layering; Phase 1's loader already caches raw USX in memory + on disk, so re-fetching is already fast.
- **No per-item theme/Design property on `ScriptureItem` yet** — every scripture slide uses the app's default theme (`Globals.Instance.AppPreferences?.DefaultTheme`). Add a real picker only when actually requested.
- **No fuzzy search for `ScriptureLibrary`** (unlike `SongLibrary`'s lyric-text index) — base `Library.Search`'s title-substring match is enough for now.
- **No "add library" UI** — none exists for any library type today; a Scripture library is configured by hand-editing `library.yml` and clicking Reload, same as everything else.
- **`ItemInstanceFactory.ToItemInstance` stays synchronous**, calling `ScriptureItemInstance.GenerateSlidesAsync()` fire-and-forget — consistent with the existing (if inconsistent) codebase convention (the `SongItem` branch's own `GenerateSlides()` call is commented out).

## Not yet planned

**Phase 4b** — the actual editor UI (passage-entry window, "Add Scripture" library button, save/unsaved-changes flow). No plan file exists yet for this. When picking it up: read `SongEditorWindow.axaml.cs`/`SongEditorViewModel.cs`/`NewSongUnsavedConfirmationWindow` in full first (the template to mirror) — a prior research pass already covered the key mechanics (save-to-library path, `IsNewSongMode` pattern, `AddItem_OnClick` gating) but did not read every file end-to-end; there is no existing passage/reference-entry UI precedent anywhere in the codebase to borrow from, so that control needs to be designed from scratch.

## How to resume

1. Read `.superpowers/sdd/progress.md` in full — it's the ground truth.
2. `cd` into this worktree (not the main checkout).
3. Verify current state: `git status --short` and `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo` (expect 129/129 with Task 2's uncommitted changes present).
4. Invoke `superpowers:subagent-driven-development` skill, resume at Phase 4a Task 2's review step (code is done, just needs review + commit), then Task 3, then Phase 4a's final whole-branch review.
5. After Phase 4a is merged/committed, brainstorm + plan Phase 4b (editor UI) following the same pattern as prior phases — brainstorm scope questions with the user first (this session repeatedly found real scope/design gaps by asking before planning, e.g. the splitting-rule reconfirmation and the narrow-vs-wide Phase 2 scope choice).
