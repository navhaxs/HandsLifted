# Scripture Reference Text Entry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user type a scripture reference (`1 Peter 1:10-12`, `1 Peter 1:20-2:8`, `Rom 8:28`, `John 3`) into `ScriptureAddDialog` and have it resolve to a validated book/chapter/verse range, while keeping the existing spinner-based picker as a fallback mode.

**Architecture:** Two new pure, static, I/O-free classes (`ScriptureBookAliasCatalog` for book-name/abbreviation matching, `ScriptureReferenceParser` for the reference grammar) feed a rebuilt `ScriptureAddDialog` that adds a "Type"/"Pick" mode toggle. Typed input is debounced, parsed structurally, then validated against the real book data via the existing `ScriptureLocalUsxStore`. Both modes share one `ReferenceState` so switching modes carries the reference across.

**Tech Stack:** net10.0, MSTest, Avalonia 12.1.0 (`TextBox`, `RadioButton`, `NumericUpDown`, `ComboBox`).

## Global Constraints

- No changes to `ScriptureItem`'s persisted fields — still 4 plain ints (`StartChapter`, `StartVerse`, `EndChapter`, `EndVerse`) plus book code/name. This feature only changes how those ints get filled in during entry.
- No comma-separated / non-contiguous verse lists (e.g. `Rom 8:28,31-34`) — out of scope.
- No exhaustive abbreviation enumeration — one common abbreviation per book plus numeric-prefix variants (`1`/`I`/`First`, `2`/`II`/`Second`, `3`/`III`/`Third`) only.
- No new persisted user preference for "last used mode" — an in-memory static field for the current app session is sufficient, not saved to disk.
- No automated UI test for the dialog — consistent with this dialog's own prior plan (no Avalonia UI test harness exists in this codebase). Verified by build + full suite staying green, plus a manual click-through in Task 3.
- Current baseline: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo` passes 170 tests before this plan starts.

**Deviation from the design spec (final review, 2026-07-29):** the design spec's Pick→Type `FormatReference` example asks for whole-chapter ranges to collapse to `{Book} {Ch}` shorthand. This plan's own `FormatReference` never implemented that collapse (it only collapses same-verse and same-chapter cases) since doing so would require the formatter to consult real book data (the last verse number) that it doesn't have at that point. Accepted as a deliberate narrowing, not a bug — Pick mode round-trips a whole chapter as an explicit `John 3:1-36`-style range instead, which is arguably more explicit anyway.

---

### Task 1: `ScriptureBookAliasCatalog`

**Files:**
- Create: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureBookAliasCatalog.cs`
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureBookAliasCatalogTests.cs`

**Interfaces:**
- Consumes: `ScriptureBookCatalog.AllBooks` (`HandsLiftedApp.Core.Models.RuntimeData.Items`, already exists — 66 `(Code, Name)` entries, canonical order).
- Produces: `public static bool TryMatchBookPrefix(string input, out string bookCode, out string bookName, out int matchedLength)` — Task 2's parser calls this to find the book at the start of a typed reference string. `matchedLength` is the number of characters of `input` (not of any internal alias) that the match consumed, so the caller can safely do `input[matchedLength..]` to get the remainder.

This task is independent of Task 2 (which depends on it) and Task 3; it's new-file-only.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureBookAliasCatalogTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureBookAliasCatalogTests
{
    [TestMethod]
    public void TryMatchBookPrefix_FullName_Matches()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("Romans 8:28", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("rom", code);
        Assert.AreEqual("Romans", name);
        Assert.AreEqual(6, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_Abbreviation_Matches()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("Rom 8:28", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("rom", code);
        Assert.AreEqual("Romans", name);
        Assert.AreEqual(3, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_IsCaseInsensitive()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("ROMANS 8:28", out var code, out _, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("rom", code);
        Assert.AreEqual(6, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_NumericPrefixVariant_RomanNumeral_Matches()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("I Peter 1:10-12", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("1pe", code);
        Assert.AreEqual("1 Peter", name);
        Assert.AreEqual(7, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_NumericPrefixVariant_WordOrdinal_Matches()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("First Peter 1:10", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("1pe", code);
        Assert.AreEqual("1 Peter", name);
        Assert.AreEqual(11, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_DigitPrefixAbbreviation_Matches()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("1 Pet 1:10-12", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("1pe", code);
        Assert.AreEqual("1 Peter", name);
        Assert.AreEqual(5, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_PrefersLongestMatch_EpistleOverGospel()
    {
        // "1 John" (epistle, code 1jn) must win over "John" (gospel, code jhn) even though
        // "John" is also a valid alias that appears later in the same input.
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("1 John 3:16", out var code, out var name, out var len);
        Assert.IsTrue(ok);
        Assert.AreEqual("1jn", code);
        Assert.AreEqual("1 John", name);
        Assert.AreEqual(6, len);
    }

    [TestMethod]
    public void TryMatchBookPrefix_RejectsPartialWordMatch()
    {
        // "John" is a real alias (the Gospel), but it must not match as a prefix of the
        // unrelated word "Johnson" — the boundary check (next char must not be a letter)
        // is what makes that distinction, and no other alias matches "Johnson" either.
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("Johnson 3:1", out _, out _, out _);
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void TryMatchBookPrefix_UnknownBook_ReturnsFalse()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("Xyz 1:1", out _, out _, out _);
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void TryMatchBookPrefix_EmptyInput_ReturnsFalse()
    {
        var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix("", out _, out _, out _);
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void NoTwoDistinctBookCodesShareTheSameAliasText()
    {
        // Build the same alias set the catalog builds internally, by probing every book's
        // own name/abbreviation plus numeric-prefix variants against TryMatchBookPrefix,
        // and confirm each resolves back to its own code with a full-length match.
        foreach (var (code, name) in ScriptureBookCatalog.AllBooks)
        {
            var ok = ScriptureBookAliasCatalog.TryMatchBookPrefix(name, out var matchedCode, out _, out var len);
            Assert.IsTrue(ok, $"Full name '{name}' should match a book.");
            Assert.AreEqual(code, matchedCode, $"Full name '{name}' resolved to the wrong book code.");
            Assert.AreEqual(name.Length, len);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureBookAliasCatalogTests"`
