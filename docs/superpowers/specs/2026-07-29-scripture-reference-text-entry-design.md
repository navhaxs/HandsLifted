# Scripture Reference Text Entry Design

**Date:** 2026-07-29
**Status:** Draft, pending user review
**Depends on:** `2026-07-26-scripture-add-item-entry-design.md` (the `ScriptureAddDialog` this design extends), `ScriptureBookCatalog`, `ScriptureLocalUsxStore`.

## Background

`ScriptureAddDialog` (see the 2026-07-26 spec) ships with a book `ComboBox` + 4 `NumericUpDown` spinners (Start/End Chapter/Verse) and was explicitly called a stopgap, deferring "a full Phase 4b passage-entry editor." That spec's non-goals also excluded any cross-field validation.

This design adds free-text reference entry — e.g. typing `1 Peter 1:10-12`, `1 Peter 1:20-2:8`, or `Rom 8:28` — as a faster alternative to the spinners, without removing the spinners.

## Goal

Let a user type a scripture reference in natural shorthand and have it resolve to a validated book/chapter/verse range, while keeping the existing spinner-based picker available as a fallback/precision mode.

## Non-Goals

- No comma-separated / non-contiguous verse lists (e.g. `Rom 8:28,31-34`) — `ScriptureItem`'s model is a single contiguous range; supporting lists would require a model change out of scope here.
- No exhaustive abbreviation enumeration per book — seed with one common abbreviation per book plus numeric-prefix variants; expand later if real usage surfaces misses (YAGNI).
- No changes to `ScriptureItem`'s persisted fields — still 4 plain ints (`StartChapter`, `StartVerse`, `EndChapter`, `EndVerse`) plus book code. This feature only changes how those ints get filled in during entry.
- No translation picker (unchanged from the 2026-07-26 spec — still hardcoded to `ScriptureUsxDownloader.FixedTranslation`).

## UI

`ScriptureAddDialog` gains a segmented mode toggle ("Type" / "Pick") above the existing controls:

- **Type mode:** single `TextBox` + inline hint/error `TextBlock` below it (red when invalid, hidden when valid). Placeholder text: `e.g. 1 Peter 1:10-12`.
- **Pick mode:** existing `BookComboBox` + 4 `NumericUpDown` fields, unchanged.
- The dialog persists last-used mode across opens (stored alongside other dialog-local UI state — no new persistence infrastructure needed, an instance field defaulting per-session is sufficient; do not over-engineer this into a saved user preference).
- **Insert** button binds to a shared `IsValid` flag — disabled whenever the active mode's current input fails parsing/validation.

### Shared reference state

Both modes read/write one shared state object owned by `ScriptureAddDialog.axaml.cs`:

```csharp
private sealed class ReferenceState
{
    public string? BookCode;
    public string? BookName;
    public int? StartChapter;
    public int? StartVerse;
    public int? EndChapter;
    public int? EndVerse;
    public bool IsValid;
    public string? ErrorMessage;
}
```

Switching modes syncs off this shared state in both directions:
- **Type → Pick:** parsed result fills the 4 spinners + selects the matching `BookComboBox` entry.
- **Pick → Type:** current spinner values are formatted back into text — `{Book} {StartCh}:{StartV}-{EndCh}:{EndV}`, collapsing to `{Book} {Ch}:{V}` when start equals end, or `{Book} {Ch}` when the range spans a full chapter (verse 1 through that chapter's last verse).

This keeps exactly one underlying reference regardless of which mode the user leaves the dialog in.

## Grammar

Whitespace-flexible, case-insensitive:

```
<book> <chapter>                             -> whole chapter (verse 1 .. last verse)
<book> <chapter>:<verse>                     -> single verse
<book> <chapter>:<verse>-<verse>             -> same-chapter range
<book> <chapter>:<verse>-<chapter>:<verse>   -> cross-chapter range
```

`<book>` resolves via longest-match against a new alias table (so `1 Peter` isn't chopped into `1` + leftover garbage). Numeric-prefix books accept `1`/`I`/`First`-style variants (`1 Peter`, `I Peter`, `First Peter`).

## New components

- **`ScriptureReferenceParser`** (static, pure, `HandsLiftedApp.Core`) — `TryParse(string input, out ParsedReference result)`. Purely structural/regex-based; no I/O, no book-data validation. Returns book token + chapter/verse fields per the grammar above (verse fields nullable to represent whole-chapter/single-verse/range variants before resolution).
- **`ScriptureBookAliasCatalog`** (static data, `HandsLiftedApp.Core`) — extends `ScriptureBookCatalog`'s 66 canonical names with one common abbreviation per book (e.g. `Rom` → Romans, `1 Pet`/`1pe` → 1 Peter) plus numeric-prefix variants. Exposes alias → book code lookup, case-insensitive.

No new class is needed for validation — it's a sequence of calls in the dialog's debounce handler (see Data Flow), reusing `ScriptureLocalUsxStore.Load(bookCode)` (already async, already the source of truth for chapter/verse bounds via the parsed `ScriptureBook`).

## Data Flow

1. `TextBox.TextChanged` → 300ms debounce.
2. `ScriptureReferenceParser.TryParse(text)` — structural only. Failure → set `ErrorMessage` to a grammar hint, `IsValid = false`, stop (no store hit).
3. Resolve book token via `ScriptureBookAliasCatalog` — unknown token → `ErrorMessage = "Unknown book \"...\""`, stop.
4. `await ScriptureLocalUsxStore.Load(bookCode)` — if this takes longer than ~150ms, show a subtle "checking…" state so the field doesn't look frozen. Load failure → `ErrorMessage = "Couldn't load {Book} — check scripture data path."`.
5. Validate chapter exists; validate verse(s) exist within that chapter's bounds; for whole-chapter input, resolve `EndVerse` to that chapter's last verse number.
6. On success: populate `ReferenceState` fields, `IsValid = true`, clear `ErrorMessage`.

Because step 4 is async and step 1 already debounces, a fast typist's in-flight validation for a stale input must be discarded if a newer keystroke started a new validation pass (guard with a generation counter or `CancellationTokenSource` swapped per keystroke — same pattern already used elsewhere in this codebase for debounced async UI work, avoid introducing a new one).

## Error Handling

| Case | Message |
|---|---|
| Unparseable grammar | `Couldn't understand that. Try "1 Peter 1:10-12".` |
| Unknown book token | `Unknown book "xyz".` |
| Chapter out of range | `{Book} has {N} chapters.` |
| Verse out of range | `{Book} {Ch} has {N} verses.` |
| Store load fails | `Couldn't load {Book} — check scripture data path.` |

## Testing

- **`ScriptureReferenceParser`** unit tests: all 4 grammar forms, abbreviations, numeric-prefix variants (`1`/`I`/`First`), malformed strings, ambiguous/prefix-collision tokens. Pure, no I/O.
- **`ScriptureBookAliasCatalog`** unit tests: case-insensitivity, whitespace variants, no duplicate alias mapping to two different book codes.
- Validation-against-store logic and the mode-toggle UI have no automated test — consistent with this dialog's existing precedent (no Avalonia UI test harness in this codebase). Verified by build + suite staying green, plus a manual click-through in the implementation plan covering: valid typed reference, invalid book, out-of-range chapter/verse, whole-chapter, cross-chapter range, and mode-toggle sync in both directions.
