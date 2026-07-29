# Scripture Item Edit-Button Wiring Design

**Date:** 2026-07-30
**Status:** Approved, ready for planning
**Depends on:** `2026-07-29-scripture-reference-text-entry-design.md` (the `ScriptureAddDialog` this design extends), `ScriptureItemInstance.GenerateSlidesAsync`.

## Background

`ItemSlidesView.axaml`'s generic `Fallback` item-list `DataTemplate` renders an `EditButton` for every item type that doesn't have its own dedicated template (`ItemSlidesView.axaml:336-347`). Its click handler, `EditButton_OnClick` (`ItemSlidesView.axaml.cs:60-109`), pattern-matches `sender.DataContext` against `SongItemInstance`, `MediaGroupItemInstance`, `PDFSlidesGroupItemInstance`, `PowerPointPresentationItemInstance`, and `GoogleSlidesGroupItemInstance` — opening a type-specific editor window/dialog for each. There is no `ScriptureItemInstance` branch, so for a scripture playlist item the Edit button is visible and wired to a click event, but does nothing.

`ScriptureAddDialog` (see the 2026-07-29 spec) only supports adding a brand-new reference — its constructor always defaults to Genesis 1:1 / the first book, with no way to seed it with an existing book/chapter/verse selection.

## Goal

Wire the Edit button for scripture playlist items so clicking it opens `ScriptureAddDialog` pre-populated with the item's current reference, and confirming updates that same item's reference and regenerated slides in place — without touching the app's Song/media editor patterns or introducing a second, divergent scripture-entry UI.

## Non-Goals

- No translation picker (unchanged — still hardcoded to `ScriptureUsxDownloader.FixedTranslation`, matching both the insert flow and this edit flow).
- No non-modal/live-binding editor like `SongEditorWindow` — this reuses `ScriptureAddDialog`'s existing modal `ShowDialog` + `Result`-on-close pattern, since the dialog's Type/Pick toggle and debounced validation are already built around that shape and reworking it into a live-bound window would be a much larger, unrelated change.
- No "preserve manually-renamed title" logic — editing the reference always regenerates `Title` from the new book/chapter/verse, exactly like inserting a new scripture item does today. If a user has manually renamed a scripture item's title, editing its reference will overwrite that rename.
- No change to `ScriptureItem`'s persisted fields or to `ScriptureAddDialog`'s `Result` shape — both stay exactly as the 2026-07-29 work left them.

## Dialog Changes (`ScriptureAddDialog`)

A new constructor overload:

```csharp
public ScriptureAddDialog(string bookCode, int startChapter, int startVerse, int endChapter, int endVerse, ScriptureLocalUsxStore? store = null)
```

- Runs the same setup as the existing parameterless constructor (store resolution, mode-toggle wiring), then additionally: selects the matching `BookComboBox` entry and sets all 4 `NumericUpDown` values from the given ints, formats those same values into `ReferenceTextBox.Text` via the existing `FormatReference` helper, and runs the formatted text through the existing `ValidateTypedReferenceAsync` path (the same one keystrokes already trigger) rather than assuming the caller's values are still valid — book data on disk could have changed since the item was created, so this reuses the dialog's one validation path instead of adding a second, separately-trusted one.
- `Title` becomes `"Edit Scripture"` and `InsertButton.Content` becomes `"Save"` when constructed via this overload (vs. `"Add Scripture"` / `"Insert"` for the existing parameterless constructor) — driven by a private `bool _isEditing` field set based on which constructor ran.
- `Result`'s shape and the Cancel behavior are unchanged — same 6-tuple, `null` on Cancel/close-without-confirming.

## Title Formatting — Extracted Shared Helper

The scripture title format (`"{Book} {Ch}:{V}"` for a single verse, `"{Book} {Ch}:{V}-{Ch}:{V}"` for a range) currently exists once, inline, in `MainViewModel.cs:373-377` (the `AddItemMessage.AddItemType.Scripture` case). This design extracts it into a small static helper so the insert path and the new edit path share one implementation instead of duplicating the branching logic:

```csharp
public static class ScriptureTitleFormatter
{
    public static string Format(string bookName, int startChapter, int startVerse, int endChapter, int endVerse) =>
        startChapter == endChapter && startVerse == endVerse
            ? $"{bookName} {startChapter}:{startVerse}"
            : $"{bookName} {startChapter}:{startVerse}-{endChapter}:{endVerse}";
}
```