Expected: FAIL — compile error, `ScriptureBookAliasCatalog` doesn't exist yet.

- [ ] **Step 3: Implement `ScriptureBookAliasCatalog`**

`HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureBookAliasCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public static class ScriptureBookAliasCatalog
    {
        // One common abbreviation per book, positionally parallel to ScriptureBookCatalog.AllBooks
        // (i.e. to ScriptureBookCatalog's own internal Names array, same 66-book canonical order).
        private static readonly string[] Abbreviations =
        {
            "Gen", "Exo", "Lev", "Num", "Deut", "Josh", "Judg", "Ruth", "1 Sam", "2 Sam",
            "1 Kgs", "2 Kgs", "1 Chr", "2 Chr", "Ezra", "Neh", "Esth", "Job", "Ps", "Prov",
            "Eccl", "Song", "Isa", "Jer", "Lam", "Ezek", "Dan", "Hos", "Joel", "Amos",
            "Obad", "Jonah", "Mic", "Nah", "Hab", "Zeph", "Hag", "Zech", "Mal",
            "Matt", "Mark", "Luke", "John", "Acts", "Rom", "1 Cor", "2 Cor", "Gal", "Eph",
            "Phil", "Col", "1 Thess", "2 Thess", "1 Tim", "2 Tim", "Titus", "Phlm", "Heb", "Jas",
            "1 Pet", "2 Pet", "1 Jn", "2 Jn", "3 Jn", "Jude", "Rev"
        };

        private static readonly IReadOnlyList<(string Alias, string Code)> AllAliases = BuildAliases();

        private static List<(string Alias, string Code)> BuildAliases()
        {
            var books = ScriptureBookCatalog.AllBooks;
            var result = new List<(string, string)>();

            for (var i = 0; i < books.Count; i++)
            {
                foreach (var variant in ExpandNumericVariants(books[i].Name))
                {
                    result.Add((variant, books[i].Code));
                }

                foreach (var variant in ExpandNumericVariants(Abbreviations[i]))
                {
                    result.Add((variant, books[i].Code));
                }
            }

            // Longest alias first: TryMatchBookPrefix takes the first match it finds, so a
            // longer, more specific alias (e.g. "1 John") must be tried before a shorter one
            // that could otherwise match a truncated prefix of it (e.g. "John").
            return result.OrderByDescending(a => a.Item1.Length).ToList();
        }

        private static IEnumerable<string> ExpandNumericVariants(string name)
        {
            yield return name;

            if (name.StartsWith("1 ", StringComparison.Ordinal))
            {
                yield return "I " + name[2..];
                yield return "First " + name[2..];
            }
            else if (name.StartsWith("2 ", StringComparison.Ordinal))
            {
                yield return "II " + name[2..];
                yield return "Second " + name[2..];
            }
            else if (name.StartsWith("3 ", StringComparison.Ordinal))
            {
                yield return "III " + name[2..];
                yield return "Third " + name[2..];
            }
        }

        public static bool TryMatchBookPrefix(string input, out string bookCode, out string bookName, out int matchedLength)
        {
            foreach (var (alias, code) in AllAliases)
            {
                if (input.Length < alias.Length) continue;
                if (!input.AsSpan(0, alias.Length).Equals(alias, StringComparison.OrdinalIgnoreCase)) continue;
                if (input.Length > alias.Length && char.IsLetter(input[alias.Length])) continue;

                bookCode = code;
                bookName = ScriptureBookCatalog.AllBooks.Single(b => b.Code == code).Name;
                matchedLength = alias.Length;
                return true;
            }

            bookCode = "";
            bookName = "";
            matchedLength = 0;
            return false;
        }
    }
}
```

