# Scripture Parser + Loader (Phase 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a new `HandsLiftedApp.Importer.Scripture` project that can fetch USX 3.0 scripture XML and parse it into a clean, verse-addressable model — the foundation phase 2 (data model/slide generation), phase 3 (render wiring), and phase 4 (editor UI) all build on.

**Architecture:** Pure C# class library, no Avalonia dependency. Ports the parsing approach from MyBibleApp's `UsxBibleParser.cs` (`C:\Users\Jeremy\RiderProjects\MyBibleApp\MyBibleApp\Services\UsxBibleParser.cs`) and `UsxBibleApiLoader.cs`, but restructured to emit **one record per verse** (`ScriptureVerseSegment`) grouped into **paragraphs matching the source USX's own `<para>` boundaries** — not MyBibleApp's paragraph-with-embedded-superscript-numbers shape, which doesn't serve HandsLifted's slide-per-passage use case. This work happens in a git worktree off HandsLifted's `master` per the approved design spec (`docs/superpowers/specs/2026-07-26-scripture-slide-type-design.md`); the worktree itself is set up at execution time via the `using-git-worktrees` skill, not as a task here.

**Tech Stack:** .NET 8, MSTest (matches `HandsLiftedApp.Tests` convention), `System.Xml.Linq`, `System.Net.Http` — all BCL, no new NuGet packages required.

## Global Constraints

- Target framework: `net8.0` (matches `HandsLiftedApp.Core.csproj`/`HandsLiftedApp.Data.csproj`, confirmed via `Directory.Build.props` + individual csproj TFM checks).
- Test framework: MSTest (`[TestClass]`/`[TestMethod]`/`Assert.*`) — matches every existing file under `HandsLiftedApp.Tests/`, not xUnit (xUnit packages exist centrally but are unused by the live test project).
- Namespace = project name exactly (e.g. `HandsLiftedApp.Importer.PDF` project → `namespace HandsLiftedApp.Importer.PDF`) — established convention, confirmed in `HandsLiftedApp.Importer.PDF/ConvertPDF.cs:6`.
- No Avalonia package reference in this project — nothing in this phase touches Avalonia types.
- `HandsLiftedApp.Importer.Scripture` is NOT referenced by `HandsLiftedApp.Core.csproj` in this phase — that reference is added in Phase 2 when `ScriptureItemInstance` actually consumes it. Adding an unused reference now would be dead weight.

---

### Task 1: Scaffold project + models

**Files:**
- Create: `HandsLiftedApp.Importer.Scripture/HandsLiftedApp.Importer.Scripture.csproj`
- Create: `HandsLiftedApp.Importer.Scripture/Models/ScriptureFootnote.cs`
- Create: `HandsLiftedApp.Importer.Scripture/Models/ScriptureVerseSegment.cs`
- Create: `HandsLiftedApp.Importer.Scripture/Models/ScriptureParagraph.cs`
- Create: `HandsLiftedApp.Importer.Scripture/Models/ScriptureBook.cs`
- Modify: `HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj` (add project reference)
- Test: `HandsLiftedApp.Tests/Importer/Scripture/Models/ScriptureModelsTests.cs`

**Interfaces:**
- Produces: `ScriptureFootnote(string Marker, string Text)`, `ScriptureVerseSegment(int VerseNumber, string Text, IReadOnlyList<ScriptureFootnote> Footnotes)`, `ScriptureParagraph(int StartChapter, bool IsPoetry, int PoetryIndentLevel, IReadOnlyList<ScriptureVerseSegment> Verses)`, `ScriptureBook(string Code, string Title, IReadOnlyList<ScriptureParagraph> Paragraphs)` — all in namespace `HandsLiftedApp.Importer.Scripture.Models`. Task 2 (parser) and Task 3 (loader) construct these directly.

- [ ] **Step 1: Create the project file**