`MainViewModel.cs:374-377`'s inline ternary is replaced with a call to this helper; the new edit wiring below calls the same helper.

## Wiring (`ItemSlidesView.axaml.cs`)

`EditButton_OnClick`'s signature changes from `void` to `async void` (matching this codebase's established convention for dialog-awaiting UI event handlers — see `AddItemFlyoutResourceDictionary.axaml.cs`'s `OnMenuItemClick`, already `async void` for the same reason). A new branch is added (order relative to the existing branches doesn't matter, since each pattern-matches a disjoint type and `return`s):

```csharp
if (sender is Control { DataContext: ScriptureItemInstance scripture } scriptureControl)
{
    var parentWindow = TopLevel.GetTopLevel(scriptureControl) as Window;
    if (parentWindow == null) return;

    var dialog = new ScriptureAddDialog(scripture.Book, scripture.StartChapter, scripture.StartVerse, scripture.EndChapter, scripture.EndVerse);
    await dialog.ShowDialog(parentWindow);
    if (dialog.Result == null) return;

    var result = dialog.Result.Value;
    scripture.Book = result.BookCode;
    scripture.StartChapter = result.StartChapter;
    scripture.StartVerse = result.StartVerse;
    scripture.EndChapter = result.EndChapter;
    scripture.EndVerse = result.EndVerse;
    scripture.Title = ScriptureTitleFormatter.Format(result.BookName, result.StartChapter, result.StartVerse, result.EndChapter, result.EndVerse);
    _ = scripture.GenerateSlidesAsync().ContinueWith(
        t => Log.Error(t.Exception, "Failed to generate scripture slides for {Title}", scripture.Title),
        TaskContinuationOptions.OnlyOnFaulted);
    return;
}
```

This mirrors `MainViewModel.cs:379-390`'s existing insert-time construction almost exactly, just mutating an existing `ScriptureItemInstance` instead of `new`-ing one, and using `TopLevel.GetTopLevel` directly rather than the CLAUDE.md-documented logical-tree-walk workaround — `EditButton` is rendered inline inside the `Fallback` `DataTemplate` in the playlist's item list, not inside a `Popup`/`Flyout`/`ContextMenu`, so the direct `TopLevel.GetTopLevel` lookup that already fails inside popups is expected to succeed here. This must be confirmed by actually clicking the button in a running app before considering the work done (per CLAUDE.md's rule for any code opening a dialog from a click handler) — if it turns out `EditButton` is nested inside a popup boundary after all, fall back to the logical-tree `.Parent`-walk pattern instead.

`Log` here refers to `Serilog`'s static logger, already used the same way at the `MainViewModel.cs:389-390` call site this mirrors — confirm the necessary `using Serilog;` is present in `ItemSlidesView.axaml.cs` (it currently is not) when implementing.

## Error Handling & Edge Cases

- **Cancel:** `Result` stays `null`, the method returns immediately — no mutation to `scripture`, no `GenerateSlidesAsync` call, no title change.
- **Book data not locally downloaded for the current translation:** Type-mode validation in the seeded dialog shows the same `"Couldn't load {Book} — check scripture data path."` hint it already shows during Add — no new failure mode. Pick-mode remains usable regardless, since it performs no store validation, consistent with today's Add behavior.
- **`GenerateSlidesAsync` failure:** uses the same fire-and-forget + `ContinueWith(..., OnlyOnFaulted)` logging pattern already used at every other call site (`ItemInstanceFactory.cs:70`, `MainViewModel.cs:389-390`) — no new error-handling shape introduced.

## Testing

- `ScriptureTitleFormatter.Format` gets unit tests (same-verse case, same-chapter range, cross-chapter range) — pure logic, no I/O, and this is now shared by two call sites so a regression here would affect both insert and edit.
- The new `ScriptureAddDialog` constructor overload and the `EditButton_OnClick` wiring get no automated test — consistent with this dialog's existing precedent (no Avalonia UI test harness in this codebase). Verified by build + full suite staying green, plus a manual click-through in the implementation plan: edit an existing scripture playlist item, confirm the dialog opens titled "Edit Scripture" with "Save" as the confirm button, pre-filled correctly in both Type and Pick modes; confirm Save updates the item's title and regenerates its slides with the new reference's verse text; confirm Cancel leaves the item completely unchanged; confirm `TopLevel.GetTopLevel` actually resolves the parent window from this specific button (per the Wiring section's caveat).
