# Scripture Add-Item Entry Point Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add "Scripture" to the add-item flyout menu so a user can insert a working `ScriptureItemInstance` — with a real book/chapter/verse selection collected via a small dialog — directly into the current playlist.

**Architecture:** A new static book-name catalog feeds a new small dialog window (styled like the existing `RenameDialog`); the flyout's existing `OnMenuItemClick` special-cases `Scripture` to open that dialog (mirroring how it already special-cases `NewSong`/`ExistingSong`) and, on confirm, sends the existing `AddItemMessage` extended with a few new nullable fields; `MainViewModel`'s existing `AddItemMessage` subscriber gets one new `case` that builds the `ScriptureItemInstance` and falls through to the shared insert-position code every other type already uses.

**Tech Stack:** .NET 8, MSTest, Avalonia 11 (`NumericUpDown`, `ComboBox`, `Window`), Material.Icons.Avalonia 2.4.1 (confirmed: `MaterialIconKind.Bible` exists in this exact pinned version).

## Global Constraints

- net8.0, MSTest, matches all prior phases.
- No translation field anywhere in the new dialog — hardcoded to `ScriptureUsxDownloader.FixedTranslation` (`eng_bsb`).
- No cross-field validation on the chapter/verse range (e.g. rejecting an End before a Start) — an invalid range degrades to the same "zero slides" outcome `ScriptureVerseRangeExtractor` already produces for any bad range; not in scope for this pass.
- No changes to `ScriptureItem`/`ScriptureItemInstance`'s data model — all 6 fields this plan needs (`Translation`, `Book`, `StartChapter`, `StartVerse`, `EndChapter`, `EndVerse`) already exist.
- No automated UI test for the dialog or menu wiring — consistent with `SetupWindow`'s UI in the local-USX-source plan (no Avalonia UI test harness exists in this codebase). Verified by build + full suite staying green, plus a manual click-through description in Task 4.

---

### Task 1: `ScriptureBookCatalog`

**Files:**
- Create: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureBookCatalog.cs`
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureBookCatalogTests.cs`

**Interfaces:**
- Produces: `public static class ScriptureBookCatalog { public static readonly IReadOnlyList<(string Code, string Name)> AllBooks; }` — Task 3's dialog binds its book `ComboBox` to this.
- Consumes: `ScriptureUsxDownloader.AllBookCodes` (`HandsLiftedApp.Importer.Scripture`, already exists) as the single source of truth for the 66 codes and their canonical order.

This task is independent of Tasks 2–4; it's purely additive, new-file-only.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureBookCatalogTests.cs`:

```csharp
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureBookCatalogTests
{
    [TestMethod]
    public void AllBooks_HasExactly66Entries()
    {
        Assert.AreEqual(66, ScriptureBookCatalog.AllBooks.Count);
    }

    [TestMethod]
    public void AllBooks_CodesMatchScriptureUsxDownloaderAllBookCodes_SameOrder()
    {
        var codes = ScriptureBookCatalog.AllBooks.Select(b => b.Code).ToList();
        CollectionAssert.AreEqual(ScriptureUsxDownloader.AllBookCodes.ToList(), codes);
    }

    [TestMethod]
    public void AllBooks_NamesAreAllUnique()
    {
        var names = ScriptureBookCatalog.AllBooks.Select(b => b.Name).ToList();
        Assert.AreEqual(names.Count, names.Distinct().Count());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureBookCatalogTests"`
Expected: FAIL — compile error, `ScriptureBookCatalog` doesn't exist yet.

- [ ] **Step 3: Implement `ScriptureBookCatalog`**

`HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureBookCatalog.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public static class ScriptureBookCatalog
    {
        private static readonly string[] Names =
        {
            "Genesis", "Exodus", "Leviticus", "Numbers", "Deuteronomy", "Joshua", "Judges", "Ruth", "1 Samuel", "2 Samuel",
            "1 Kings", "2 Kings", "1 Chronicles", "2 Chronicles", "Ezra", "Nehemiah", "Esther", "Job", "Psalms", "Proverbs",
            "Ecclesiastes", "Song of Solomon", "Isaiah", "Jeremiah", "Lamentations", "Ezekiel", "Daniel", "Hosea", "Joel", "Amos",
            "Obadiah", "Jonah", "Micah", "Nahum", "Habakkuk", "Zephaniah", "Haggai", "Zechariah", "Malachi",
            "Matthew", "Mark", "Luke", "John", "Acts", "Romans", "1 Corinthians", "2 Corinthians", "Galatians", "Ephesians",
            "Philippians", "Colossians", "1 Thessalonians", "2 Thessalonians", "1 Timothy", "2 Timothy", "Titus", "Philemon", "Hebrews", "James",
            "1 Peter", "2 Peter", "1 John", "2 John", "3 John", "Jude", "Revelation"
        };

        public static readonly IReadOnlyList<(string Code, string Name)> AllBooks =
            ScriptureUsxDownloader.AllBookCodes.Zip(Names, (code, name) => (code, name)).ToList();
    }
}
```

