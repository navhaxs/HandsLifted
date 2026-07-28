# Scripture Add-Item Entry Point Design

**Date:** 2026-07-26
**Status:** Approved, ready for planning
**Depends on:** Phases 1–4a and the local-USX-source plan (all merged on `feature/scripture-slide-type`) — `ScriptureItemInstance`, `ScriptureUsxDownloader.AllBookCodes`, and the `AddItemMessage`/`OnMenuItemClick` add-item pipeline all already exist.

## Background

`HandsLiftedApp.Core/Assets/AddItemFlyoutResourceDictionary.axaml` is the "add item" flyout menu (`_Presentation`, `_Google Slides`, `_Song`, `_Media`, `_Logo`, `_Section`, `_Comment`, etc.). Each `MenuItem` either sends an `AddItemMessage` (handled by a big `switch` in `MainViewModel`'s `MessageBus.Current.Listen<AddItemMessage>()` subscriber, which constructs the right `*Instance` and inserts it into `Playlist.Items` at the right position) or, for `NewSong`/`ExistingSong`, opens a window directly from `AddItemFlyoutResourceDictionary.axaml.cs`'s `OnMenuItemClick`.

There is no "Scripture" entry today. `ScriptureItem`/`ScriptureItemInstance` are fully built and working (Phases 1–4a, local-USX-source plan) but the only ways to get one into a playlist are hand-editing XML or a unit test — no UI path exists.

## Goal

Add "Scripture" to the flyout so a user can insert a working `ScriptureItemInstance` — with a real book/chapter/verse selection, not a blank placeholder — directly from the playlist editor, without waiting for a full Phase 4b passage-entry editor to be designed and built.

## Non-Goals

- No translation picker — hardcoded to `ScriptureUsxDownloader.FixedTranslation` (`eng_bsb`), since nothing else is downloadable yet.
- No full Phase 4b editor (re-editing an already-inserted scripture item's reference after the fact, theming, etc.) — this is only the creation entry point.
- No cross-field validation on the chapter/verse range (e.g. rejecting an End before a Start) — an invalid range degrades to the same "zero slides" outcome `ScriptureVerseRangeExtractor` already produces for any bad range today; not worth dedicated validation UI for this pass.

## Entry Point Flow

1. `AddItemFlyoutResourceDictionary.axaml` gets a new `MenuItem` (`CommandParameter="Scripture"`, `Header="_Scripture"`), styled like its siblings.
2. `AddItemFlyoutResourceDictionary.axaml.cs`'s `OnMenuItemClick` special-cases `AddItemType.Scripture` the same way it already special-cases `ExistingSong`/`NewSong`: computes `itemInsertIndex`/`nearestItem` as normal, then opens `ScriptureAddDialog` via `await dialog.ShowDialog(parent)` (`parent` resolved via `TopLevel.GetTopLevel(menuItem) as Window`, matching `LibraryQueryView.axaml.cs`'s `RenameDialog` usage). This requires making `OnMenuItemClick` `async void` — already this codebase's established convention for dialog-awaiting UI event handlers (see `RenameItem_OnClick`).
3. If `dialog.Result` is non-null (user clicked Insert, not Cancel), send `AddItemMessage` with `Type = AddItemType.Scripture`, `InsertIndex`/`ItemToInsertAfter` set as normal, and the new Scripture-specific fields populated from `dialog.Result`.
4. `MainViewModel`'s existing `AddItemMessage` subscriber gets a new `case AddItemType.Scripture:` that builds the instance and falls through to the same generic insert-position code (`Insert`/`Add` at the computed index) every other type already shares.

## Dialog UI

`ScriptureAddDialog.axaml` — a `Window`, styled like `RenameDialog` (rounded border, no window chrome, `CenterOwner`, non-resizable, similar fixed size).

- **Book:** `ComboBox` bound to `ScriptureBookCatalog.AllBooks` (new static list, see below), displaying real book names ("Genesis", "John", "1 Corinthians") in canonical Bible order. Defaults to the first entry (Genesis) so the dialog is immediately submittable with no forced selection.
- **Chapter/verse range:** 4 `NumericUpDown` fields — Start Chapter, Start Verse, End Chapter, End Verse — each defaulting to `1`, matching `ScriptureItem`'s own field defaults.
- **No translation field.**
- **Buttons:** "Insert" (`IsDefault="True"`) and "Cancel" (`IsCancel="True"`). "Insert" is disabled only if any `NumericUpDown` is null/empty (Avalonia allows clearing a `NumericUpDown` to blank) — otherwise always enabled, since the default selection is always valid.
- Code-behind, no separate ViewModel (matches `RenameDialog`'s style): exposes a single nullable result —
  ```csharp
  public (string BookCode, string BookName, int StartChapter, int StartVerse, int EndChapter, int EndVerse)? Result { get; private set; }
  ```
  set on Insert, left `null` on Cancel or window close without confirming.

## Wiring

**`AddItemMessage`** (`HandsLiftedApp.Controls/Messages/AddItemMessage.cs`):
- Add `Scripture` to the `AddItemType` enum.
- Add 6 new nullable properties: `ScriptureBookCode`, `ScriptureBookName`, `ScriptureStartChapter`, `ScriptureStartVerse`, `ScriptureEndChapter`, `ScriptureEndVerse` — following the exact precedent `CreateInfo` already set (a type-specific payload riding on the shared message; `CreateInfo` today is used only by `GoogleSlides`, so this record already mixes generic and type-specific fields).

**`ScriptureBookCatalog`** (new, `HandsLiftedApp.Core`): a static class exposing
```csharp
public static IReadOnlyList<(string Code, string Name)> AllBooks { get; }
```
built by zipping `ScriptureUsxDownloader.AllBookCodes` (already the single source of truth for the 66 codes, in canonical order) with a parallel, hardcoded 66-entry array of real book names in that same order. Codes are never duplicated into this new class — only names are added.

**`MainViewModel`**'s `AddItemMessage` subscriber: new `case AddItemMessage.AddItemType.Scripture:` —
```csharp
var scripture = new ScriptureItemInstance(Playlist)
{
    Translation = ScriptureUsxDownloader.FixedTranslation,
    Book = addItemMessage.ScriptureBookCode!,
    StartChapter = addItemMessage.ScriptureStartChapter!.Value,
    StartVerse = addItemMessage.ScriptureStartVerse!.Value,
    EndChapter = addItemMessage.ScriptureEndChapter!.Value,
    EndVerse = addItemMessage.ScriptureEndVerse!.Value,
    Title = FormatScriptureTitle(addItemMessage.ScriptureBookName!, ...)
};
_ = scripture.GenerateSlidesAsync();
itemToInsert = scripture;
```
`Title` is auto-generated so the playlist row isn't blank: `"{BookName} {StartChapter}:{StartVerse}"` when the range is a single verse (`StartChapter == EndChapter && StartVerse == EndVerse`), else `"{BookName} {StartChapter}:{StartVerse}-{EndChapter}:{EndVerse}"` — matching the label format `ScriptureItemInstance.UpdateVerseSlides` already computes per-slide. `GenerateSlidesAsync`'s fire-and-forget call has no explicit fault handling added here — it already logs internally (missing-book → placeholder slide) and via `ItemInstanceFactory`'s established `ContinueWith(..., OnlyOnFaulted)` pattern elsewhere; this call site follows the same fire-and-forget convention (no `ContinueWith` needed here specifically since `GenerateSlidesAsync` itself already catches its one expected failure mode internally).

## Testing

- `ScriptureBookCatalog`: unit test asserting exactly 66 entries, `Code`s match `ScriptureUsxDownloader.AllBookCodes` exactly (same order, same values), and no duplicate `Name`s.
- The dialog and menu wiring have no automated test — consistent with `SetupWindow`'s UI in the local-USX-source plan (no Avalonia UI test harness exists in this codebase). Verified by build + full suite staying green, plus a manual click-through description in the implementation plan.
