# Song lyric reflow - design

## Intent
Long unbroken stanzas are hard to read on the projector. Slides split on blank lines inside `SongStanza.Lyrics` (`SongItemInstance.cs` ~557), so "reflow" = rewrite the blank lines in each stanza. Add a Reflow button to the song editor: fixed 2/3/4 lines per slide, or Smart (algorithm picks per stanza).

## Goals
- One button/dropdown in the lyric editor (`SongLyricBlockEditor`, next to the Rearrange control): Smart, 2, 3, 4 lines per slide.
- Applies to the whole song; one undo step (`SongEditorViewModel.BeginUndoStep("Reflow lyrics")`).
- Overwrites existing blank-line splits in ALL modes, including Smart (decided with user).
- Smart gives sensible results for 1-9+ line stanzas, including hymn verses (8 lines).
- Pure, display-free algorithm, unit-tested.

## Non-goals / anti-goals
- NO splitting inside a line (and no capitalising the next word). Deferred until the user supplies a real song that needs it; revisit then. Library max line is 51 chars, nothing wraps.
- No measuring with the real theme font/width. No per-stanza reflow. No Skia dependency.
- Never change lyric text, only blank lines. Empty stanzas (interludes) untouched. Idempotent.

## Approaches considered
- A. Lookup table by line count: simple, breaks on 5/7/9+, ignores rhyme.
- B. Scored split (CHOSEN): DP over chunk sizes 2-4, minimise cost, weights in one place, testable.
- C. Real-renderer measurement: most accurate but ties to Skia/theme; not needed now. B can later take a per-line "wrapped line count" input without redesign.

## Design
`LyricReflow` (static, in `HandsLiftedApp.Core/Models/Library/`, check the csproj the folder compiles into):
- `string Reflow(string lyrics, ReflowMode mode)`; `ReflowMode` = Smart | Fixed(n in 2..4).
- Normalise: `\r\n`->`\n`, trim lines, drop all blank lines, then chunk, join chunks with a blank line.
- Fixed n: chunks of n; a 1-line remainder is avoided by rebalancing the last two chunks (7 lines at 3 -> 3,2,2). Stanzas with <= n lines stay whole.
- Smart: stanza of 1-4 lines stays whole. Otherwise DP over chunk sizes {2,3,4}; cost = orphan(1 line) forbidden, +small per 4-line chunk, +small per unequal chunk pair, - bonus when a break follows a rhyming line pair (crude: last word shares final 2-3 letters). Equal chunks win: 6 -> 3+3, 8 -> 4+4, 5 -> 3+2 or 2+3 by rhyme, 7 -> 4+3/3+4, 9 -> 3+3+3. Weights are named constants, tuned against `V:\Songs`.
- Editor: in `SongLyricBlockEditor`, a Button with Flyout (or MenuFlyout) holding the four options; handler runs the reflow inside the undo step, setting `stanza.Lyrics` only when it changed. Free-text tab refreshes via the existing `_song_PropertyChanged`.

## Testing
- Unit tests (`HandsLiftedApp.Tests/Models/Library/`): each line count 1-10 in each mode, idempotence, text preserved, rhyme tiebreak, CRLF, empty stanza.
- Run Smart over all `V:\Songs` stanzas and eyeball the output.
- Headless UI test: click the flyout item via `FindFlyoutMenuItem` / `ClickInPopup` (the flyout-click-does-nothing trap in CLAUDE.md), confirm lyrics changed, then undo restores.

## Left to the builder
Exact weights, rhyme heuristic details, button icon/placement polish, folder/project placement of `LyricReflow`.