The `Names` array is in the exact same canonical order as `ScriptureUsxDownloader.AllBookCodes` (`HandsLiftedApp.Importer.Scripture/ScriptureUsxDownloader.cs`) — Genesis→Malachi (39 OT books) then Matthew→Revelation (27 NT books), matching that file's own ordering exactly. Do not reorder either list independently.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureBookCatalogTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 133 + 3 = 136, no regressions.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureBookCatalog.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureBookCatalogTests.cs
git commit -m "feat: add ScriptureBookCatalog mapping USX book codes to display names"
```

---

### Task 2: Extend `AddItemMessage` with a `Scripture` type and payload fields

**Files:**
- Modify: `HandsLiftedApp.Controls/Messages/AddItemMessage.cs`

**Interfaces:**
- Produces: `AddItemMessage.AddItemType.Scripture` enum value, plus 6 new nullable properties on `AddItemMessage`: `ScriptureBookCode`, `ScriptureBookName`, `ScriptureStartChapter`, `ScriptureStartVerse`, `ScriptureEndChapter`, `ScriptureEndVerse`. Task 4 both sets these (in `OnMenuItemClick`) and reads them (in `MainViewModel`'s subscriber).
- Consumes: nothing new.

This task is independent of Task 1 and Task 3; it's a small, mechanical, additive edit to one file.

- [ ] **Step 1: Edit `AddItemMessage.cs`**

Replace the whole file content of `HandsLiftedApp.Controls/Messages/AddItemMessage.cs` with:

```csharp
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Controls.Messages
{
    public record AddItemMessage
    {
        public int? InsertIndex { get; init; }
        public Item? ItemToInsertAfter { get; init; }
        public AddItemType Type { get; init; }

        public enum AddItemType
        {
            Presentation,
            GoogleSlides,
            PDF,
            ExistingSong,
            NewSong,
            // Media,
            Logo,
            SectionHeading,
            MediaGroup,
            BlankGroup,
            BibleReadingSlideGroup,
            Comment,
            Scripture
        }
        
        // TODO make this an interface?
        public string? CreateInfo { get; init; } = null;

        public string? ScriptureBookCode { get; init; }
        public string? ScriptureBookName { get; init; }
        public int? ScriptureStartChapter { get; init; }
        public int? ScriptureStartVerse { get; init; }
        public int? ScriptureEndChapter { get; init; }
        public int? ScriptureEndVerse { get; init; }
    }
}
```

The only changes from the current file: `Scripture` added as the last `AddItemType` enum member, and the 6 new nullable properties added after `CreateInfo`. Nothing else in the file changes.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build HandsLiftedApp.Controls/HandsLiftedApp.Controls.csproj --nologo`
Expected: builds with 0 errors (this record isn't exercised by any test directly — no behavior to unit test here, it's a pure data-shape change consumed by Task 4).

