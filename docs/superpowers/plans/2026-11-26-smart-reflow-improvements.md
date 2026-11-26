# Smart Lyric Reflow Improvements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Smart reflow groups lyrics by phrase structure (couplets/repeat period), predicts autofit font scale, and splits over-wide lines so no slide shrinks abruptly.

**Architecture:** `LyricReflow.Reflow` gains an optional `ReflowMetrics` (width measurer + canvas limits, same maths as `SongSlideSpecBuilder.ComputeAutofitSize`). Smart mode becomes: split over-wide *logical* lines into 1-2 physical lines, then a DP groups logical lines into slides (max 4 physical lines), with cost = existing terms + predicted-shrink penalty + phrase-period bonus. Phrase similarity is computed on the original (logical) lines.

**Tech Stack:** C# / .NET 10, MSTest (`HandsLiftedApp.Tests`), SkiaSharp (theme measurer only).

**Spec:** `docs/superpowers/specs/2026-11-26-smart-reflow-improvements-design.md`

## Global Constraints

- Work on `master` in place (user's choice). Do not commit unless the user asks; leave changes uncommitted.
- Fixed 2/3/4 lines-per-slide modes: behaviour unchanged (existing `Fixed*` tests stay green).
- Words never change. The only text edit allowed: the first letter of a split-off line is upper-cased (leading punctuation such as `(` or `"` skipped); nothing else touched.
- Max 4 physical lines per slide; min 2 physical lines per slide when the stanza has more than 3 physical lines; never a 1-line orphan.
- Autofit maths to mirror: canvas 1920x1080, margins 80 => max text width 1760, max height 920; line height = `FontSize * LineHeightEm`; floor ratio `AutofitMinFontSizeRatio` (default 0.5). Theme defaults: `FontSize` 100, `LineHeightEm` 1.2.
- `LyricReflow.SlideCharCap` (140) and its density cost are **kept** (deviation from the spec's "replaces": the existing `Dense*` tests pin it, and it covers wall-of-text, which scale alone does not). The new scale penalty is added on top.
- No `find`/`grep` shell-outs; never edit under `.claude/worktrees/`.
- Run tests: `tools\test.ps1 -Filter LyricReflow` (script reduces output to pass/fail + failing tests).

## Review Focus

1. Line with no spaces, or only 2 words, that is over-wide: left whole (cannot split), no exception, no infinite loop.
2. Split-off line starting with punctuation (`"and I will`, `(and then)`): the first *letter* is capitalised, punctuation kept.
3. Stanza of 1-3 logical lines containing a split line (more than 4 physical lines total): must still be grouped into legal slides, not returned as one 5+ line slide.
4. Lyrics already lowercase/ALL CAPS or lacking punctuation: split points still found (connector or middle), no crash on empty halves.
5. Re-running Smart on its own output: words preserved and no line is split a second time (exact idempotency is best-effort, see Task 3).

---

### Task 1: ReflowMetrics and predicted font scale

**Files:**
- Create: `HandsLiftedApp.Core/Models/Library/ReflowMetrics.cs`
- Modify: `HandsLiftedApp.Core/Models/Library/LyricReflow.cs` (signature, cost)
- Modify: `HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs` (expose limits/typeface as `internal`)
- Test: `HandsLiftedApp.Tests/Models/Library/LyricReflowTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record ReflowMetrics(Func<string,double> MeasureWidth, double MaxWidth, double MaxHeight, double LineHeight, double MinScale)` in namespace `HandsLiftedApp.Core.Models.Library`.
  - `ReflowMetrics.Default` (static): fallback for no theme = width `chars * 50.0` (0.5em at FontSize 100), MaxWidth 1760, MaxHeight 920, LineHeight 120, MinScale 0.5.
  - `ReflowMetrics.Unconstrained` (static): `MaxWidth = double.MaxValue`, otherwise like Default. For tests that isolate phrasing.
  - `ReflowMetrics.FromTheme(BaseSlideTheme theme)`: MeasureWidth uses an `SKFont` at `theme.FontSize` built from `SongSlideSpecBuilder.GetTypeface(theme)`; limits from `SongSlideSpecBuilder` (now `internal const MaxTextWidth = 1760f`, `MaxTextHeight = 920f`; `GetTypeface` becomes `internal static`); LineHeight = `theme.FontSize * (double)theme.LineHeightEm`; MinScale = `(double)theme.AutofitMinFontSizeRatio`; if `!theme.AutofitEnabled` returns `Default` (no shrink to predict).
  - `public static string LyricReflow.Reflow(string? lyrics, ReflowMode mode, ReflowMetrics? metrics = null)` (null => `ReflowMetrics.Default`).
  - `internal static double LyricReflow.PredictedScale(IReadOnlyList<string> slideLines, ReflowMetrics m)` = `Math.Max(m.MinScale, Math.Min(1.0, Math.Min(m.MaxWidth / widestLinePx, m.MaxHeight / (slideLines.Count * m.LineHeight))))`; an empty list or zero width => 1.0.

- [ ] **Step 1: Write failing tests** in `LyricReflowTests`:
  - `PredictedScale_WideLine_ShrinksByWidthRatio`: metrics `new ReflowMetrics(l => l.Length * 100.0, 1000, 920, 120, 0.5)`; a 20-char line => 0.5 (2000px vs 1000, clamped at MinScale 0.5); a 12-char line => `1000/1200` (assert within 1e-9); a 5-char line => 1.0.
  - `PredictedScale_TallSlide_ShrinksByHeightRatio`: 10 one-char lines, height 920, lineHeight 120 => `920/1200`.
  - `PredictedScale_NeverBelowMinScale`: 100-char line => 0.5.
  - `Reflow_Fixed_IgnoresMetrics`: `Reflow(Lines(6), ReflowMode.Fixed(2), ReflowMetrics.Default)` still sizes 2,2,2.
- [ ] **Step 2: Run** `tools\test.ps1 -Filter LyricReflow`. Expected: compile FAIL (`ReflowMetrics` undefined).
- [ ] **Step 3: Implement** `ReflowMetrics`, the new overload and `PredictedScale`; change `SongSlideSpecBuilder` members to `internal` as listed (keep behaviour). Add to the Smart DP cost: `+ ShrinkWeight * (1 - PredictedScale(slideLines))` per slide, plus `ScaleJumpWeight * |scale - previousScale|` between adjacent slides (add the previous slide's scale to the memo key, or track it by `last` size + start; builder's choice, memo must stay correct). Constants `ShrinkWeight = 6.0`, `ScaleJumpWeight = 4.0` as starting values.
- [ ] **Step 4: Run** `tools\test.ps1 -Filter LyricReflow`. Expected: ALL existing tests (including `Dense*`, `Rhyme*`, `RepeatedOpening`, idempotency) PASS plus the new ones. If an existing test fails, retune the two weights (not the tests) until all pass.

### Task 2: Phrase similarity and repeat-period detection

**Files:**
- Modify: `HandsLiftedApp.Core/Models/Library/LyricReflow.cs` (replace `OpeningKey`/`opensRepeat`/`RepeatOpenBonus`)
- Test: `HandsLiftedApp.Tests/Models/Library/LyricReflowTests.cs`

**Interfaces:**
- Consumes: `Reflow(lyrics, mode, metrics)`, `ReflowMetrics.Unconstrained` (Task 1).
- Produces:
  - `internal static double LyricReflow.Similarity(string a, string b)`: 0..1. Words lower-cased, letters only, contractions split at the apostrophe (`you've` -> `you`, `ve`; drop 1-2 letter fragments after the apostrophe), then `max(openingScore, endingScore)` where openingScore = shared leading words / min(word counts), capped at 1, and 1.0 for an exact (normalised) repeat; endingScore = 1.0 if last words equal, 0.6 if `Rhymes`.
  - `internal static int LyricReflow.DetectPeriod(IReadOnlyList<string> lines)`: returns 2 or 3, or 0 if none. For p in {2,3}: `score(p)` = mean of `Similarity(lines[i], lines[i+p])` over valid i; choose the larger score; require `score >= PeriodMinScore` (start 0.45) and `lines.Count >= 2*p`; ties prefer 2.
  - The DP gives `PeriodBreakBonus` (start 1.0) to a slide break after line index `k` where `(k+1) % period == 0` (and k+1 < n), replacing the old `RepeatOpenBonus`. Rhyme bonus stays. A break that would separate a line from the earlier line it repeats **inside the same period group** gets no bonus (that was the Verse 2 bug).

- [ ] **Step 1: Write failing tests** (all use `ReflowMetrics.Unconstrained` so no splitting interferes):
  - `Similarity_ContractionsMatch`: `Similarity("If you've been forgiven and if you've been redeemed", "If you walk in freedom and if you bear His name") >= 0.5`.
  - `Similarity_ExactRepeat_IsOne`: `"Sing the song forever to the Lamb"` vs itself with different case/punctuation => 1.0.
  - `Similarity_Unrelated_IsLow`: `"Hear Your people sing Holy"` vs `"Your word is a lamp"` < 0.3.
  - `DetectPeriod_HolyForeverVerse2_Is2` (the five Verse 2 lines).
  - `DetectPeriod_HolyForeverVerse1_Is3` (lines: `A thousand generations falling down in worship` / `To sing the song of ages to the Lamb` / `And all who've gone before us` / `And all who will believe` / `Will sing the song of ages to the Lamb`).
  - `Smart_HolyForeverVerse2_Is2_3`: the five lines, `AssertSizes(..., 2, 3)`.
  - `Smart_HolyForeverVerse1_Is3_2`: `AssertSizes(..., 3, 2)`.
  - `Smart_HolyForeverPreChorus_Is3_3`: lines `Your name is the highest / Your name is the greatest / Your name stands above them all / All thrones and dominions / All powers and positions / Your name stands above them all` => 3,3.
- [ ] **Step 2: Run** `tools\test.ps1 -Filter LyricReflow`. Expected: FAIL (new members undefined / Verse 2 gives 3,2).
- [ ] **Step 3: Implement** `Similarity`, `DetectPeriod`, the period bonus; delete `OpeningKey`, `RepeatOpenWords`, `RepeatOpenBonus`, `opensRepeat`.
- [ ] **Step 4: Run** `tools\test.ps1 -Filter LyricReflow`. Expected: all PASS, including the old `Smart_RepeatedOpening_StartsNewSlide` (4,2) and `Smart_5Lines_RhymeDecidesSplit`. If an old test regresses, tune `PeriodBreakBonus`/`PeriodMinScore`/similarity weights; do not edit old expectations.

### Task 3: Per-line splitting and logical-line grouping

**Files:**
- Modify: `HandsLiftedApp.Core/Models/Library/LyricReflow.cs`
- Test: `HandsLiftedApp.Tests/Models/Library/LyricReflowTests.cs`

**Interfaces:**
- Consumes: `ReflowMetrics`, `PredictedScale` (Task 1); `Similarity`, `DetectPeriod` (Task 2).
- Produces:
  - `internal static IReadOnlyList<string> LyricReflow.SplitLongLine(string line, ReflowMetrics m)`: returns the line unchanged (1 element) or 2 lines. Split only when `PredictedScale([line]) < SplitBelowScale` (start 0.85) **and** the best candidate's worse-half scale beats the unsplit scale by at least `MinSplitGain` (start 0.1). Candidates: every inter-word position leaving >= 2 words each side (>= 1 word allowed only if the lone word has >= 4 letters... keep simple: >= 2 words each side). Score: comma/semicolon/dash at the end of the left half +3; right half starting with a connector from `{and, but, or, for, so, who, whom, that, which, when, where, while, as, to, in, with, from, by}` (case-insensitive) +2; closeness to the middle by width `-(|leftPx - rightPx| / totalPx)*2`. Highest score wins; ties take the earlier position. The right half gets its first letter upper-cased (`char.ToUpperInvariant` on the first `char.IsLetter` character; leading punctuation kept).
  - Smart pipeline in `Reflow`: normalised lines -> `logical = lines` -> `phys[i] = SplitLongLine(lines[i], m)`; similarity/period/rhyme computed on `logical` text; DP items are logical lines, a slide takes consecutive items whose physical total is 2..4 (1 logical item with 2 physical lines is a legal slide); scale/char-cap cost uses the slide's *physical* lines; output joins physical lines.
  - Early-return "stay whole" rule becomes: physical total <= 4 and (for exactly 4 physical lines) chars <= `SlideCharCap`, and logical count <= 3 or the old condition; otherwise DP.

- [ ] **Step 1: Write failing tests**:
  - `SplitLongLine_Verse2Line1`: `SplitLongLine("If you've been forgiven and if you've been redeemed", ReflowMetrics.Default)` equals `["If you've been forgiven", "And if you've been redeemed"]`.
  - `SplitLongLine_PrefersComma`: `"Oh Lord, I will praise You for all of my days"` returns `["Oh Lord,", "I will praise You for all of my days"]` (left half ends with `,`).
  - `SplitLongLine_ShortLine_Unchanged`: `"Holy forever"` => 1 element, same text.
  - `SplitLongLine_NoSpaces_OrTwoWords_Unchanged`: a 60-char single token, and `"Alleluiaaaaaaaaaaaaaaaaaaaaaaaaaaaa Hallelujahhhhhhhhhhhhhhhhhhhhhhhhhhh"` => unchanged.
  - `SplitLongLine_CapitalisesFirstLetterAfterPunctuation`: line `all of creation sings out loud (and the angels join the song forever)` => the right half's first letter character is upper case and the text after it is unchanged (e.g. `(And the angels ...`).
  - `SplitLongLine_AllLowerNoPunct_StillSplits`: 55-char lowercase sentence with `and` in the middle splits there and capitalises `And`.
  - `Smart_HolyForeverVerse2_EndToEnd`: with default metrics expect exactly  
    `"If you've been forgiven\nAnd if you've been redeemed\nSing the song forever to the Lamb\n\nIf you walk in freedom\nAnd if you bear His name\nSing the song forever to the Lamb\nWe'll sing the song forever and amen"`  
    (if cost tuning makes line 3 stay unsplit, that is acceptable only if the line fits; assert instead that no slide's `PredictedScale` is below 0.8 and the break is after the second logical line).
  - `Smart_SplitLine_NeverSeparatedFromItsPartner`: for each Verse 2 variant, the two halves of a split line are in the same slide (no `\n\n` between them).
  - `Smart_FewLogicalLines_WithSplit_StillLegalSlides` (Review Focus 3): 3 logical over-wide lines (each 60 chars, several spaces) => every slide has 2..4 physical lines.
  - `Smart_SplitOutput_Rerun_PreservesWords_AndDoesNotSplitAgain`: run twice on Verse 2; word sequences (case-insensitive, whitespace-normalised) equal input's; the second run's physical line count equals the first's.
  - Existing `Smart_*` tests must remain green unchanged.
- [ ] **Step 2: Run** `tools\test.ps1 -Filter LyricReflow`. Expected: FAIL (`SplitLongLine` undefined).
- [ ] **Step 3: Implement** the splitter and the logical-line DP as specified; keep `FixedSizes` and the fixed path untouched.
- [ ] **Step 4: Run** `tools\test.ps1 -Filter LyricReflow`. Expected: all PASS. If an old test breaks because short test lines now split, raise `SplitBelowScale` constraints only if the old line really fits; do not edit old expectations.

### Task 4: Theme-aware wiring and real-library verification

**Files:**
- Modify: `HandsLiftedApp.Core/ViewModels/Editor/SongEditorViewModel.cs:137-148` (`ReflowLyrics`)
- Modify: `HandsLiftedApp.Tests/Models/Library/LyricReflowLibraryTests.cs`

**Interfaces:**
- Consumes: `ReflowMetrics.FromTheme(BaseSlideTheme)`, `Reflow(lyrics, mode, metrics)`.

- [ ] **Step 1: Wire the VM.** In `ReflowLyrics(ReflowMode mode)` build `var metrics = SelectedSlideTheme != null ? ReflowMetrics.FromTheme(SelectedSlideTheme) : ReflowMetrics.Default;` once, and pass it to every `LyricReflow.Reflow` call. Dispose the `SKFont`/typeface used by `FromTheme` (create per measure call or hold in a disposable the VM disposes; builder's choice, no leaks - see CLAUDE.md typeface notes).
- [ ] **Step 2: Update the library test.** In `Smart_OverRealLibrary_HoldsInvariants_AndDumpsOutput`: compare words only (lower-case, split on whitespace) instead of line equality; replace the idempotency assert with "second run keeps the same words and the same physical line count"; chunk-size assert applies to physical lines (2..4 when the stanza has more than 3 physical lines). Add to the report header the count of split lines and the count of slides with `PredictedScale < 0.8`. The test still returns early when `V:\Songs` is absent.
- [ ] **Step 3: Run** `tools\test.ps1 -Filter LyricReflow`. Expected: all PASS.
- [ ] **Step 4: Read the dump** `HandsLiftedApp.Tests/bin/Debug/net10.0/LyricReflowLibraryTests.out.txt`. Check: Holy Forever / Verse 2 matches the Task 3 end-to-end expectation; the SUMMARY shows few slides under scale 0.8; skim at least 10 other songs for regressions (a broken couplet, a silly split point, a split in an already-fine song). Report the SUMMARY and any suspicious songs to the user; tune constants only for clear regressions.
- [ ] **Step 5: Full check.** `tools\test.ps1` (both projects). Expected: pass/fail line shows no new failures.

---

## Self-review notes

- Spec coverage: item 1 -> Task 2; item 2 -> Task 1 (+VM in Task 4); item 3 -> Task 3; capitalisation -> Task 3; verification/risks -> Tasks 3-4. `SlideCharCap` retained is a flagged deviation.
- Types consistent: `ReflowMetrics`, `PredictedScale`, `Similarity`, `DetectPeriod`, `SplitLongLine` signatures are used identically across tasks.