`HandsLiftedApp.Importer.Scripture/HandsLiftedApp.Importer.Scripture.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

- [ ] **Step 2: Register the project in the solution and reference it from Tests**

Run:
```bash
dotnet sln HandsLiftedApp.sln add HandsLiftedApp.Importer.Scripture/HandsLiftedApp.Importer.Scripture.csproj
dotnet add HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj reference HandsLiftedApp.Importer.Scripture/HandsLiftedApp.Importer.Scripture.csproj
```
Expected: both commands report success; `HandsLiftedApp.Tests.csproj`'s `<ItemGroup>` of `<ProjectReference>` now includes `..\HandsLiftedApp.Importer.Scripture\HandsLiftedApp.Importer.Scripture.csproj`.

- [ ] **Step 3: Write the failing test for the models**

`HandsLiftedApp.Tests/Importer/Scripture/Models/ScriptureModelsTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.Scripture.Models;

namespace HandsLiftedApp.Tests.Importer.Scripture.Models;

[TestClass]
public class ScriptureModelsTests
{
    [TestMethod]
    public void ScriptureBook_ExposesConstructorArguments()
    {
        var footnote = new ScriptureFootnote("1", "Cited in 2 Corinthians 4:6");
        var verse = new ScriptureVerseSegment(3, "And God said, “Let there be light,”", new[] { footnote });
        var paragraph = new ScriptureParagraph(1, IsPoetry: false, PoetryIndentLevel: 0, new[] { verse });
        var book = new ScriptureBook("GEN", "Genesis", new[] { paragraph });

        Assert.AreEqual("GEN", book.Code);
        Assert.AreEqual("Genesis", book.Title);
        Assert.AreEqual(1, book.Paragraphs.Count);
        Assert.AreEqual(1, book.Paragraphs[0].StartChapter);
        Assert.IsFalse(book.Paragraphs[0].IsPoetry);
        Assert.AreEqual(1, book.Paragraphs[0].Verses.Count);
        Assert.AreEqual(3, book.Paragraphs[0].Verses[0].VerseNumber);
        Assert.AreEqual("And God said, “Let there be light,”", book.Paragraphs[0].Verses[0].Text);
        Assert.AreEqual(1, book.Paragraphs[0].Verses[0].Footnotes.Count);
        Assert.AreEqual("Cited in 2 Corinthians 4:6", book.Paragraphs[0].Verses[0].Footnotes[0].Text);
    }