- [ ] **Step 3: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count unchanged from wherever Task 1 left it (136 if done first; this task adds no tests), no regressions.

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Controls/Messages/AddItemMessage.cs
git commit -m "feat: add Scripture type and payload fields to AddItemMessage"
```

---

### Task 3: `ScriptureAddDialog`

**Files:**
- Create: `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml`
- Create: `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs`

**Interfaces:**
- Consumes: `ScriptureBookCatalog.AllBooks` (Task 1).
- Produces: `public partial class ScriptureAddDialog : Window { public (string BookCode, string BookName, int StartChapter, int StartVerse, int EndChapter, int EndVerse)? Result { get; private set; } }`, shown via `await dialog.ShowDialog(parentWindow)` (standard Avalonia `Window` extension method, no custom API). Task 4 constructs, shows, and reads `Result` from this dialog.

This task's namespace is `HandsLiftedApp.Core.Views` (flat, not `.AddItem`) — matching `AddItemWindow.axaml.cs`'s own declared namespace exactly, even though both live under the `Views/AddItem/` folder; this codebase does not strictly enforce folder-equals-namespace, and matching the sibling file in the same folder avoids introducing a one-off inconsistency.

- [ ] **Step 1: Create the XAML**

`HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Width="360"
        Height="320"
        WindowStartupLocation="CenterOwner"
        ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaChromeHints="NoChrome"
        Background="Transparent"
        TransparencyLevelHint="Transparent"
        SystemDecorations="None"
        ShowInTaskbar="False"
        CanResize="False"
        x:Class="HandsLiftedApp.Core.Views.ScriptureAddDialog"
        Icon="/Assets/app.ico"
        Title="Add Scripture">
    <Border CornerRadius="8"
            Background="{DynamicResource BackgroundBrush}"
            BorderBrush="{DynamicResource WindowBorderBrush}"
            BorderThickness="1">
        <DockPanel Margin="15">
            <StackPanel Margin="0 10 0 0" DockPanel.Dock="Bottom"
                        Orientation="Horizontal" HorizontalAlignment="Right" Spacing="5">
                <Button Content="Insert" x:Name="InsertButton" IsDefault="True" Click="OnConfirmInsert" />
                <Button Content="Cancel" IsCancel="True" Click="OnCancel" />
            </StackPanel>

            <StackPanel Spacing="8">
                <TextBlock Text="Add Scripture" FontWeight="SemiBold" FontSize="14" Margin="0 4 0 0" />

                <TextBlock Text="Book" />
                <ComboBox x:Name="BookComboBox" HorizontalAlignment="Stretch" />

                <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,Auto" Margin="0 8 0 0">
                    <TextBlock Grid.Row="0" Grid.Column="0" Text="Start Chapter" />
                    <TextBlock Grid.Row="0" Grid.Column="1" Text="Start Verse" Margin="8 0 0 0" />
                    <NumericUpDown Grid.Row="1" Grid.Column="0" x:Name="StartChapterUpDown"
                                    Minimum="1" Value="1" FormatString="0" />
                    <NumericUpDown Grid.Row="1" Grid.Column="1" x:Name="StartVerseUpDown"
                                    Minimum="1" Value="1" FormatString="0" Margin="8 0 0 0" />
                </Grid>

                <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,Auto" Margin="0 8 0 0">
                    <TextBlock Grid.Row="0" Grid.Column="0" Text="End Chapter" />
                    <TextBlock Grid.Row="0" Grid.Column="1" Text="End Verse" Margin="8 0 0 0" />
                    <NumericUpDown Grid.Row="1" Grid.Column="0" x:Name="EndChapterUpDown"
                                    Minimum="1" Value="1" FormatString="0" />
                    <NumericUpDown Grid.Row="1" Grid.Column="1" x:Name="EndVerseUpDown"
                                    Minimum="1" Value="1" FormatString="0" Margin="8 0 0 0" />
                </Grid>
            </StackPanel>
        </DockPanel>
    </Border>
</Window>
```

- [ ] **Step 2: Create the code-behind**

`HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs`:

```csharp
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Core.Views
{
    public partial class ScriptureAddDialog : Window
    {
        public (string BookCode, string BookName, int StartChapter, int StartVerse, int EndChapter, int EndVerse)? Result { get; private set; }

        public ScriptureAddDialog()
        {
            InitializeComponent();
            BookComboBox.ItemsSource = ScriptureBookCatalog.AllBooks.Select(b => b.Name).ToList();
            BookComboBox.SelectedIndex = 0;
        }

        private void OnConfirmInsert(object? sender, RoutedEventArgs e)
        {
            if (BookComboBox.SelectedIndex < 0) return;
            if (StartChapterUpDown.Value is null || StartVerseUpDown.Value is null ||
                EndChapterUpDown.Value is null || EndVerseUpDown.Value is null) return;

            var selected = ScriptureBookCatalog.AllBooks[BookComboBox.SelectedIndex];

            Result = (
                selected.Code,
                selected.Name,
                (int)StartChapterUpDown.Value.Value,
                (int)StartVerseUpDown.Value.Value,
                (int)EndChapterUpDown.Value.Value,
                (int)EndVerseUpDown.Value.Value
            );
            Close();
        }

        private void OnCancel(object? sender, RoutedEventArgs e) => Close();
    }
}
```

`NumericUpDown.Value` is `decimal?` in Avalonia — the null-checks above guard the `.Value.Value` unwrap immediately below them, and the `(int)` casts are safe narrowing from a value the `NumericUpDown`'s own `Minimum="1"`/integer `FormatString="0"` keeps whole and small.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --nologo`
Expected: builds with 0 errors. (No automated test for this dialog — see Global Constraints.)

