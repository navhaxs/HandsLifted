# Song Lyric Reflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Reflow dropdown in the song lyric editor that rewrites the blank-line slide breaks of every stanza: Smart, or fixed 2/3/4 lines per slide.

**Architecture:** A pure static `LyricReflow` (no Avalonia/Skia) takes a stanza's lyric string and a `ReflowMode` and returns the reflowed string. `SongEditorViewModel.ReflowLyrics(mode)` applies it to every stanza inside one undo step. A flyout button in `SongLyricBlockEditor` calls the view-model.

**Tech Stack:** C# / .NET 10, Avalonia 12.1.1, ReactiveUI, xUnit (`HandsLiftedApp.Tests`, `HandsLiftedApp.UiTests`).

**Spec:** `docs/superpowers/specs/2026-11-23-song-lyric-reflow-design.md`

## Global Constraints

- Slides split on a blank line inside `SongStanza.Lyrics`; reflow changes ONLY blank lines, never lyric text.
- Overwrites existing blank-line splits in ALL modes, including Smart.
- Empty stanzas (interludes) are untouched; reflow is idempotent.
- NO splitting inside lines, no capitalisation changes (deferred by decision).
- No Skia/Avalonia dependency in `LyricReflow`.
- Whole-song scope; exactly one undo step (`BeginUndoStep("Reflow lyrics")`).
- Tooling: no `find`/`grep` shellouts; use Glob/Grep. Run tests with `dotnet test` (dotnet PATH workaround per CLAUDE.md). Do not edit anything under `.claude/worktrees/`.
- Flyout `MenuItem` handlers must be clicked through in a headless test (CLAUDE.md flyout trap).

## Review Focus

- Windows line endings (`\r\n`) in stanza lyrics: output must not contain stray `\r`, text preserved.
- Whitespace-only "blank" lines and trailing/leading blank lines: treated as blank, not as lines.
- Stanza with 0 or 1 non-blank lines: returned unchanged-ish (no break added, no crash).
- Very long stanzas (e.g. 20 lines): every chunk 2-4 lines, no orphans.
- Lines with no letters (e.g. "...", "—") or empty last word: rhyme check must not throw.
- Running Reflow twice yields the same text (idempotent), in every mode.
- Undo after reflow restores the original lyrics for all stanzas in a single step.

---

### Task 1: LyricReflow core + fixed modes

**Files:**
- Create: `HandsLiftedApp.Core/Models/Library/LyricReflow.cs` (namespace `HandsLiftedApp.Core.Models.Library`, same folder as `LibrarySearch.cs`)
- Test: `HandsLiftedApp.Tests/Models/Library/LyricReflowTests.cs`

**Interfaces:**
- Produces:
  - `public readonly record struct ReflowMode(int LinesPerSlide)` with `public static ReflowMode Smart { get; }` (LinesPerSlide 0) and `public static ReflowMode Fixed(int n)` (throws `ArgumentOutOfRangeException` unless 2 <= n <= 4).
  - `public static class LyricReflow` with `public static string Reflow(string? lyrics, ReflowMode mode)` and `internal static List<string> NormalisedLines(string? lyrics)` (trimmed, non-blank, `\r\n` handled).
  - Output format: chunk lines joined with `\n`, chunks joined with `\n\n`, no trailing newline. Null/empty/whitespace input returns `""`.

- [ ] **Step 1: Write failing tests** in `LyricReflowTests`, a helper `Lines(int n)` builds `"L1\nL2\n..."`. Tests (names exact):
  - `Fixed2_6Lines_ThreeChunksOfTwo`: result splits on `"\n\n"` into sizes `[2,2,2]`.
  - `Fixed3_7Lines_RebalancesToAvoidOrphan`: sizes `[3,2,2]`.
  - `Fixed2_5Lines_Is2_3`: sizes `[2,3]` (last two rebalanced, so `[2,2,1]` never occurs).
  - `Fixed4_8Lines_Is4_4`; `Fixed3_3Lines_StaysWhole`; `Fixed4_2Lines_StaysWhole`.
  - `Fixed2_OverwritesExistingBreaks`: input `"a\nb\nc\nd\n\ne\nf"` gives sizes `[2,2,2]`.
  - `HandlesCrLfAndWhitespaceBlanks`: `"a\r\nb\r\n  \r\n\r\nc\r\nd\r\n"` with Fixed2 gives `"a\nb\n\nc\nd"` and contains no `\r`.
  - `EmptyOrNull_ReturnsEmpty` (null, `""`, `"  \n \n"`).
  - `TextPreserved`: joining all non-blank output lines equals joining the input's trimmed non-blank lines.
  - `Idempotent_Fixed`: `Reflow(Reflow(x,m),m) == Reflow(x,m)` for 1..12 lines, modes 2/3/4.
  - `Fixed_InvalidN_Throws`: `Fixed(1)` and `Fixed(5)`.
