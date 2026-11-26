# Smart lyric reflow improvements - design

Date: 2026-11-26. Status: approved by user in chat. Edits made on `master` (user's choice).

## Intent and why

Smart reflow (`HandsLiftedApp.Core/Models/Library/LyricReflow.cs`) mostly breaks lyrics well, but some slides
carry a long line or too many lines, so autofit (`SongSlideSpecBuilder.ComputeAutofitSize`) shrinks the font
and that slide looks abruptly smaller than its neighbours. Example: Holy Forever, Verse 2, first slide.
The same stanza should also split 2+3 (couplets: lines 1-2, 3-4, tag line 5), not 3+2.

## Why it happens today

- Autofit shrinks until the widest *raw line* fits and the line count fits in height. It never wraps first.
  Smart cost uses total characters (`SlideCharCap` = 140), which is a poor proxy.
- `RepeatOpenBonus` rewards a break *before* a repeated line, which can split a couplet. `OpeningKey` needs
  3 matching words after stripping apostrophes (`you've` -> `youve`), so "If you've been..." / "If you walk..."
  never match.
- Stanzas are reflowed independently.

## Goals

- Verse 2 of Holy Forever reflows to 2+3.
- Fewer slides whose predicted font scale is well below their neighbours'.
- Long lines are split at natural points.

## Non-goals / anti-goals

- Fixed 2/3/4 lines-per-slide modes unchanged.
- No song-wide re-balance pass (deferred).
- No renderer change (no wrap-before-shrink).
- Must not change the words of a lyric; only line breaks and the capitalised first letter of a split-off line.

## Approaches considered

A. Tune weights only. Cheap, but cannot fix a single over-wide line.
B. Phrase signals + font-scale cost + per-line splitting (chosen by user).
C. B plus a song-wide re-balance pass. Deferred.

## Design (B)

1. **Phrase signals.** Graded line-to-line similarity: shared opening words (contractions split, `you've` -> `you`),
   same ending word or rhyme, exact repeat. Detect repeat period 2 or 3; bonus for breaks on period multiples,
   remainder joins the last slide. Drop the break-before-repeat bonus when it would split a couplet.
2. **Cost from predicted font scale.** Scale = `min(1, maxWidth / widestLine, maxHeight / (lines * lineHeight))`.
   Cost penalises shrink plus a penalty for adjacent slides of differing scale; replaces `SlideCharCap`.
   `Reflow(lyrics, mode, measurer?)` takes an optional width measurer. `SongEditorViewModel.ReflowLyrics`
   supplies one built from the selected theme font. No measurer (tests, no theme) = character-width estimate.
3. **Per-line splitting** before grouping. A line that does not fit at full size is split. Candidates scored:
   comma/semicolon best, then just before a connector (and, but, who, that, to, for...), then nearest the middle;
   each half keeps >= 2 words; skip a split that does not improve fit. First letter of each new line is
   capitalised, rest untouched. Grouping runs on the resulting physical lines, max 4 physical lines per slide.

## Calls made by the designer (overrule if wrong)

- Invariant "never changes lyric text" becomes "never changes words"; tests compare words only.
- Idempotency is a risk: a second run sees split pieces as plain lines. Keep the library idempotency
  check if achievable; otherwise relax to "same words, split pieces stay on one slide".
- Verification: unit tests (Verse 2 = 2+3, Verse 1 / Pre-Chorus shapes, splitting, capitalisation,
  measurer fallback), rerun `LyricReflowLibraryTests` dump over `V:\Songs`, read before/after for regressions.

## Left to the builder

Weights, connector word list, similarity scoring details, measurer interface shape.