- [ ] **Step 4: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, same count as end of Task 2 (this task adds no tests), no regressions.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs
git commit -m "feat: add ScriptureAddDialog for collecting a book/chapter/verse selection"
```

---

### Task 4: Wire the flyout menu item, `OnMenuItemClick`, and `MainViewModel`'s dispatch

**Files:**
- Modify: `HandsLiftedApp.Core/Assets/AddItemFlyoutResourceDictionary.axaml`
- Modify: `HandsLiftedApp.Core/Assets/AddItemFlyoutResourceDictionary.axaml.cs`
- Modify: `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`

**Interfaces:**
- Consumes: `ScriptureAddDialog` (Task 3), `AddItemMessage`'s new fields (Task 2), `ScriptureItemInstance`/`ScriptureUsxDownloader.FixedTranslation` (already exist).
- Produces: nothing further downstream — this is the last task in the plan.

This is the last task; it composes everything built in Tasks 1–3 into the actual user-facing flow.

- [ ] **Step 1: Add the menu item to the flyout XAML**

In `HandsLiftedApp.Core/Assets/AddItemFlyoutResourceDictionary.axaml`, find the existing `_Song` `MenuItem`:

```xml
        <MenuItem Click="OnMenuItemClick" CommandParameter="NewSong" Header="_Song">
            <MenuItem.Icon>
                <material:MaterialIcon Kind="Music" />
            </MenuItem.Icon>
        </MenuItem>
```

Insert a new `MenuItem` immediately after it (before the `_Media` `MenuItem`):

```xml
        <MenuItem Click="OnMenuItemClick" CommandParameter="Scripture" Header="_Scripture">
            <MenuItem.Icon>
                <material:MaterialIcon Kind="Bible" />
            </MenuItem.Icon>
        </MenuItem>
```

(`MaterialIconKind.Bible` is confirmed present in the pinned `Material.Icons` `2.4.1` package this project actually restores — verified directly against `global-packages/material.icons/2.4.1/.../Material.Icons.dll` via reflection, not assumed from a newer version.)

- [ ] **Step 2: Special-case `Scripture` in `OnMenuItemClick`**

In `HandsLiftedApp.Core/Assets/AddItemFlyoutResourceDictionary.axaml.cs`, change the method signature from:

```csharp
        public void OnMenuItemClick(object? sender, RoutedEventArgs args)
```

to:

```csharp
        public async void OnMenuItemClick(object? sender, RoutedEventArgs args)
```

(Matches this codebase's existing convention for dialog-awaiting UI event handlers — see `LibraryQueryView.axaml.cs`'s `RenameItem_OnClick`, which is likewise `async void` around a `RenameDialog.ShowDialog`.)

Then, immediately after the existing `if (type == AddItemMessage.AddItemType.ExistingSong || type == AddItemMessage.AddItemType.NewSong) { ... return; }` block and before the generic `MessageBus.Current.SendMessage(new AddItemMessage { ... })` call, insert:

```csharp
            if (type == AddItemMessage.AddItemType.Scripture)
            {
                var parentWindow = TopLevel.GetTopLevel(menuItem) as Window;
                if (parentWindow == null) return;

                var dialog = new ScriptureAddDialog();
                await dialog.ShowDialog(parentWindow);

                if (dialog.Result == null) return;

                var result = dialog.Result.Value;
                MessageBus.Current.SendMessage(new AddItemMessage
                {
                    Type = type,
                    ItemToInsertAfter = nearestItem,
                    InsertIndex = itemInsertIndex,
                    ScriptureBookCode = result.BookCode,
                    ScriptureBookName = result.BookName,
                    ScriptureStartChapter = result.StartChapter,
                    ScriptureStartVerse = result.StartVerse,
                    ScriptureEndChapter = result.EndChapter,
                    ScriptureEndVerse = result.EndVerse
                });

                return;
            }