- [ ] **Step 2: Run** `dotnet test HandsLiftedApp.Tests --filter LyricReflowTests`. Expected: compile FAIL (types missing).
- [ ] **Step 3: Implement** `ReflowMode`, `NormalisedLines`, and the fixed-mode chunker. Rule: stanza with `<= n` lines stays whole; else take chunks of `n`; if the remainder is exactly 1, redistribute the last two chunks as evenly as possible (larger first). Smart mode can throw `NotImplementedException` for now.
- [ ] **Step 4: Run tests.** Expected: all PASS.
- [ ] **Step 5: Commit** `feat: LyricReflow fixed lines-per-slide modes`.

### Task 2: Smart mode

**Files:**
- Modify: `HandsLiftedApp.Core/Models/Library/LyricReflow.cs`
- Test: `HandsLiftedApp.Tests/Models/Library/LyricReflowTests.cs` (add) and `HandsLiftedApp.Tests/Models/Library/LyricReflowLibraryTests.cs` (create)

**Interfaces:**
- Consumes: `NormalisedLines`, `ReflowMode.Smart` from Task 1.
- Produces: `Reflow(x, ReflowMode.Smart)` implemented; `internal static bool Rhymes(string lineA, string lineB)`; weights as `private const double` in `LyricReflow` (starting values below, tune in Step 6).

Algorithm (stanza of 1-4 normalised lines stays whole): dynamic programming over break positions; each chunk is 2-4 lines (a 1-line chunk is never allowed); cost to minimise = sum over chunks of `ChunkCost = 1.5` + `FourLineCost = 0.25` per 4-line chunk + `UnevenCost = 0.5` per adjacent chunk pair with different sizes - `RhymeBonus = 1.0` for each break that falls directly after a rhyming pair (line i-1 and i rhyme, where i is the last line of the chunk). Ties prefer the earlier chunk being larger (so 5 gives 3+2, 7 gives 4+3). `Rhymes`: compare the last word of each line lowercased with non-letters stripped; true if both are non-empty and the final 2 letters match (words shorter than 2 letters: compare whole word); false if either has no letters (must not throw).

- [ ] **Step 1: Write failing tests.** `Smart_6Lines_Is3_3`, `Smart_8Lines_Is4_4`, `Smart_4Lines_StaysWhole`, `Smart_3Lines_StaysWhole`, `Smart_1Line_StaysWhole`, `Smart_9Lines_Is3_3_3`, `Smart_7Lines_Is4_3`, `Smart_20Lines_AllChunks2To4` (assert every chunk size in 2..4), `Smart_NeverOrphansOneLine` for 5..16 lines, `Smart_5Lines_RhymeDecidesSplit` (lines `"x light","x night","x sight","y hand","y stand"` assert sizes `[3,2]`; mirror `"y hand","y stand","x light","x night","x sight"` asserts `[2,3]`), `Smart_Idempotent` for 1..12 lines, `Rhymes_Basics` (`"light","night"` true; `"light","hand"` false; `"...","night"` false; `""` false), `Smart_TextPreserved`, `Smart_OverwritesExistingBreaks`.
- [ ] **Step 2: Run** `--filter LyricReflowTests`. Expected: new tests FAIL (`NotImplementedException`).
- [ ] **Step 3: Implement** Smart mode and `Rhymes` as described.
- [ ] **Step 4: Run tests.** Expected: PASS.
- [ ] **Step 5: Library eyeball test.** `LyricReflowLibraryTests`: if `V:\Songs` exists (skip otherwise, `Assert.Skip`/early return), load each song XML's stanza `<Lyrics>`, run Smart, assert invariants (text preserved, chunks 2-4 lines or whole stanza <= 4, idempotent) and write all before/after pairs to `LyricReflowLibraryTests.out.txt` in the test output directory.
- [ ] **Step 6: Tune.** Read the output file. Check: every 6-line stanza is 3+3, 8-line is 4+4, 5-line prefers a rhyme-aligned split, nothing odd. Adjust the named constants only; keep tests green. Note in a code comment what the constants mean.
- [ ] **Step 7: Commit** `feat: smart lyric reflow`.

