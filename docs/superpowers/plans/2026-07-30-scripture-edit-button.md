# Scripture Item Edit-Button Wiring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the (currently dead) Edit button for scripture playlist items so it opens `ScriptureAddDialog` pre-populated with the item's current reference, and confirming updates that item's reference/title in place and regenerates its slides.

**Architecture:** A small static `ScriptureTitleFormatter` extracts the title-format logic already inline in `MainViewModel`'s insert path, shared by both insert and this new edit path. `ScriptureAddDialog` gains a second constructor overload that seeds the Pick-mode controls and re-validates the seeded reference through the dialog's existing debounced Type-mode validation path (rather than trusting the caller blindly), plus relabels the window/heading/button for editing. `ItemSlidesView.axaml.cs`'s `EditButton_OnClick` gets a new `ScriptureItemInstance` branch mirroring the existing Song/media branches' shape but mutating the existing instance instead of constructing a new one.

**Tech Stack:** net10.0, MSTest, Avalonia 12.1.0.

## Global Constraints

- No change to `ScriptureItem`'s persisted fields or to `ScriptureAddDialog.Result`'s shape.
- No translation picker — unchanged, still hardcoded to `ScriptureUsxDownloader.FixedTranslation` implicitly (translation isn't touched by this feature at all; the dialog only ever dealt with book/chapter/verse).
- Editing a reference always regenerates `Title` from the new book/chapter/verse via `ScriptureTitleFormatter` — no "was this title manually renamed" detection.
- No non-modal/live-binding editor — reuses `ScriptureAddDialog`'s existing modal `ShowDialog`/`Result`-on-close pattern.
- No automated UI test for the dialog or the edit-button wiring — consistent with this dialog's existing precedent (no Avalonia UI test harness in this codebase). Verified by build + full suite staying green, plus a manual click-through in Task 3.
- Current baseline: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo` passes 205 tests before this plan starts.

---

### Task 1: `ScriptureTitleFormatter` (extract shared title logic)

**Files:**
- Create: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureTitleFormatter.cs`
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureTitleFormatterTests.cs`
- Modify: `HandsLiftedApp.Core/ViewModels/MainViewModel.cs:373-377`

**Interfaces:**
- Produces: `public static class ScriptureTitleFormatter { public static string Format(string bookName, int startChapter, int startVerse, int endChapter, int endVerse); }` — Task 3's edit wiring calls this exact method.
- Consumes: nothing new.

This task is independent of Tasks 2-3 (which depend on it); it's a mechanical extraction plus one caller-site update.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureTitleFormatterTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureTitleFormatterTests
{
    [TestMethod]
    public void Format_SingleVerse_ReturnsBookChapterColonVerse()
    {
        Assert.AreEqual("Romans 8:28", ScriptureTitleFormatter.Format("Romans", 8, 28, 8, 28));
    }

    [TestMethod]
    public void Format_SameChapterRange_ReturnsBookChapterColonVerseDashVerse()
    {
        Assert.AreEqual("1 Peter 1:10-12", ScriptureTitleFormatter.Format("1 Peter", 1, 10, 1, 12));
    }

    [TestMethod]
    public void Format_CrossChapterRange_ReturnsBookChapterColonVerseDashChapterColonVerse()
    {
        Assert.AreEqual("1 Peter 1:20-2:8", ScriptureTitleFormatter.Format("1 Peter", 1, 20, 2, 8));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureTitleFormatterTests"`
Expected: FAIL — compile error, `ScriptureTitleFormatter` doesn't exist yet.

- [ ] **Step 3: Implement `ScriptureTitleFormatter`**

`HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureTitleFormatter.cs`:

```csharp
namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public static class ScriptureTitleFormatter
    {
        public static string Format(string bookName, int startChapter, int startVerse, int endChapter, int endVerse) =>
            startChapter == endChapter && startVerse == endVerse
                ? $"{bookName} {startChapter}:{startVerse}"
                : $"{bookName} {startChapter}:{startVerse}-{endChapter}:{endVerse}";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureTitleFormatterTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Update `MainViewModel.cs`'s insert path to use the new helper**

In `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`, replace lines 373-377:

```csharp
                    case AddItemMessage.AddItemType.Scripture:
                        var scriptureTitle = addItemMessage.ScriptureStartChapter == addItemMessage.ScriptureEndChapter &&
                                              addItemMessage.ScriptureStartVerse == addItemMessage.ScriptureEndVerse
                            ? $"{addItemMessage.ScriptureBookName} {addItemMessage.ScriptureStartChapter}:{addItemMessage.ScriptureStartVerse}"
                            : $"{addItemMessage.ScriptureBookName} {addItemMessage.ScriptureStartChapter}:{addItemMessage.ScriptureStartVerse}-{addItemMessage.ScriptureEndChapter}:{addItemMessage.ScriptureEndVerse}";
```

with:

```csharp
                    case AddItemMessage.AddItemType.Scripture:
                        var scriptureTitle = ScriptureTitleFormatter.Format(
                            addItemMessage.ScriptureBookName!,
                            addItemMessage.ScriptureStartChapter!.Value,
                            addItemMessage.ScriptureStartVerse!.Value,
                            addItemMessage.ScriptureEndChapter!.Value,
                            addItemMessage.ScriptureEndVerse!.Value);
```

`MainViewModel.cs` already has `using HandsLiftedApp.Core.Models.RuntimeData.Items;` (line 20) — no new `using` needed. The rest of the `case` block (constructing `scripture`, the `GenerateSlidesAsync` call, `itemToInsert = scripture; break;`) is unchanged.

- [ ] **Step 6: Build and run the full suite**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --nologo`
Expected: builds with 0 errors.

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 205 + 3 = 208, no regressions.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureTitleFormatter.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureTitleFormatterTests.cs HandsLiftedApp.Core/ViewModels/MainViewModel.cs
git commit -m "refactor: extract ScriptureTitleFormatter, shared by insert and (upcoming) edit paths"
```

---

### Task 2: `ScriptureAddDialog` edit-mode constructor

**Files:**
- Modify: `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml`
- Modify: `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs`

**Interfaces:**
- Produces: `public ScriptureAddDialog(string bookCode, int startChapter, int startVerse, int endChapter, int endVerse, ScriptureLocalUsxStore? store = null)` — Task 3's edit wiring constructs the dialog via this overload. `Result`'s shape is unchanged from the existing parameterless constructor's dialog.
- Consumes: `ScriptureBookCatalog.AllBooks`, `FormatReference` (both already exist in this file, unchanged).

This task depends on nothing from Task 1; Task 3 depends on this task's new constructor.

- [ ] **Step 1: Add `x:Name` to the dialog's heading `TextBlock`**

In `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml`, change line 30 from:

```xml
                <TextBlock Text="Add Scripture" FontWeight="SemiBold" FontSize="14" Margin="0 4 0 0" />
```

to:

```xml
                <TextBlock x:Name="HeadingText" Text="Add Scripture" FontWeight="SemiBold" FontSize="14" Margin="0 4 0 0" />
```

Nothing else in the XAML changes.

- [ ] **Step 2: Add the edit-mode constructor**

In `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs`, add this new constructor immediately after the existing one (after the closing brace of the `public ScriptureAddDialog(ScriptureLocalUsxStore? store = null)` constructor, i.e. after line 55):

```csharp

        public ScriptureAddDialog(string bookCode, int startChapter, int startVerse, int endChapter, int endVerse, ScriptureLocalUsxStore? store = null)
            : this(store)
        {
            Title = "Edit Scripture";
            HeadingText.Text = "Edit Scripture";
            InsertButton.Content = "Save";

            var idx = ScriptureBookCatalog.AllBooks.ToList().FindIndex(b => b.Code == bookCode);
            var bookName = idx >= 0 ? ScriptureBookCatalog.AllBooks[idx].Name : bookCode;

            if (idx >= 0)
            {
                BookComboBox.SelectedIndex = idx;
            }

            StartChapterUpDown.Value = startChapter;
            StartVerseUpDown.Value = startVerse;
            EndChapterUpDown.Value = endChapter;
            EndVerseUpDown.Value = endVerse;

            // Setting Text fires OnReferenceTextChanged synchronously, which unconditionally
            // disables InsertButton while the debounced re-validation runs (correct in Type mode:
            // Insert should stay disabled until the freshly-seeded reference re-validates against
            // real book data, since that data could have changed on disk since this item was
            // created). But if the remembered mode is Pick, Pick mode's invariant is "always
            // enabled" — restore that explicitly, since nothing else will after this point.
            ReferenceTextBox.Text = FormatReference(bookName, startChapter, startVerse, endChapter, endVerse);

            if (PickModeRadio.IsChecked == true)
            {
                InsertButton.IsEnabled = true;
            }
        }
```

This constructor chains to the existing `ScriptureAddDialog(ScriptureLocalUsxStore? store)` constructor (`: this(store)`), so `InitializeComponent()`, store resolution, the book-combo item source, and the initial mode selection (`s_preferPickMode`) all run exactly as before — this constructor only adds the edit-specific relabeling and pre-population on top, after the base constructor body has fully completed (including `_initializing = false`).

Reusing the existing debounced `ValidateTypedReferenceAsync` path (via the `ReferenceTextBox.Text` assignment) rather than trusting `bookCode`/`startChapter`/etc. as already-valid is deliberate: it's the one validation path the dialog has, and re-running it means a book whose data has since been deleted/moved on disk is caught the same way a typo would be, instead of silently letting Insert/Save through with unchecked values.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --nologo`
Expected: builds with 0 errors.

- [ ] **Step 4: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, same count as end of Task 1 (208) — this task adds no automated tests, no regressions.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs
git commit -m "feat: add pre-populated edit-mode constructor to ScriptureAddDialog"
```

---

### Task 3: Wire the Edit button for `ScriptureItemInstance`

**Files:**
- Modify: `HandsLiftedApp.Core/Views/ItemSlidesView.axaml.cs`

**Interfaces:**
- Consumes: `ScriptureAddDialog`'s edit-mode constructor (Task 2), `ScriptureTitleFormatter.Format` (Task 1), `ScriptureItemInstance.GenerateSlidesAsync` (already exists).
- Produces: nothing further downstream — this is the last task.

This is the last task; it composes Tasks 1-2 into the actual user-facing edit flow.

- [ ] **Step 1: Add the missing `using` directives**

In `HandsLiftedApp.Core/Views/ItemSlidesView.axaml.cs`, add these three lines to the existing `using` block (alphabetical position doesn't matter here, but keep them grouped with the existing similar `using`s for readability — e.g. `System.Threading.Tasks` near the top with the other `System.*` usings, `HandsLiftedApp.Core.Views` and `Serilog` near the other `HandsLiftedApp.*`/third-party usings):

```csharp
using System.Threading.Tasks;
using HandsLiftedApp.Core.Views;
using Serilog;
```

`System.Threading.Tasks` is needed for `TaskContinuationOptions`; `HandsLiftedApp.Core.Views` is needed for `ScriptureAddDialog` (this file's own namespace is `HandsLiftedApp.Controls`, a sibling, not a parent/child of `HandsLiftedApp.Core.Views`, so it isn't visible without this `using`); `Serilog` is needed for `Log.Error`. `ScriptureItemInstance` itself is already reachable — `using HandsLiftedApp.Core.Models.RuntimeData.Items;` is already present (line 16 in the current file).

- [ ] **Step 2: Change `EditButton_OnClick`'s signature to `async void`**

In `HandsLiftedApp.Core/Views/ItemSlidesView.axaml.cs`, change:

```csharp
        private void EditButton_OnClick(object? sender, RoutedEventArgs e)
```

to:

```csharp
        private async void EditButton_OnClick(object? sender, RoutedEventArgs e)
```

This matches this codebase's established convention for dialog-awaiting UI event handlers (see `AddItemFlyoutResourceDictionary.axaml.cs`'s `OnMenuItemClick`, already `async void` for the same reason). The existing Song/media branches (which call `.Show()`, not `.ShowDialog()`, and don't `await` anything) are unaffected by this signature change — they still run and `return` synchronously exactly as before.

- [ ] **Step 3: Add the `ScriptureItemInstance` branch**

In the same method, add this branch. Placement relative to the existing branches doesn't matter (each pattern-matches a disjoint type and `return`s) — add it either before the `SongItemInstance` branch or after the `GoogleSlidesGroupItemInstance` branch (i.e. right before the method's closing `}`):

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

                // forceInvalidateCache: true — UpdatePages reuses existing ScriptureSlideInstances
                // by page index and only resets a reused slide's Cached bitmap when the resolved
                // theme object changed; an edited verse RANGE (this call) can produce the same
                // page count with entirely different text, which that reuse check alone would not
                // catch, leaving a stale cached thumbnail. Forcing invalidation here is the same
                // fix CLAUDE.md documents for the analogous theme-reassignment case, generalized to
                // content changes.
                _ = scripture.GenerateSlidesAsync(forceInvalidateCache: true).ContinueWith(
                    t => Log.Error(t.Exception, "Failed to generate scripture slides for {Title}", scripture.Title),
                    TaskContinuationOptions.OnlyOnFaulted);
                return;
            }
```