```

`TopLevel`, `Window`, and `MessageBus` are already in scope in this file (`using Avalonia.Controls;`, `using ReactiveUI;` are already present) — `ScriptureAddDialog` resolves via the existing `using HandsLiftedApp.Core.Views;` (already present in this file, since `AddItemWindow` — declared in that same namespace — is already used here) with no new `using` needed.

- [ ] **Step 3: Add the `Scripture` case to `MainViewModel`'s `AddItemMessage` subscriber**

In `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`, add `using HandsLiftedApp.Importer.Scripture;` to the file's using block (needed for `ScriptureUsxDownloader.FixedTranslation`; `ScriptureItemInstance` is already reachable via the existing `using HandsLiftedApp.Core.Models.RuntimeData.Items;`).

In the `MessageBus.Current.Listen<AddItemMessage>().Subscribe(...)` switch, immediately after the existing `case AddItemMessage.AddItemType.Comment: itemToInsert = new CommentItem(); break;` and before the commented-out `BibleReadingSlideGroup` block, insert:

```csharp
                    case AddItemMessage.AddItemType.Scripture:
                        var scriptureTitle = addItemMessage.ScriptureStartChapter == addItemMessage.ScriptureEndChapter &&
                                              addItemMessage.ScriptureStartVerse == addItemMessage.ScriptureEndVerse
                            ? $"{addItemMessage.ScriptureBookName} {addItemMessage.ScriptureStartChapter}:{addItemMessage.ScriptureStartVerse}"
                            : $"{addItemMessage.ScriptureBookName} {addItemMessage.ScriptureStartChapter}:{addItemMessage.ScriptureStartVerse}-{addItemMessage.ScriptureEndChapter}:{addItemMessage.ScriptureEndVerse}";

                        var scripture = new ScriptureItemInstance(Playlist)
                        {
                            Translation = ScriptureUsxDownloader.FixedTranslation,
                            Book = addItemMessage.ScriptureBookCode!,
                            StartChapter = addItemMessage.ScriptureStartChapter!.Value,
                            StartVerse = addItemMessage.ScriptureStartVerse!.Value,
                            EndChapter = addItemMessage.ScriptureEndChapter!.Value,
                            EndVerse = addItemMessage.ScriptureEndVerse!.Value,
                            Title = scriptureTitle
                        };
                        _ = scripture.GenerateSlidesAsync();
                        itemToInsert = scripture;
                        break;
```

The `!`s are safe: this case is only ever reached via the `AddItemMessage` this plan's own `OnMenuItemClick` branch constructs (Step 2), which always populates all 6 Scripture fields together from a confirmed dialog `Result` — there is no other code path in the app that sends `AddItemMessage { Type = AddItemType.Scripture }` with any of them null.

- [ ] **Step 4: Build and run the full test suite**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --nologo`
Expected: build succeeds, no XAML/compile errors.

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, same count as end of Task 3 (this task adds no tests) — confirms nothing else broke.

- [ ] **Step 5: Manual verification**

Run the app (check `docs/superpowers/HANDOVER.md` or the repo's build docs for the exact launch command if unsure). With a playlist open and some Bible data already downloaded via Setup (from the local-USX-source plan's "Download Bible Data" button):

1. Click the add-item button, confirm a new "Scripture" entry appears in the flyout (with a book icon) between "Song" and "Media".
2. Click it — confirm the "Add Scripture" dialog opens, centered on the main window, with "Genesis" pre-selected and all 4 chapter/verse fields showing `1`.
3. Change the book (e.g. to "John") and the range (e.g. Start Chapter 3, Start Verse 16, End Chapter 3, End Verse 21), click Insert — confirm a new item appears in the playlist's item list, labeled "John 3:16-21", and that selecting it and checking its slides shows real verse text (not a "Scripture data not found" placeholder, assuming John's data was downloaded).
4. Repeat, opening the dialog again and clicking Cancel instead — confirm no item is inserted and the dialog closes.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Assets/AddItemFlyoutResourceDictionary.axaml HandsLiftedApp.Core/Assets/AddItemFlyoutResourceDictionary.axaml.cs HandsLiftedApp.Core/ViewModels/MainViewModel.cs
git commit -m "feat: wire Scripture into the add-item flyout, dialog, and playlist insertion"
```

---

## Final Whole-Branch Review

After all 4 tasks: full suite should be at 136 tests (133 + 3 new `ScriptureBookCatalogTests`; Tasks 2–4 add none). Confirm `grep -rn "AddItemType.Scripture" --include=*.cs .` shows exactly the two sites this plan added (the `OnMenuItemClick` send and the `MainViewModel` case) plus the enum declaration itself.