The word-boundary check (`char.IsLetter(input[alias.Length])`) stops a short alias from matching as a prefix of a longer unrelated word (e.g. `"Jon"` inside `"Jonah"`) — `Jonah`'s own full-name alias is longer and gets tried first regardless, but the boundary check is what makes the shorter `Jon`-style alias safe to have in the list at all for any book that has one.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureBookAliasCatalogTests"`
Expected: PASS (11 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 170 + 11 = 181, no regressions.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureBookAliasCatalog.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureBookAliasCatalogTests.cs
git commit -m "feat: add ScriptureBookAliasCatalog for book-name/abbreviation matching"
```

---

### Task 2: `ScriptureReferenceParser`

**Files:**
- Create: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureReferenceParser.cs`
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureReferenceParserTests.cs`

**Interfaces:**
- Consumes: `ScriptureBookAliasCatalog.TryMatchBookPrefix` (Task 1).
- Produces:
  ```csharp
  public readonly record struct ParsedScriptureReference(
      string BookCode, string BookName, int StartChapter, int? StartVerse, int EndChapter, int? EndVerse);

  public static class ScriptureReferenceParser
  {
      public static bool TryParse(string input, out ParsedScriptureReference result, out string? error);
  }
  ```
  `StartVerse`/`EndVerse` are `null` only for a whole-chapter reference (e.g. `"John 3"`) — Task 3's dialog resolves the actual last verse number against real book data before accepting the reference. In every other case (single verse, same-chapter range, cross-chapter range) both are already fully resolved by the parser alone (no store lookup needed to know the numbers, only to confirm they exist).

This task depends on Task 1 (calls `ScriptureBookAliasCatalog.TryMatchBookPrefix`) and has no dependency on Task 3.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureReferenceParserTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureReferenceParserTests
{
    [TestMethod]
    public void TryParse_WholeChapter_Succeeds()
    {
        var ok = ScriptureReferenceParser.TryParse("John 3", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("jhn", result.BookCode);
        Assert.AreEqual(3, result.StartChapter);
        Assert.IsNull(result.StartVerse);
        Assert.AreEqual(3, result.EndChapter);
        Assert.IsNull(result.EndVerse);
    }

    [TestMethod]
    public void TryParse_SingleVerse_Succeeds()
    {
        var ok = ScriptureReferenceParser.TryParse("Rom 8:28", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("rom", result.BookCode);
        Assert.AreEqual(8, result.StartChapter);
        Assert.AreEqual(28, result.StartVerse);
        Assert.AreEqual(8, result.EndChapter);
        Assert.AreEqual(28, result.EndVerse);
    }

    [TestMethod]
    public void TryParse_SameChapterRange_Succeeds()
    {
        var ok = ScriptureReferenceParser.TryParse("1 Peter 1:10-12", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("1pe", result.BookCode);
        Assert.AreEqual(1, result.StartChapter);
        Assert.AreEqual(10, result.StartVerse);
        Assert.AreEqual(1, result.EndChapter);
        Assert.AreEqual(12, result.EndVerse);
    }

    [TestMethod]
    public void TryParse_CrossChapterRange_Succeeds()
    {
        var ok = ScriptureReferenceParser.TryParse("1 Peter 1:20-2:8", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("1pe", result.BookCode);
        Assert.AreEqual(1, result.StartChapter);
        Assert.AreEqual(20, result.StartVerse);
        Assert.AreEqual(2, result.EndChapter);
        Assert.AreEqual(8, result.EndVerse);
    }

    [TestMethod]
    public void TryParse_IsWhitespaceTolerant()
    {
        var ok = ScriptureReferenceParser.TryParse("  Romans   8 : 28  ", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("rom", result.BookCode);
        Assert.AreEqual(8, result.StartChapter);
        Assert.AreEqual(28, result.StartVerse);
    }

    [TestMethod]
    public void TryParse_NumericPrefixVariant_Succeeds()
    {
        var ok = ScriptureReferenceParser.TryParse("First Peter 1:10-12", out var result, out var error);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("1pe", result.BookCode);
        Assert.AreEqual(1, result.StartChapter);
        Assert.AreEqual(10, result.StartVerse);
        Assert.AreEqual(12, result.EndVerse);
    }

    [TestMethod]
    public void TryParse_UnknownBook_FailsWithBookNameInError()
    {
        var ok = ScriptureReferenceParser.TryParse("Xyz 1:1", out _, out var error);
        Assert.IsFalse(ok);
        StringAssert.Contains(error, "Xyz");
    }

    [TestMethod]
    public void TryParse_NoChapterAtAll_Fails()
    {
        var ok = ScriptureReferenceParser.TryParse("John", out _, out var error);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParse_GarbageAfterBook_Fails()
    {
        var ok = ScriptureReferenceParser.TryParse("John abc", out _, out var error);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParse_EmptyInput_Fails()
    {
        var ok = ScriptureReferenceParser.TryParse("", out _, out var error);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParse_WhitespaceOnlyInput_Fails()
    {
        var ok = ScriptureReferenceParser.TryParse("   ", out _, out var error);
        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureReferenceParserTests"`
Expected: FAIL — compile error, `ScriptureReferenceParser` doesn't exist yet.

- [ ] **Step 3: Implement `ScriptureReferenceParser`**

`HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureReferenceParser.cs`:

```csharp
using System.Text.RegularExpressions;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public readonly record struct ParsedScriptureReference(
        string BookCode,
        string BookName,
        int StartChapter,
        int? StartVerse,
        int EndChapter,
        int? EndVerse);

    public static class ScriptureReferenceParser
    {
        private const string GrammarHint = "Couldn't understand that. Try \"1 Peter 1:10-12\".";

        private static readonly Regex ReferencePattern = new(
            @"^(?<startChapter>\d+)\s*(?::\s*(?<startVerse>\d+)\s*(?:-\s*(?:(?<endChapter>\d+)\s*:\s*(?<endVerse>\d+)|(?<endVerseOnly>\d+)))?)?$",
            RegexOptions.Compiled);

        private static readonly Regex TrailingReferencePattern = new(
            @"\s+\d+(:\d+(-\d+(:\d+)?)?)?\s*$",
            RegexOptions.Compiled);

        public static bool TryParse(string input, out ParsedScriptureReference result, out string? error)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = GrammarHint;
                return false;
            }

            var trimmed = input.Trim();

            if (!ScriptureBookAliasCatalog.TryMatchBookPrefix(trimmed, out var bookCode, out var bookName, out var matchedLength))
            {
                error = $"Unknown book \"{ExtractLikelyBookToken(trimmed)}\".";
                return false;
            }

            var remainder = trimmed[matchedLength..].Trim();
            var match = ReferencePattern.Match(remainder);
            if (!match.Success)
            {
                error = GrammarHint;
                return false;
            }

            var startChapter = int.Parse(match.Groups["startChapter"].Value);
            int? startVerse = match.Groups["startVerse"].Success ? int.Parse(match.Groups["startVerse"].Value) : null;

            int endChapter;
            int? endVerse;

            if (startVerse is null)
            {
                // Whole chapter — caller resolves the actual last verse against real book data.
                endChapter = startChapter;
                endVerse = null;
            }
            else if (match.Groups["endChapter"].Success)
            {
                endChapter = int.Parse(match.Groups["endChapter"].Value);
                endVerse = int.Parse(match.Groups["endVerse"].Value);
            }
            else if (match.Groups["endVerseOnly"].Success)
            {
                endChapter = startChapter;
                endVerse = int.Parse(match.Groups["endVerseOnly"].Value);
            }
            else
            {
                // No dash at all — single verse, start == end.
                endChapter = startChapter;
                endVerse = startVerse;
            }

            result = new ParsedScriptureReference(bookCode, bookName, startChapter, startVerse, endChapter, endVerse);
            error = null;
            return true;
        }

        private static string ExtractLikelyBookToken(string trimmed)
        {
            var match = TrailingReferencePattern.Match(trimmed);
            return match.Success ? trimmed[..match.Index].Trim() : trimmed;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureReferenceParserTests"`
Expected: PASS (11 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 181 + 11 = 192, no regressions.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureReferenceParser.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureReferenceParserTests.cs
git commit -m "feat: add ScriptureReferenceParser for typed scripture reference grammar"
```

---

### Task 3: Rebuild `ScriptureAddDialog` with a Type/Pick mode toggle

**Files:**
- Modify: `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml`
- Modify: `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs`

**Interfaces:**
- Consumes: `ScriptureReferenceParser.TryParse` (Task 2), `ScriptureBookCatalog.AllBooks` (already exists), `ScriptureLocalUsxStore.LoadBookAsync` (already exists, `HandsLiftedApp.Importer.Scripture`), `Globals.Instance.AppPreferences.ScriptureDataPath` (already exists).
- Produces: same public surface as before — `public (string BookCode, string BookName, int StartChapter, int StartVerse, int EndChapter, int EndVerse)? Result { get; private set; }` — no change needed in `AddItemFlyoutResourceDictionary.axaml.cs`, which already consumes this exact shape.

This is the only UI task; it depends on Tasks 1 and 2.

- [ ] **Step 1: Replace the dialog XAML**

Replace the full contents of `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml` with:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Width="360"
        Height="360"
        WindowStartupLocation="CenterOwner"
        ExtendClientAreaToDecorationsHint="True"
        Background="Transparent"
        TransparencyLevelHint="Transparent"
        WindowDecorations="None"
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

                <StackPanel Orientation="Horizontal" Spacing="4">
                    <RadioButton x:Name="TypeModeRadio" GroupName="ScriptureEntryMode" Content="Type" Checked="OnModeChanged" />
                    <RadioButton x:Name="PickModeRadio" GroupName="ScriptureEntryMode" Content="Pick" Checked="OnModeChanged" />
                </StackPanel>

                <StackPanel x:Name="TypeModePanel" Spacing="4">
                    <TextBox x:Name="ReferenceTextBox" Watermark="e.g. 1 Peter 1:10-12" TextChanged="OnReferenceTextChanged" />
                    <TextBlock x:Name="ReferenceHintText" FontSize="11" TextWrapping="Wrap" MinHeight="28" />
                </StackPanel>

                <StackPanel x:Name="PickModePanel" Spacing="8">
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
            </StackPanel>
        </DockPanel>
    </Border>
</Window>
```

`TypeModePanel`/`PickModePanel` both start with default (visible) `IsVisible` in XAML — the code-behind sets the correct one to `false` as soon as it picks the initial mode in the constructor, so there's no visible flash of both panels.

- [ ] **Step 2: Replace the dialog code-behind**

Replace the full contents of `HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs` with:

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Core.Views
{
    public partial class ScriptureAddDialog : Window
    {
        private sealed class ReferenceState
        {
            public bool IsValid;
            public string? BookCode;
            public string? BookName;
            public int StartChapter;
            public int StartVerse;
            public int EndChapter;
            public int EndVerse;
        }

        // Remembered for the lifetime of the app process only — not a saved user preference.
        private static bool s_preferPickMode;

        private readonly ScriptureLocalUsxStore _store;
        private readonly ReferenceState _state = new();
        private CancellationTokenSource? _validationCts;
        private bool _initializing = true;

        public (string BookCode, string BookName, int StartChapter, int StartVerse, int EndChapter, int EndVerse)? Result { get; private set; }

        public ScriptureAddDialog(ScriptureLocalUsxStore? store = null)
        {
            InitializeComponent();
            _store = store ?? new ScriptureLocalUsxStore(Globals.Instance.AppPreferences.ScriptureDataPath);

            BookComboBox.ItemsSource = ScriptureBookCatalog.AllBooks.Select(b => b.Name).ToList();
            BookComboBox.SelectedIndex = 0;

            if (s_preferPickMode)
            {
                PickModeRadio.IsChecked = true;
            }
            else
            {
                TypeModeRadio.IsChecked = true;
            }

            _initializing = false;
        }

        private void OnModeChanged(object? sender, RoutedEventArgs e)
        {
            if (TypeModeRadio.IsChecked == true)
            {
                TypeModePanel.IsVisible = true;
                PickModePanel.IsVisible = false;

                if (!_initializing && TryReadPickModeValues(out var bookName, out var startChapter, out var startVerse, out var endChapter, out var endVerse))
                {
                    ReferenceTextBox.Text = FormatReference(bookName, startChapter, startVerse, endChapter, endVerse);
                }

                InsertButton.IsEnabled = !_initializing && _state.IsValid;
            }
            else
            {
                TypeModePanel.IsVisible = false;
                PickModePanel.IsVisible = true;

                if (!_initializing && _state.IsValid)
                {
                    var idx = ScriptureBookCatalog.AllBooks.ToList().FindIndex(b => b.Code == _state.BookCode);
                    if (idx >= 0)
                    {
                        BookComboBox.SelectedIndex = idx;
                        StartChapterUpDown.Value = _state.StartChapter;
                        StartVerseUpDown.Value = _state.StartVerse;
                        EndChapterUpDown.Value = _state.EndChapter;
                        EndVerseUpDown.Value = _state.EndVerse;
                    }
                }

                InsertButton.IsEnabled = true;
            }

            if (!_initializing)
            {
                s_preferPickMode = PickModeRadio.IsChecked == true;
            }
        }

        private bool TryReadPickModeValues(out string bookName, out int startChapter, out int startVerse, out int endChapter, out int endVerse)
        {
            bookName = "";
            startChapter = startVerse = endChapter = endVerse = 0;

            if (BookComboBox.SelectedIndex < 0) return false;
            if (StartChapterUpDown.Value is null || StartVerseUpDown.Value is null ||
                EndChapterUpDown.Value is null || EndVerseUpDown.Value is null) return false;

            bookName = ScriptureBookCatalog.AllBooks[BookComboBox.SelectedIndex].Name;
            startChapter = (int)StartChapterUpDown.Value.Value;
            startVerse = (int)StartVerseUpDown.Value.Value;
            endChapter = (int)EndChapterUpDown.Value.Value;
            endVerse = (int)EndVerseUpDown.Value.Value;
            return true;
        }

        private static string FormatReference(string bookName, int startChapter, int startVerse, int endChapter, int endVerse)
        {
            if (startChapter == endChapter && startVerse == endVerse)
            {
                return $"{bookName} {startChapter}:{startVerse}";
            }

            if (startChapter == endChapter)
            {
                return $"{bookName} {startChapter}:{startVerse}-{endVerse}";
            }

            return $"{bookName} {startChapter}:{startVerse}-{endChapter}:{endVerse}";
        }

        private void OnReferenceTextChanged(object? sender, TextChangedEventArgs e)
        {
            _validationCts?.Cancel();
            var cts = new CancellationTokenSource();
            _validationCts = cts;
            _ = ValidateTypedReferenceAsync(ReferenceTextBox.Text ?? "", cts.Token);
        }

        private async Task ValidateTypedReferenceAsync(string text, CancellationToken token)
        {
            try
            {
                await Task.Delay(300, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!ScriptureReferenceParser.TryParse(text, out var parsed, out var parseError))
            {
                SetInvalid(parseError!);
                return;
            }

            ScriptureBook book;
            try
            {
                SetChecking(parsed.BookName);
                book = await _store.LoadBookAsync(parsed.BookCode);
            }
            catch (Exception)
            {
                if (token.IsCancellationRequested) return;
                SetInvalid($"Couldn't load {parsed.BookName} — check scripture data path.");
                return;
            }

            if (token.IsCancellationRequested) return;

            var verses = book.Paragraphs.SelectMany(p => p.Verses).ToList();
            var chapters = verses.Select(v => v.Chapter).ToHashSet();

            if (!chapters.Contains(parsed.StartChapter) || (parsed.EndVerse is not null && !chapters.Contains(parsed.EndChapter)))
            {
                SetInvalid($"{parsed.BookName} has {chapters.Max()} chapters.");
                return;
            }

            var startChapterVerses = verses.Where(v => v.Chapter == parsed.StartChapter).Select(v => v.VerseNumber).ToList();
            var maxStartVerse = startChapterVerses.Max();

            if (parsed.StartVerse is not null && !startChapterVerses.Contains(parsed.StartVerse.Value))
            {
                SetInvalid($"{parsed.BookName} {parsed.StartChapter} has {maxStartVerse} verses.");
                return;
            }

            int resolvedStartVerse = parsed.StartVerse ?? 1;
            int resolvedEndVerse;

            if (parsed.EndVerse is null)
            {
                resolvedEndVerse = maxStartVerse;
            }
            else
            {
                var endChapterVerses = parsed.EndChapter == parsed.StartChapter
                    ? startChapterVerses
                    : verses.Where(v => v.Chapter == parsed.EndChapter).Select(v => v.VerseNumber).ToList();
                var maxEndVerse = endChapterVerses.Max();

                if (!endChapterVerses.Contains(parsed.EndVerse.Value))
                {
                    SetInvalid($"{parsed.BookName} {parsed.EndChapter} has {maxEndVerse} verses.");
                    return;
                }

                resolvedEndVerse = parsed.EndVerse.Value;
            }

            SetValid(parsed.BookCode, parsed.BookName, parsed.StartChapter, resolvedStartVerse, parsed.EndChapter, resolvedEndVerse);
        }

        private void SetChecking(string bookName)
        {
            ReferenceHintText.Text = $"Checking {bookName}…";
            ReferenceHintText.Foreground = Brushes.Gray;
        }

        private void SetInvalid(string message)
        {
            _state.IsValid = false;
            ReferenceHintText.Text = message;
            ReferenceHintText.Foreground = Brushes.IndianRed;
            InsertButton.IsEnabled = false;
        }

        private void SetValid(string bookCode, string bookName, int startChapter, int startVerse, int endChapter, int endVerse)
        {
            _state.IsValid = true;
            _state.BookCode = bookCode;
            _state.BookName = bookName;
            _state.StartChapter = startChapter;
            _state.StartVerse = startVerse;
            _state.EndChapter = endChapter;
            _state.EndVerse = endVerse;

            ReferenceHintText.Text = "";
            InsertButton.IsEnabled = true;
        }

        private void OnConfirmInsert(object? sender, RoutedEventArgs e)
        {
            if (PickModeRadio.IsChecked == true)
            {
                if (!TryReadPickModeValues(out var bookName, out var startChapter, out var startVerse, out var endChapter, out var endVerse)) return;
                var selected = ScriptureBookCatalog.AllBooks[BookComboBox.SelectedIndex];
                Result = (selected.Code, bookName, startChapter, startVerse, endChapter, endVerse);
                Close();
                return;
            }

            if (!_state.IsValid) return;
            Result = (_state.BookCode!, _state.BookName!, _state.StartChapter, _state.StartVerse, _state.EndChapter, _state.EndVerse);
            Close();
        }

        private void OnCancel(object? sender, RoutedEventArgs e) => Close();
    }
}
```

A few things worth noting for whoever implements this:

- `_initializing` exists purely so the constructor's `TypeModeRadio.IsChecked = true` (or `PickModeRadio.IsChecked = true`) — which fires `OnModeChanged` via the `Checked` event — doesn't try to format an empty reference into the text box or read `_state` before it has any data. Without it you'd see a spurious `Genesis 1:1`-shaped string appear in the text box on every dialog open in Type mode.
- `ScriptureBook` and `ScriptureVerseSegment` (used via `book.Paragraphs.SelectMany(p => p.Verses)`) both live in `HandsLiftedApp.Importer.Scripture.Models`, which is already reachable here because `ScriptureLocalUsxStore` (in `HandsLiftedApp.Importer.Scripture`, `using`d above) returns `ScriptureBook` from that models namespace — C# resolves the return type without needing a separate `using` for the call site to compile, but IDE tooling may still want you to add `using HandsLiftedApp.Importer.Scripture.Models;` explicitly for the local `ScriptureBook book` declaration. Add it if the build complains.
- `Globals` (used via `Globals.Instance.AppPreferences.ScriptureDataPath`) is in the enclosing `HandsLiftedApp.Core` namespace and resolves unqualified from this file's `HandsLiftedApp.Core.Views` namespace with no extra `using` needed.
- Pick-mode values are only ever read at the moment they're needed (mode switch to Type, or Insert click) — there's no `ValueChanged`/`SelectionChanged` wiring on the spinners/combo, matching the original dialog's simplicity. Pick mode has no "invalid" state, same as before this change.
- Reversed ranges (e.g. typing `1 Peter 3:5-1:2`) are not rejected by this validation — same accepted non-goal as the original dialog's picker mode (`ScriptureVerseRangeExtractor` degrades to zero slides for a bad range either way).

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --nologo`
Expected: builds with 0 errors. If it complains about `ScriptureBook` being ambiguous or unresolved, add `using HandsLiftedApp.Importer.Scripture.Models;` to the top of the file as noted above.

- [ ] **Step 4: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, same count as end of Task 2 (192) — this task adds no automated tests, no regressions.

- [ ] **Step 5: Manual verification**

Run the app (check `docs/superpowers/HANDOVER.md` or the repo's build docs for the exact launch command if unsure). With a playlist open and at least Romans, John, and 1 Peter downloaded via Setup's "Download Bible Data":

1. Open the add-item flyout, click "Scripture" — dialog opens in **Type** mode by default (first-ever open of the app process), text box empty with watermark `e.g. 1 Peter 1:10-12`, Insert disabled.
2. Type `1 Peter 1:10-12` — after a brief pause, hint text clears and Insert becomes enabled. Click Insert — confirm a new playlist item labeled `1 Peter 1:10-12` appears with real verse text on its slides.
3. Reopen the dialog, type `Rom 8:28` — confirm it resolves (abbreviation works) and Insert enables.
4. Type `1 Peter 1:20-2:8` — confirm cross-chapter range resolves.
5. Type `John 3` — confirm whole-chapter resolves (no error), and after Insert the resulting item's slides cover all of John 3, ending at its actual last verse.
6. Type `Xyz 1:1` — confirm the hint shows `Unknown book "Xyz".` and Insert stays disabled.
7. Type `John 3:9999` — confirm the hint shows a "has N verses" message and Insert stays disabled.
8. Type `1 Peter 1:10-12`, then click the **Pick** radio — confirm the book combo jumps to "1 Peter" and all 4 spinners show 1/10/1/12.
9. With Pick mode active, change the book/spinners to something else (e.g. John 3:16), then click the **Type** radio — confirm the text box now shows `John 3:16`.
10. Close and reopen the dialog — confirm it remembers the mode you were last in (from step 9, that's Type).
11. Cancel from either mode — confirm no item is inserted.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml HandsLiftedApp.Core/Views/AddItem/ScriptureAddDialog.axaml.cs
git commit -m "feat: add typed scripture reference entry to ScriptureAddDialog"
```

---

## Final Whole-Branch Review

After all 3 tasks: full suite should be at 192 tests (170 baseline + 11 `ScriptureBookAliasCatalogTests` + 11 `ScriptureReferenceParserTests`; Task 3 adds none). Confirm `ScriptureAddDialog`'s public `Result` shape is byte-for-byte unchanged from before this plan (`AddItemFlyoutResourceDictionary.axaml.cs` should need zero changes). Confirm the manual click-through in Task 3 Step 5 was actually run in a live app window, not skipped — this dialog has no automated UI test, so that walkthrough is the only verification its interactive behavior gets.