    [TestMethod]
    public void ScriptureParagraph_TracksPoetryIndentLevel()
    {
        var verse = new ScriptureVerseSegment(27, "So God created man in His own image;", System.Array.Empty<ScriptureFootnote>());
        var paragraph = new ScriptureParagraph(1, IsPoetry: true, PoetryIndentLevel: 2, new[] { verse });

        Assert.IsTrue(paragraph.IsPoetry);
        Assert.AreEqual(2, paragraph.PoetryIndentLevel);
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureModelsTests"`
Expected: FAIL — compile error, `HandsLiftedApp.Importer.Scripture.Models` namespace/types don't exist yet.

- [ ] **Step 5: Implement the models**

`HandsLiftedApp.Importer.Scripture/Models/ScriptureFootnote.cs`:

```csharp
namespace HandsLiftedApp.Importer.Scripture.Models;

public sealed record ScriptureFootnote(string Marker, string Text);
```

`HandsLiftedApp.Importer.Scripture/Models/ScriptureVerseSegment.cs`:

```csharp
using System.Collections.Generic;

namespace HandsLiftedApp.Importer.Scripture.Models;

public sealed record ScriptureVerseSegment(int VerseNumber, string Text, IReadOnlyList<ScriptureFootnote> Footnotes);
```

`HandsLiftedApp.Importer.Scripture/Models/ScriptureParagraph.cs`:

```csharp
using System.Collections.Generic;

namespace HandsLiftedApp.Importer.Scripture.Models;

public sealed record ScriptureParagraph(int StartChapter, bool IsPoetry, int PoetryIndentLevel, IReadOnlyList<ScriptureVerseSegment> Verses);
```

`HandsLiftedApp.Importer.Scripture/Models/ScriptureBook.cs`:

```csharp
using System.Collections.Generic;

namespace HandsLiftedApp.Importer.Scripture.Models;

public sealed class ScriptureBook
{
    public ScriptureBook(string code, string title, IReadOnlyList<ScriptureParagraph> paragraphs)
    {
        Code = code;
        Title = title;
        Paragraphs = paragraphs;
    }

    public string Code { get; }

    public string Title { get; }

    public IReadOnlyList<ScriptureParagraph> Paragraphs { get; }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureModelsTests"`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Importer.Scripture HandsLiftedApp.Tests HandsLiftedApp.sln
git commit -m "feat: scaffold HandsLiftedApp.Importer.Scripture project with scripture models"
```

---

### Task 2: USX parser (`UsxScriptureParser`)

**Files:**
- Create: `HandsLiftedApp.Importer.Scripture/UsxScriptureParser.cs`
- Test: `HandsLiftedApp.Tests/Importer/Scripture/UsxScriptureParserTests.cs`

**Interfaces:**
- Consumes: `ScriptureBook`/`ScriptureParagraph`/`ScriptureVerseSegment`/`ScriptureFootnote` from Task 1 (`HandsLiftedApp.Importer.Scripture.Models`).
- Produces: `public static class UsxScriptureParser { public static ScriptureBook Parse(System.Xml.Linq.XDocument document) }` — Task 3 (loader) calls `UsxScriptureParser.Parse(document)`.

**Design note (deviation from source app, deliberate):** MyBibleApp's `UsxBibleParser` reads the book title from the `<book>` element's own text — but real BSB USX (fetched from `v1.fetch.bible`) puts a generator credit string there (`"Autogenerated BSB by bsb2usfm"`), not the book name; the actual title lives in the `<para style="mt1">` element. This parser prefers `mt1` text, falling back to the `<book>` element's text, falling back to the book code.

**Design note (verse-spans-paragraphs handling):** Real USX can split one verse's text across multiple `<para>` elements (e.g. a poetry paragraph break, or a narrative paragraph break mid-verse, marked with a `vid` attribute on the continuation `<para>` — see the BSB fixture in Step 1 below). Rather than trying to merge continuation paragraphs back together, this parser keeps each source `<para>` as its own `ScriptureParagraph` (preserving the source's intended visual line/paragraph breaks — this matters for poetry) and simply emits a second `ScriptureVerseSegment` with the same `VerseNumber` in the next paragraph. Downstream slide-building code decides how to handle same-numbered segments across adjacent paragraphs; the parser does not merge them.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Importer/Scripture/UsxScriptureParserTests.cs`:

```csharp
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Importer.Scripture;

[TestClass]
public class UsxScriptureParserTests
{
    private const string SimpleUsx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <usx version="3.0">
          <book code="GEN" style="id">- Genesis</book>
          <para style="mt1">Genesis</para>
          <chapter number="1" style="c" sid="GEN 1"/>
          <para style="p">
            <verse number="1" style="v" sid="GEN 1:1"/>In the beginning God created the heaven and the earth.<verse eid="GEN 1:1"/>
            <verse number="2" style="v" sid="GEN 1:2"/>And the earth was without form, and void.<verse eid="GEN 1:2"/>
          </para>
          <chapter eid="GEN 1"/>
        </usx>
        """;

    // Mirrors the real shape fetched from v1.fetch.bible/bibles/eng_bsb/usx/gen.usx:
    // book element holds a generator credit (not the title), a section heading (s2) that
    // must be skipped, a footnote on verse 3, and verse 5 + verse 27's text split across
    // multiple <para> elements via the `vid` continuation attribute.
    private const string BsbShapedUsx = """
        <?xml version="1.0" encoding="utf-8"?>
        <usx version="3.0">
          <book code="GEN" style="id">Autogenerated BSB by bsb2usfm</book>
          <para style="mt1">Genesis</para>
          <chapter number="1" style="c" sid="GEN 1" />
          <para style="s2">The First Day</para>
          <para style="p">
            <verse number="3" style="v" sid="GEN 1:3" />And God said, &#8220;Let there be light,&#8221;<note caller="+" style="f"><char style="fr" closed="false">1:3 </char><char style="ft" closed="false">Cited in 2 Corinthians 4:6</char></note> and there was light. <verse eid="GEN 1:3" /><verse number="4" style="v" sid="GEN 1:4" />And God saw that the light was good.<verse eid="GEN 1:4" /><verse number="5" style="v" sid="GEN 1:5" />God called the light &#8220;day,&#8221; and the darkness He called &#8220;night.&#8221;</para>
          <para style="p" vid="GEN 1:5">And there was evening, and there was morning — the first day.<verse eid="GEN 1:5" /></para>
          <para style="b" />
          <para style="q1">
            <verse number="27" style="v" sid="GEN 1:27" />So God created man in His own image;</para>
          <para style="q2" vid="GEN 1:27">in the image of God He created him;</para>
          <para style="q2" vid="GEN 1:27">male and female He created them.<verse eid="GEN 1:27" /></para>
          <chapter eid="GEN 1" />
        </usx>
        """;

    [TestMethod]
    public void Parse_SimpleUsx_ReturnsBookCodeAndTitle()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(SimpleUsx));

        Assert.AreEqual("GEN", book.Code);
        Assert.AreEqual("Genesis", book.Title);
    }

    [TestMethod]
    public void Parse_SimpleUsx_GroupsBothVersesIntoOneParagraph()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(SimpleUsx));

        Assert.AreEqual(1, book.Paragraphs.Count);
        var paragraph = book.Paragraphs[0];
        Assert.AreEqual(1, paragraph.StartChapter);
        Assert.IsFalse(paragraph.IsPoetry);
        Assert.AreEqual(2, paragraph.Verses.Count);
        Assert.AreEqual(1, paragraph.Verses[0].VerseNumber);
        Assert.AreEqual("In the beginning God created the heaven and the earth.", paragraph.Verses[0].Text);
        Assert.AreEqual(2, paragraph.Verses[1].VerseNumber);
        Assert.AreEqual("And the earth was without form, and void.", paragraph.Verses[1].Text);
    }

    [TestMethod]
    public void Parse_BsbShapedUsx_PrefersMt1TitleOverBookElementText()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(BsbShapedUsx));

        Assert.AreEqual("GEN", book.Code);
        Assert.AreEqual("Genesis", book.Title);
    }

    [TestMethod]
    public void Parse_BsbShapedUsx_SkipsSectionHeadingAndEmptyParagraph()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(BsbShapedUsx));

        // 5 kept paragraphs: [v3-5a], [v5b continuation], [v27a q1], [v27b q2], [v27c q2].
        // "The First Day" (s2) and the empty <para style="b" /> must not appear.
        Assert.AreEqual(5, book.Paragraphs.Count);
        foreach (var paragraph in book.Paragraphs)
        {
            Assert.IsTrue(paragraph.Verses.Count > 0);
        }
    }

    [TestMethod]
    public void Parse_BsbShapedUsx_CapturesFootnoteOnVerseThree()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(BsbShapedUsx));

        var firstParagraph = book.Paragraphs[0];
        Assert.AreEqual(3, firstParagraph.Verses[0].VerseNumber);
        Assert.AreEqual(1, firstParagraph.Verses[0].Footnotes.Count);
        Assert.AreEqual("Cited in 2 Corinthians 4:6", firstParagraph.Verses[0].Footnotes[0].Text);
    }

    [TestMethod]
    public void Parse_BsbShapedUsx_SplitsVerseFiveTextAcrossTwoParagraphs()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(BsbShapedUsx));

        var firstParagraph = book.Paragraphs[0];
        Assert.AreEqual(5, firstParagraph.Verses[2].VerseNumber);
        Assert.AreEqual("God called the light “day,” and the darkness He called “night.”", firstParagraph.Verses[2].Text);

        var continuationParagraph = book.Paragraphs[1];
        Assert.AreEqual(1, continuationParagraph.Verses.Count);
        Assert.AreEqual(5, continuationParagraph.Verses[0].VerseNumber);
        Assert.AreEqual("And there was evening, and there was morning — the first day.", continuationParagraph.Verses[0].Text);
    }

    [TestMethod]
    public void Parse_BsbShapedUsx_TracksPoetryAcrossContinuationParagraphs()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(BsbShapedUsx));

        var q1Paragraph = book.Paragraphs[2];
        Assert.IsTrue(q1Paragraph.IsPoetry);
        Assert.AreEqual(1, q1Paragraph.PoetryIndentLevel);
        Assert.AreEqual(27, q1Paragraph.Verses[0].VerseNumber);
        Assert.AreEqual("So God created man in His own image;", q1Paragraph.Verses[0].Text);

        var q2ParagraphA = book.Paragraphs[3];
        Assert.AreEqual(2, q2ParagraphA.PoetryIndentLevel);
        Assert.AreEqual(27, q2ParagraphA.Verses[0].VerseNumber);
        Assert.AreEqual("in the image of God He created him;", q2ParagraphA.Verses[0].Text);

        var q2ParagraphB = book.Paragraphs[4];
        Assert.AreEqual(27, q2ParagraphB.Verses[0].VerseNumber);
        Assert.AreEqual("male and female He created them.", q2ParagraphB.Verses[0].Text);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~UsxScriptureParserTests"`
Expected: FAIL — compile error, `HandsLiftedApp.Importer.Scripture.UsxScriptureParser` doesn't exist yet.

- [ ] **Step 3: Implement the parser**

`HandsLiftedApp.Importer.Scripture/UsxScriptureParser.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HandsLiftedApp.Importer.Scripture.Models;

namespace HandsLiftedApp.Importer.Scripture;

public static class UsxScriptureParser
{
    public static ScriptureBook Parse(XDocument document)
    {
        var root = document.Root ?? throw new InvalidOperationException("USX document does not have a root element.");
        var bookElement = root.Elements().FirstOrDefault(x => x.Name.LocalName == "book");
        var mt1Element = root.Elements().FirstOrDefault(x =>
            x.Name.LocalName == "para" && x.Attribute("style")?.Value == "mt1");

        var code = bookElement?.Attribute("code")?.Value ?? "UNK";

        var titleText = CollapseWhitespace(mt1Element?.Value) is { Length: > 0 } mt1Title
            ? mt1Title
            : CollapseWhitespace(bookElement?.Value);
        var title = string.IsNullOrEmpty(titleText) ? code : titleText;

        var paragraphs = new List<ScriptureParagraph>();
        var currentChapter = 1;
        var accumulator = new ParagraphAccumulator();

        foreach (var element in root.Elements())
        {
            if (element.Name.LocalName == "chapter" && element.Attribute("number") is not null)
            {
                if (int.TryParse(element.Attribute("number")?.Value, out var parsedChapter))
                {
                    currentChapter = parsedChapter;
                }

                continue;
            }

            if (element.Name.LocalName != "para")
            {
                continue;
            }

            var style = element.Attribute("style")?.Value;
            if (IsNonVerseStyle(style))
            {
                continue;
            }

            foreach (var node in element.Nodes())
            {
                AppendNode(node, accumulator);
            }

            accumulator.Flush();

            if (accumulator.Verses.Count > 0)
            {
                paragraphs.Add(new ScriptureParagraph(
                    currentChapter,
                    IsPoetryStyle(style),
                    GetPoetryIndentLevel(style),
                    accumulator.Verses.ToList()));
            }

            accumulator.Verses.Clear();
        }

        return new ScriptureBook(code, title, paragraphs);
    }

    private static bool IsNonVerseStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return false;
        }

        // Front matter, navigation metadata, and section headings are not scripture text.
        return style.Equals("h", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("toc", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("mt", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("imt", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("s", StringComparison.OrdinalIgnoreCase)
            || style is "d" or "mr" or "ms" or "r" or "usfm";
    }

    private static bool IsPoetryStyle(string? style) => style is "q1" or "q2" or "q3" or "qa" or "qr" or "qc";

    private static int GetPoetryIndentLevel(string? style) => style switch
    {
        "q1" or "qa" or "qc" => 1,
        "q2" or "qr" => 2,
        "q3" => 3,
        _ => 0
    };

    private static void AppendNode(XNode node, ParagraphAccumulator accumulator)
    {
        if (node is XText textNode)
        {
            accumulator.AppendText(CollapseWhitespace(textNode.Value));
            return;
        }

        if (node is not XElement element)
        {
            return;
        }

        if (element.Name.LocalName == "verse")
        {
            if (element.Attribute("eid") is not null)
            {
                return;
            }

            var verseNumber = element.Attribute("number")?.Value;
            if (string.IsNullOrWhiteSpace(verseNumber) || !int.TryParse(verseNumber, out var parsedVerse))
            {
                return;
            }

            accumulator.StartVerse(parsedVerse);
            return;
        }

        if (element.Name.LocalName == "note")
        {
            var footnoteText = ExtractFootnoteText(element);
            if (!string.IsNullOrWhiteSpace(footnoteText))
            {
                accumulator.AddFootnote(footnoteText);
            }

            return;
        }

        foreach (var child in element.Nodes())
        {
            AppendNode(child, accumulator);
        }
    }

    private static string ExtractFootnoteText(XElement noteElement)
    {
        var ftChunks = noteElement
            .Descendants()
            .Where(x => x.Name.LocalName == "char" && x.Attribute("style")?.Value == "ft")
            .Select(x => CollapseWhitespace(x.Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return ftChunks.Count > 0 ? string.Join(' ', ftChunks) : CollapseWhitespace(noteElement.Value);
    }

    private static string CollapseWhitespace(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return string.Join(' ', input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsClosingPunctuation(char value) =>
        value is '.' or ',' or ';' or ':' or '!' or '?' or ')' or ']' or '}' or '"' or '\'';

    // Carries verse-number state across paragraph boundaries (a verse's text can span
    // multiple <para> elements — see Parse_BsbShapedUsx_SplitsVerseFiveTextAcrossTwoParagraphs)
    // while collecting only the current paragraph's segments in Verses.
    private sealed class ParagraphAccumulator
    {
        private readonly StringBuilder _text = new();
        private List<ScriptureFootnote> _footnotes = new();
        private int _currentVerse;

        public List<ScriptureVerseSegment> Verses { get; } = new();

        public void AppendText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (_text.Length > 0 && !char.IsWhiteSpace(_text[^1]) && !IsClosingPunctuation(text[0]))
            {
                _text.Append(' ');
            }

            _text.Append(text);
        }

        public void StartVerse(int verseNumber)
        {
            Flush();
            _currentVerse = verseNumber;
        }

        public void AddFootnote(string text)
        {
            _footnotes.Add(new ScriptureFootnote((_footnotes.Count + 1).ToString(), text));
        }

        public void Flush()
        {
            if (_currentVerse > 0 && _text.Length > 0)
            {
                Verses.Add(new ScriptureVerseSegment(_currentVerse, _text.ToString().Trim(), _footnotes));
                _footnotes = new List<ScriptureFootnote>();
            }

            _text.Clear();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~UsxScriptureParserTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Importer.Scripture/UsxScriptureParser.cs HandsLiftedApp.Tests/Importer/Scripture/UsxScriptureParserTests.cs
git commit -m "feat: add UsxScriptureParser producing verse-addressable paragraphs"
```

---

### Task 3: Fetch + cache loader (`ScriptureSourceLoader`)

**Files:**
- Create: `HandsLiftedApp.Importer.Scripture/ScriptureSourceLoader.cs`
- Test: `HandsLiftedApp.Tests/Importer/Scripture/ScriptureSourceLoaderTests.cs`
- Test helper: `HandsLiftedApp.Tests/Importer/Scripture/FakeHttpMessageHandler.cs`

**Interfaces:**
- Consumes: `UsxScriptureParser.Parse(XDocument)` from Task 2; `ScriptureBook` from Task 1.
- Produces: `public sealed class ScriptureSourceLoader { public ScriptureSourceLoader(HttpClient? httpClient = null, string? cacheRoot = null); public Task<ScriptureBook> LoadBookAsync(string translation, string bookCode); }` — Phase 2's `ScriptureItemInstance` will construct this with defaults (real `HttpClient`, real AppData cache path) and call `LoadBookAsync`.

- [ ] **Step 1: Write the test helper**

`HandsLiftedApp.Tests/Importer/Scripture/FakeHttpMessageHandler.cs`:

```csharp
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HandsLiftedApp.Tests.Importer.Scripture;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public int CallCount { get; private set; }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_respond(request));
    }
}
```

- [ ] **Step 2: Write the failing tests**

`HandsLiftedApp.Tests/Importer/Scripture/ScriptureSourceLoaderTests.cs`:

```csharp
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Importer.Scripture;

[TestClass]
public class ScriptureSourceLoaderTests
{
    private const string GenesisUsx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <usx version="3.0">
          <book code="GEN" style="id">- Genesis</book>
          <para style="mt1">Genesis</para>
          <chapter number="1" style="c" sid="GEN 1"/>
          <para style="p">
            <verse number="1" style="v" sid="GEN 1:1"/>In the beginning God created the heaven and the earth.<verse eid="GEN 1:1"/>
          </para>
          <chapter eid="GEN 1"/>
        </usx>
        """;

    private string _tempCacheRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempCacheRoot = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureTests_" + Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempCacheRoot))
        {
            Directory.Delete(_tempCacheRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadBookAsync_FetchesAndParsesFromHttp()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            Assert.AreEqual("https://v1.fetch.bible/bibles/eng_bsb/usx/gen.usx", request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(GenesisUsx) };
        });
        var loader = new ScriptureSourceLoader(new HttpClient(handler), _tempCacheRoot);

        var book = await loader.LoadBookAsync("eng_bsb", "gen");

        Assert.AreEqual("GEN", book.Code);
        Assert.AreEqual("Genesis", book.Title);
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task LoadBookAsync_SecondCallForSameBook_UsesMemoryCache()
    {
        var handler = new FakeHttpMessageHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(GenesisUsx) });
        var loader = new ScriptureSourceLoader(new HttpClient(handler), _tempCacheRoot);

        await loader.LoadBookAsync("eng_bsb", "gen");
        await loader.LoadBookAsync("eng_bsb", "gen");

        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task LoadBookAsync_DiskCacheHit_NeverCallsHttp()
    {
        var cacheDir = Path.Combine(_tempCacheRoot, "eng_bsb");
        Directory.CreateDirectory(cacheDir);
        await File.WriteAllTextAsync(Path.Combine(cacheDir, "gen.usx"), GenesisUsx);

        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called on a disk-cache hit."));
        var loader = new ScriptureSourceLoader(new HttpClient(handler), _tempCacheRoot);

        var book = await loader.LoadBookAsync("eng_bsb", "gen");

        Assert.AreEqual("GEN", book.Code);
        Assert.AreEqual(0, handler.CallCount);
    }

    [TestMethod]
    public async Task LoadBookAsync_HttpFailure_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var loader = new ScriptureSourceLoader(new HttpClient(handler), _tempCacheRoot);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => loader.LoadBookAsync("eng_bsb", "gen"));
    }

    [TestMethod]
    public async Task LoadBookAsync_SuccessfulFetch_WritesDiskCache()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(GenesisUsx) });
        var loader = new ScriptureSourceLoader(new HttpClient(handler), _tempCacheRoot);

        await loader.LoadBookAsync("eng_bsb", "gen");

        var cachedPath = Path.Combine(_tempCacheRoot, "eng_bsb", "gen.usx");
        Assert.IsTrue(File.Exists(cachedPath));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureSourceLoaderTests"`
Expected: FAIL — compile error, `HandsLiftedApp.Importer.Scripture.ScriptureSourceLoader` doesn't exist yet.

- [ ] **Step 4: Implement the loader**

`HandsLiftedApp.Importer.Scripture/ScriptureSourceLoader.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using HandsLiftedApp.Importer.Scripture.Models;

namespace HandsLiftedApp.Importer.Scripture;

public sealed class ScriptureSourceLoader
{
    private const string BaseUrl = "https://v1.fetch.bible/bibles/";

    private readonly HttpClient _httpClient;
    private readonly string _cacheRoot;
    private readonly ConcurrentDictionary<string, string> _memoryCache = new(StringComparer.OrdinalIgnoreCase);

    public ScriptureSourceLoader(HttpClient? httpClient = null, string? cacheRoot = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _cacheRoot = cacheRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HandsLifted", "ScriptureCache");
    }

    public async Task<ScriptureBook> LoadBookAsync(string translation, string bookCode)
    {
        if (string.IsNullOrWhiteSpace(translation))
        {
            throw new ArgumentException("Translation is required.", nameof(translation));
        }

        if (string.IsNullOrWhiteSpace(bookCode))
        {
            throw new ArgumentException("Book code is required.", nameof(bookCode));
        }

        var xml = await GetXmlAsync(translation, bookCode).ConfigureAwait(false);
        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        return UsxScriptureParser.Parse(document);
    }

    private async Task<string> GetXmlAsync(string translation, string bookCode)
    {
        var normalizedTranslation = translation.Trim().ToLowerInvariant();
        var normalizedBook = bookCode.Trim().ToLowerInvariant();
        var key = $"{normalizedTranslation}/{normalizedBook}";

        if (_memoryCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var diskPath = GetDiskCachePath(normalizedTranslation, normalizedBook);
        if (File.Exists(diskPath))
        {
            var diskXml = await File.ReadAllTextAsync(diskPath).ConfigureAwait(false);
            _memoryCache[key] = diskXml;
            return diskXml;
        }

        var uri = new Uri($"{BaseUrl}{normalizedTranslation}/usx/{normalizedBook}.usx", UriKind.Absolute);
        using var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Scripture fetch failed ({(int)response.StatusCode} {response.ReasonPhrase}) for {key}.");
        }

        var xml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        await WriteToDiskCacheAsync(normalizedTranslation, normalizedBook, xml).ConfigureAwait(false);
        _memoryCache[key] = xml;

        return xml;
    }

    private string GetDiskCachePath(string normalizedTranslation, string normalizedBook) =>
        Path.Combine(_cacheRoot, normalizedTranslation, $"{normalizedBook}.usx");

    private async Task WriteToDiskCacheAsync(string normalizedTranslation, string normalizedBook, string xml)
    {
        var finalPath = GetDiskCachePath(normalizedTranslation, normalizedBook);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        var tempPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(tempPath, xml).ConfigureAwait(false);
        File.Move(tempPath, finalPath, overwrite: true);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureSourceLoaderTests"`
Expected: PASS (5 tests).

- [ ] **Step 6: Run the full Phase 1 test suite together**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~Importer.Scripture"`
Expected: PASS (14 tests total — 2 model + 7 parser + 5 loader).

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Importer.Scripture/ScriptureSourceLoader.cs HandsLiftedApp.Tests/Importer/Scripture/ScriptureSourceLoaderTests.cs HandsLiftedApp.Tests/Importer/Scripture/FakeHttpMessageHandler.cs
git commit -m "feat: add ScriptureSourceLoader with memory+disk USX caching"
```

---

## What This Phase Does Not Cover

- No `ScriptureItem`/`ScriptureSlide`/`ScriptureItemInstance` — that's Phase 2, and is what will actually reference this project from `HandsLiftedApp.Core`.
- No rendering (`ScriptureSlideSpecBuilder`, `LivePane`/`ProjectorWindow` wiring) — Phase 3.
- No editor UI — Phase 4.
- No verse-per-slide or paragraph-per-slide splitting logic — deferred per your explicit direction to "deal with reflow across multiple slides at a later time." This phase only proves the data can be fetched and parsed correctly.