`TopLevel.GetTopLevel(scriptureControl)` (not the CLAUDE.md-documented logical-tree-walk workaround) is used here because `EditButton` is rendered inline inside the `Fallback` `DataTemplate` in the playlist's item list — not inside a `Popup`/`Flyout`/`ContextMenu` — so the direct lookup is expected to succeed. **This must be confirmed by actually clicking Edit on a scripture item in a running app** (Step 5) before considering this task done; if `parentWindow` turns out to be `null` in practice, replace this line with the logical-tree `.Parent`-walk pattern from `HandleAddItemButtonClick.ShowAddWindow`/`AddItemFlyoutResourceDictionary.axaml.cs`'s own fallback.

- [ ] **Step 4: Build and run the full suite**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --nologo`
Expected: builds with 0 errors.

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, same count as end of Task 2 (208) — this task adds no automated tests, no regressions.

- [ ] **Step 5: Manual verification**

Run the app. With a playlist open containing at least one scripture item (inserted via the existing Add Scripture flow) and its book's data downloaded locally:

1. Select the scripture item, click its **Edit** button — confirm a dialog opens titled "Edit Scripture" with a "Save" button (not "Insert"), pre-filled with the item's current reference in whichever mode (Type/Pick) was last used, and confirm `parentWindow` actually resolved (the dialog opens centered on the main window rather than not opening at all or opening detached).
2. In Type mode, confirm the pre-filled text matches the current reference and Insert/Save is initially disabled, then becomes enabled once the brief re-validation completes.
3. Switch to Pick mode — confirm the book combo and all 4 spinners show the item's current values, and Save is enabled.
4. Change the reference (e.g. to a different book/chapter/verse) and click Save — confirm the playlist item's title updates to the new reference and its slides show the new verse text (not stale content).
5. Reopen the dialog and click Cancel — confirm nothing about the item changes (title, slides, reference all stay exactly as they were).
6. Edit a reference to a DIFFERENT verse range that happens to produce the SAME number of slides as before (e.g. two different single-verse references) — confirm the thumbnail/slide content actually updates to the new text rather than showing stale content (this specifically exercises the `forceInvalidateCache: true` fix from Step 3).

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Views/ItemSlidesView.axaml.cs
git commit -m "feat: wire Edit button for scripture playlist items"
```

---

## Final Whole-Branch Review

After all 3 tasks: full suite should be at 208 tests (205 baseline + 3 new `ScriptureTitleFormatterTests`; Tasks 2-3 add none). Confirm `ScriptureAddDialog`'s existing parameterless-constructor callers (`AddItemFlyoutResourceDictionary.axaml.cs`) need zero changes — this plan only adds a second constructor overload, never modifies the first one's behavior. Confirm the manual click-through in Task 3 Step 5 was actually run in a live app window, not skipped — this dialog and its edit wiring have no automated UI test, so that walkthrough (especially item 6, the same-slide-count content-change case) is the only verification this feature's interactive behavior gets.