### Task 3: Editor wiring + UI

**Files:**
- Modify: `HandsLiftedApp.Core/ViewModels/Editor/SongEditorViewModel.cs`
- Modify: `HandsLiftedApp.Core/Views/Editors/SongLyricBlockEditor.axaml` (header area near the "Rearrange Stanzas" `Button` at ~line 84) and `SongLyricBlockEditor.axaml.cs`
- Test: `HandsLiftedApp.Tests/ViewModels/SongEditorViewModelReflowTests.cs` (create; follow the folder/namespace of existing view-model tests; run inside `DispatcherTestThread.Run(...)` per CLAUDE.md) and `HandsLiftedApp.UiTests/SongReflowUiTests.cs`

**Interfaces:**
- Consumes: `LyricReflow.Reflow`, `ReflowMode` from Tasks 1-2.
- Produces: `public void ReflowLyrics(ReflowMode mode)` on `SongEditorViewModel`: inside `using var step = BeginUndoStep("Reflow lyrics");` set `stanza.Lyrics = LyricReflow.Reflow(stanza.Lyrics, mode)` for each stanza, only when the new value differs (skips empty stanzas and avoids no-op edits). Code-behind handler `Reflow_OnClick(object?, RoutedEventArgs)` reading the mode from the clicked item's `Tag` ("smart", "2", "3", "4").

- [ ] **Step 1: Write failing view-model tests.** `ReflowLyrics_Smart_RewritesAllStanzas`; `ReflowLyrics_Fixed2_OverwritesExisting`; `ReflowLyrics_EmptyStanzaUntouched`; `ReflowLyrics_IsOneUndoStep_UndoRestoresAll` (build a song with 3 stanzas, `UndoStack.Clear()` after setup per CLAUDE.md, reflow, call undo once, assert all three lyrics equal the originals); `ReflowLyrics_NoChange_RecordsNoUndoStep`.
- [ ] **Step 2: Run** the new tests. Expected: FAIL (method missing).
- [ ] **Step 3: Implement `ReflowLyrics`** in `SongEditorViewModel`.
- [ ] **Step 4: Run view-model tests.** Expected: PASS.
- [ ] **Step 5: Add the UI.** In the header `StackPanel`, add a `Button` (text "Reflow", material icon of your choice) with a `MenuFlyout` of four `MenuItem`s: "Smart", "2 lines per slide", "3 lines per slide", "4 lines per slide", each with `Tag` and `Click="Reflow_OnClick"`. Handler needs no Window lookup, so the flyout-owner trap does not apply to the handler, but the click is still verified in Step 6. Add a tooltip: "Re-split lyrics into slides (replaces existing breaks)".
- [ ] **Step 6: Headless UI test** `Reflow_Flyout_Smart_RewritesLyrics_AndUndoRestores`: open the song editor window with a song of one unbroken 6-line stanza (follow `UiTestHelpers` / `MessageWindowTests` patterns), find the "Reflow" button, `FindFlyoutMenuItem("Smart")`, `ClickInPopup()`, assert the stanza lyrics become 3+3 and `Screenshot` it; then undo via the editor's undo (`UndoStack`) and assert original restored. Run `dotnet test HandsLiftedApp.UiTests --filter SongReflowUiTests`. Read the screenshot in `HandsLiftedApp.UiTests/bin/Debug/net10.0/screenshots/`.
- [ ] **Step 7: Full build and tests.** `dotnet build` (full, non-incremental per CLAUDE.md) then `dotnet test HandsLiftedApp.Tests` and `dotnet test HandsLiftedApp.UiTests`. Expected: all PASS.
- [ ] **Step 8: Click through the real app** once: `tools\devtools.ps1 start`, open a song in the editor, use Reflow (Smart and 2 lines), check slides/thumbnails update, then `tools\devtools.ps1 stop`.
- [ ] **Step 9: Commit** `feat: song editor reflow lyrics button`.
