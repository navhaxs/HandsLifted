# Scripture Paragraph Rendering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace one-verse-per-slide scripture rendering with flowing paragraph text: a reference header on slide 1, superscript verse numbers, and automatic reflow (splitting mid-verse where needed) across as many slides as the passage requires at the resolved theme's font settings.

**Architecture:** A new pure-logic layout engine (`ScriptureParagraphLayoutEngine`) paginates the flat verse list into pages of lines of runs, using the same `SKPaint.MeasureText` primitives the existing builders already use. A new `MultiRunTextLineElement` render-element type (alongside the existing `TextLineElement`, which stays untouched for Song) carries mixed-size runs so a superscript marker and its regular-size verse text can share one baseline. `ScriptureItem` gains a `Design` (Guid) property mirroring `SongItem.Design`; `ScriptureItemInstance` resolves it to a `BaseSlideTheme` the same way `SongItemInstance.ResolvedDesignTheme` does, and regenerates its pages whenever that changes (unlike Song, changing a Scripture item's theme changes how many slides the passage needs, not just how it looks). One-verse-per-slide's old builder, its identity scheme, and its tests are removed outright.

**Tech Stack:** .NET 8, MSTest, SkiaSharp (SKPaint/SKFont/SKTypeface — same APIs the existing builders already use), Avalonia 11.

## Global Constraints

- net8.0, MSTest, matches all prior phases.
- Fixed ratios (not user-configurable in this pass): header font size = body `FontSize × 1.3`; superscript font size = body `FontSize × 0.6`; superscript baseline raised by body `FontSize × 0.35`; header-to-body spacing = 20px.
- `AutofitEnabled`/`AutofitMinFontSizeRatio` are **not read** by the new pagination path — it always uses the resolved theme's `FontSize` as-is.
- Mid-verse breaks are allowed (a verse's text can continue on the next slide) — a verse's superscript marker is glued to its first word as one atomic unit so the marker itself is never orphaned alone at the end of a line, but the verse's remaining words wrap and paginate exactly like any other text.
- The reference header renders only on page 0 (slide 1); continuation slides show only paragraph text.
- Every verse shows its superscript number, including the first verse of the passage. A verse whose chapter differs from the previous verse's chapter shows `"{chapter}:{verse}"` instead of a bare verse number.
- No new font-family/size/alignment fields anywhere — `ScriptureItem.Design` (Guid) points at an existing shared `BaseSlideTheme` (from `Playlist.Designs`), exactly like `SongItem.Design`, and that theme's existing `FontFamily`/`FontSize`/`TextAlignment`/`LineHeightEm` properties are reused as-is.
- One-verse-per-slide is removed entirely, not kept as a second mode: `ScriptureItemInstance.UpdateVerseSlides`'s per-verse loop, the old `ScriptureSlideSpecBuilder`, its identity scheme (`slideId = "{chapter}:{verse}"`), and its test file are all deleted.
- New page-based slide identity: `slideId = "page{N}"` (N = zero-based page index). Regeneration diffs by this index — same index reuses the existing slide instance (preserving `Cached`/`Thumbnail` unless that page's actual content changed), extra trailing slides from a previous longer pagination are removed, new trailing slides are added.
- **Layering**: `ScriptureParagraphRun`/`ScriptureParagraphLine`/`ScriptureParagraphPage` and the layout engine all live in the `HandsLiftedApp.Core` project. The new `Lines` property goes on `ScriptureSlideInstance` (also `HandsLiftedApp.Core`, despite its `HandsLiftedApp.Data.Slides` namespace), **not** on the base `ScriptureSlide` class (which physically lives in the `HandsLiftedApp.Data` project and must not gain a dependency on Core types) — this mirrors how `Theme`/`Cached`/`Thumbnail` already live on `ScriptureSlideInstance` rather than `ScriptureSlide`.

---

### Task 1: `ScriptureParagraphLayoutEngine`

**Files:**
- Create: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureParagraphLayoutEngine.cs`
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureParagraphLayoutEngineTests.cs`

**Interfaces:**
- Produces: `ScriptureParagraphRun(string Text, bool IsSuperscript)`, `ScriptureParagraphLine(IReadOnlyList<ScriptureParagraphRun> Runs, bool IsHeader)`, `ScriptureParagraphPage(IReadOnlyList<ScriptureParagraphLine> Lines)` — all `readonly record struct`s — plus `ScriptureParagraphLayoutEngine.Paginate(IReadOnlyList<ScriptureVerseRef> verses, string headerText, BaseSlideTheme theme) : List<ScriptureParagraphPage>`. Also exposes `public const float HeaderFontSizeRatio = 1.3f`, `SuperscriptFontSizeRatio = 0.6f`, `SuperscriptBaselineOffsetRatio = 0.35f` — Task 5's builder reads these same constants rather than duplicating them, so pagination and rendering never drift apart on these three ratios.
- Consumes: `ScriptureVerseRef` (already exists, same namespace, `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureVerseRangeExtractor.cs:6`), `BaseSlideTheme` (`HandsLiftedApp.Data.SlideTheme`, already referenced from Core elsewhere).

This task is fully standalone — no dependency on any other task, and no other task's code needs to exist yet for this one to compile and pass its tests.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureParagraphLayoutEngineTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureParagraphLayoutEngineTests
{
    private static BaseSlideTheme MakeTheme(int fontSize = 60) => new BaseSlideTheme
    {
        FontSize = fontSize,
        TextColour = Colors.White,
        BackgroundColour = Colors.Black,
    };

    private static List<ScriptureVerseRef> MakeVerses(params (int chapter, int verse, string text)[] verses) =>
        verses.Select(v => new ScriptureVerseRef(v.chapter, v.verse, v.text)).ToList();

    [TestMethod]
    public void Paginate_ShortPassage_ProducesSinglePage()
    {
        var verses = MakeVerses((1, 1, "In the beginning God created the heaven and the earth."));

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Genesis 1:1", MakeTheme());

        Assert.AreEqual(1, pages.Count);
    }

    [TestMethod]
    public void Paginate_HeaderAppearsOnlyOnFirstPage()
    {
        var longVerseText = string.Join(" ", Enumerable.Repeat("word", 400));
        var verses = MakeVerses((1, 1, longVerseText));

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Genesis 1:1", MakeTheme(fontSize: 80));

        Assert.IsTrue(pages.Count > 1, "expected the long passage to reflow across multiple pages");
        Assert.IsTrue(pages[0].Lines.Any(l => l.IsHeader), "page 0 must contain the header");
        Assert.IsFalse(pages[1].Lines.Any(l => l.IsHeader), "continuation pages must not repeat the header");
    }

    [TestMethod]
    public void Paginate_LongPassage_ProducesMultiplePages()
    {
        var verses = Enumerable.Range(1, 40)
            .Select(v => new ScriptureVerseRef(1, v, "This is a reasonably long verse of sample text for pagination testing purposes."))
            .ToList();

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Test 1:1-40", MakeTheme(fontSize: 80));

        Assert.IsTrue(pages.Count > 1);
    }

    [TestMethod]
    public void Paginate_VerseNumberNeverOrphanedFromFirstWord()
    {
        var verses = Enumerable.Range(1, 40)
            .Select(v => new ScriptureVerseRef(1, v, "This is a reasonably long verse of sample text for pagination testing purposes."))
            .ToList();

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Test 1:1-40", MakeTheme(fontSize: 80));

        foreach (var page in pages)
        {
            foreach (var line in page.Lines)
            {
                if (line.Runs.Count == 0) continue;
                var lastRun = line.Runs[^1];
                Assert.IsFalse(lastRun.IsSuperscript,
                    "a line must never end with an orphaned superscript marker and no following word");
            }
        }
    }

    [TestMethod]
    public void Paginate_ChapterChangeVerse_ShowsChapterPrefixedMarker()
    {
        var verses = MakeVerses(
            (1, 25, "Follow peace with all men, and holiness."),
            (2, 1, "Wherefore laying aside all malice."));

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Hebrews 12:25-13:1", MakeTheme());

        var allRuns = pages.SelectMany(p => p.Lines).SelectMany(l => l.Runs).ToList();
        Assert.IsTrue(allRuns.Any(r => r.IsSuperscript && r.Text == "25"), "first verse keeps a bare verse number");
        Assert.IsTrue(allRuns.Any(r => r.IsSuperscript && r.Text == "2:1"), "chapter-change verse gets a chapter-prefixed marker");
    }

    [TestMethod]
    public void Paginate_FirstVerseAlsoGetsSuperscriptMarker()
    {
        var verses = MakeVerses((1, 1, "In the beginning God created the heaven and the earth."));

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Genesis 1:1", MakeTheme());

        var allRuns = pages.SelectMany(p => p.Lines).SelectMany(l => l.Runs).ToList();
        Assert.IsTrue(allRuns.Any(r => r.IsSuperscript && r.Text == "1"), "the very first verse must still show its number");
    }

    [TestMethod]
    public void Paginate_PathologicalSingleTooWideToken_PlacedAloneOnItsOwnLine()
    {
        var absurdlyLongWord = new string('a', 500);
        var verses = MakeVerses((1, 1, absurdlyLongWord));

        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, "Test 1:1", MakeTheme(fontSize: 100));

        Assert.IsTrue(pages.Count >= 1, "must return without hanging or throwing");
        var allText = pages.SelectMany(p => p.Lines).SelectMany(l => l.Runs).Select(r => r.Text);
        Assert.IsTrue(allText.Any(t => t == absurdlyLongWord), "the oversized word must still appear, alone on its line");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureParagraphLayoutEngineTests"`
Expected: FAIL — compile error, `ScriptureParagraphLayoutEngine` and its record types don't exist yet.

- [ ] **Step 3: Implement `ScriptureParagraphLayoutEngine`**

`HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureParagraphLayoutEngine.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public readonly record struct ScriptureParagraphRun(string Text, bool IsSuperscript);

    public readonly record struct ScriptureParagraphLine(IReadOnlyList<ScriptureParagraphRun> Runs, bool IsHeader);

    public readonly record struct ScriptureParagraphPage(IReadOnlyList<ScriptureParagraphLine> Lines);

    public static class ScriptureParagraphLayoutEngine
    {
        public const int CanvasWidth = 1920;
        public const int CanvasHeight = 1080;
        public const float HorizontalMargin = 80f;
        public const float VerticalMargin = 80f;
        public const float HeaderFontSizeRatio = 1.3f;
        public const float SuperscriptFontSizeRatio = 0.6f;
        public const float SuperscriptBaselineOffsetRatio = 0.35f;
        private const float HeaderSpacingBelow = 20f;

        public static List<ScriptureParagraphPage> Paginate(
            IReadOnlyList<ScriptureVerseRef> verses, string headerText, BaseSlideTheme theme)
        {
            float maxWidth = CanvasWidth - 2 * HorizontalMargin;
            float maxHeight = CanvasHeight - 2 * VerticalMargin;
            float bodyFontSize = theme.FontSize;
            float headerFontSize = bodyFontSize * HeaderFontSizeRatio;
            float lineHeight = bodyFontSize * (float)theme.LineHeightEm;
            float headerLineHeight = headerFontSize * (float)theme.LineHeightEm;

            using var typeface = GetTypeface(theme);
            using var bodyFont = new SKFont(typeface, bodyFontSize);
            using var bodyPaint = new SKPaint(bodyFont);
            using var superscriptFont = new SKFont(typeface, bodyFontSize * SuperscriptFontSizeRatio);
            using var superscriptPaint = new SKPaint(superscriptFont);
            using var headerFont = new SKFont(typeface, headerFontSize);
            using var headerPaint = new SKPaint(headerFont);

            var headerUnits = TokenizeHeader(headerText);
            var headerLines = WrapUnits(headerUnits, headerPaint, headerPaint, maxWidth, isHeader: true);

            var bodyUnits = TokenizeVerses(verses);
            var bodyLines = WrapUnits(bodyUnits, bodyPaint, superscriptPaint, maxWidth, isHeader: false);

            var pages = new List<ScriptureParagraphPage>();
            var currentPageLines = new List<ScriptureParagraphLine>();
            float heightUsed = 0f;

            foreach (var line in headerLines)
            {
                currentPageLines.Add(line);
                heightUsed += headerLineHeight;
            }
            if (headerLines.Count > 0)
                heightUsed += HeaderSpacingBelow;

            foreach (var line in bodyLines)
            {
                if (currentPageLines.Count > 0 && heightUsed + lineHeight > maxHeight)
                {
                    pages.Add(new ScriptureParagraphPage(currentPageLines));
                    currentPageLines = new List<ScriptureParagraphLine>();
                    heightUsed = 0f;
                }
                currentPageLines.Add(line);
                heightUsed += lineHeight;
            }

            pages.Add(new ScriptureParagraphPage(currentPageLines));
            return pages;
        }

        // A "unit" is one or two runs that must never be split across a line wrap:
        // a plain word is a 1-run unit; a verse's superscript marker + its first word
        // is a 2-run unit, so the marker can never end up orphaned alone at the end of
        // a line with its word pushed to the next line.
        private readonly record struct WrapUnit(IReadOnlyList<ScriptureParagraphRun> Runs);

        private static List<WrapUnit> TokenizeHeader(string headerText)
        {
            if (string.IsNullOrWhiteSpace(headerText))
                return new List<WrapUnit>();

            return headerText
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => new WrapUnit(new[] { new ScriptureParagraphRun(word, IsSuperscript: false) }))
                .ToList();
        }

        private static List<WrapUnit> TokenizeVerses(IReadOnlyList<ScriptureVerseRef> verses)
        {
            var units = new List<WrapUnit>();
            int? previousChapter = null;

            foreach (var v in verses)
            {
                string marker = previousChapter.HasValue && previousChapter.Value != v.Chapter
                    ? $"{v.Chapter}:{v.Verse}"
                    : $"{v.Verse}";
                previousChapter = v.Chapter;

                var words = v.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    units.Add(new WrapUnit(new[] { new ScriptureParagraphRun(marker, IsSuperscript: true) }));
                    continue;
                }

                units.Add(new WrapUnit(new[]
                {
                    new ScriptureParagraphRun(marker, IsSuperscript: true),
                    new ScriptureParagraphRun(words[0], IsSuperscript: false)
                }));

                for (int w = 1; w < words.Length; w++)
                    units.Add(new WrapUnit(new[] { new ScriptureParagraphRun(words[w], IsSuperscript: false) }));
            }

            return units;
        }

        private static List<ScriptureParagraphLine> WrapUnits(
            List<WrapUnit> units, SKPaint bodyPaint, SKPaint superscriptPaint, float maxWidth, bool isHeader)
        {
            var lines = new List<ScriptureParagraphLine>();
            var currentRuns = new List<ScriptureParagraphRun>();
            float currentWidth = 0f;

            foreach (var unit in units)
            {
                bool isFirstOnLine = currentRuns.Count == 0;
                var runsToAdd = isFirstOnLine ? unit.Runs : PrependSpace(unit.Runs);
                float unitWidth = MeasureRuns(runsToAdd, bodyPaint, superscriptPaint);

                if (!isFirstOnLine && currentWidth + unitWidth > maxWidth)
                {
                    lines.Add(new ScriptureParagraphLine(currentRuns, isHeader));
                    currentRuns = new List<ScriptureParagraphRun>();
                    currentWidth = 0f;
                    runsToAdd = unit.Runs;
                    unitWidth = MeasureRuns(runsToAdd, bodyPaint, superscriptPaint);
                }

                currentRuns.AddRange(runsToAdd);
                currentWidth += unitWidth;
            }

            if (currentRuns.Count > 0)
                lines.Add(new ScriptureParagraphLine(currentRuns, isHeader));

            return lines;
        }

        private static IReadOnlyList<ScriptureParagraphRun> PrependSpace(IReadOnlyList<ScriptureParagraphRun> runs)
        {
            var copy = runs.ToList();
            copy[0] = copy[0] with { Text = " " + copy[0].Text };
            return copy;
        }

        private static float MeasureRuns(IReadOnlyList<ScriptureParagraphRun> runs, SKPaint bodyPaint, SKPaint superscriptPaint)
        {
            float total = 0f;
            foreach (var run in runs)
                total += (run.IsSuperscript ? superscriptPaint : bodyPaint).MeasureText(run.Text);
            return total;
        }

        private static SKTypeface GetTypeface(BaseSlideTheme theme)
        {
            var weight = theme.CalculatedTextFontBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = theme.CalculatedTextFontItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            return SKTypeface.FromFamilyName(theme.FontFamilyAsText, weight, SKFontStyleWidth.Normal, slant)
                   ?? SKTypeface.Default;
        }
    }
}
```

Note: `WrapUnits` is called for the header pass with `superscriptPaint: headerPaint` (there is no superscript text in the header, so the parameter is unused for that call but the method signature stays uniform).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureParagraphLayoutEngineTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 137 + 7 = 144, no regressions.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureParagraphLayoutEngine.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureParagraphLayoutEngineTests.cs
git commit -m "feat: add ScriptureParagraphLayoutEngine for verse-to-page pagination"
```

---

### Task 2: `MultiRunTextLineElement` rendering primitives

**Files:**
- Modify: `HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs`
- Modify: `HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs`
- Modify: `HandsLiftedApp.Tests/Render/Skia/SlideRenderSpecTests.cs`
- Modify: `HandsLiftedApp.Tests/Render/Skia/SlideRendererTests.cs`

**Interfaces:**
- Produces: `TextRun(string Text, float FontSize, float BaselineOffsetY)` and `MultiRunTextLineElement(IReadOnlyList<TextRun> Runs, SKRect Bounds, SKTypeface Typeface, SKColor Color, DropShadowSpec? Shadow) : RenderElement(Bounds)`. Task 5's builder constructs these; `SlideRenderer` draws them.
- Consumes: nothing new — `RenderElement`, `SKRect`, `SKTypeface`, `SKColor`, `DropShadowSpec` all already exist.

This task is independent of Task 1 — it only defines and draws the new element type; nothing here depends on the layout engine's pagination logic. `TextLineElement`, `SongSlideSpecBuilder`, and Song's rendering are not touched at all.

- [ ] **Step 1: Write the failing tests**

Add to `HandsLiftedApp.Tests/Render/Skia/SlideRenderSpecTests.cs` (append after the existing 3 test methods, inside the same `SlideRenderSpecTests` class):

```csharp
    [TestMethod]
    public void MultiRunTextLineElement_IdentityIsConcatenatedRunText()
    {
        var runsA = new[] { new TextRun("13", 60f, -20f), new TextRun("Be sober-minded", 100f, 0f) };
        var runsB = new[] { new TextRun("13", 60f, -20f), new TextRun("Be sober-minded", 100f, 0f) };
        var a = new MultiRunTextLineElement(runsA, SKRect.Empty, SKTypeface.Default, SKColors.White, null);
        var b = new MultiRunTextLineElement(runsB, new SKRect(10, 20, 300, 120), SKTypeface.Default, SKColors.Red, null);

        Assert.AreEqual(
            string.Concat(a.Runs.Select(r => r.Text)),
            string.Concat(b.Runs.Select(r => r.Text)));
    }

    [TestMethod]
    public void SlideRenderSpec_StoresMultiRunTextLineElement()
    {
        var runs = new[] { new TextRun("Line one", 100f, 0f) };
        var elements = new List<RenderElement>
        {
            new MultiRunTextLineElement(runs, SKRect.Empty, SKTypeface.Default, SKColors.White, null)
        };
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Black), elements);

        Assert.AreEqual(1, spec.Elements.Count);
        Assert.IsInstanceOfType(spec.Elements[0], typeof(MultiRunTextLineElement));
    }
```

Add `using System.Linq;` to this test file's using block (needed for `.Select(...)` above).

Add to `HandsLiftedApp.Tests/Render/Skia/SlideRendererTests.cs` (append after the existing 6 test methods, inside the same `SlideRendererTests` class):

```csharp
    [TestMethod]
    public void RenderToSKBitmap_MultiRunTextLineElement_DrawsBothRuns()
    {
        // A superscript marker run and a regular-size text run on the same line —
        // both should paint white pixels somewhere in the bounds.
        var runs = new[]
        {
            new TextRun("13", 48f, -20f),
            new TextRun("Be sober-minded", 80f, 0f)
        };
        var element = new MultiRunTextLineElement(runs, new SKRect(0, 0, 600, 120), SKTypeface.Default, SKColors.White, null);
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Black), new[] { element });

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 600, 120);

        bool hasWhitePixel = false;
        for (int x = 0; x < bitmap.Width && !hasWhitePixel; x++)
            for (int y = 0; y < bitmap.Height && !hasWhitePixel; y++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red > 200 && px.Green > 200 && px.Blue > 200)
                    hasWhitePixel = true;
            }
        Assert.IsTrue(hasWhitePixel, "multi-run element should paint visible white text");
    }

    [TestMethod]
    public void Draw_UnchangedMultiRunLine_StaysAtFullAlpha()
    {
        var sharedElement = new MultiRunTextLineElement(
            new[] { new TextRun("Same line", 80f, 0f) },
            new SKRect(0, 0, 400, 120), SKTypeface.Default, SKColors.White, null);

        var prev = new SlideRenderSpec(new SolidBackground(SKColors.Black), new[] { sharedElement });
        var curr = new SlideRenderSpec(new SolidBackground(SKColors.Black), new[] { sharedElement });

        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 400, 120, prev, 0.5f);

        bool hasWhitePixel = false;
        for (int x = 0; x < bitmap.Width && !hasWhitePixel; x++)
            for (int y = 0; y < bitmap.Height && !hasWhitePixel; y++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red > 200 && px.Green > 200 && px.Blue > 200)
                    hasWhitePixel = true;
            }
        Assert.IsTrue(hasWhitePixel, "unchanged multi-run line should render at full opacity at mid-transition");
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SlideRenderSpecTests|FullyQualifiedName~SlideRendererTests"`
Expected: FAIL — compile error, `TextRun`/`MultiRunTextLineElement` don't exist yet.

- [ ] **Step 3: Add `TextRun`/`MultiRunTextLineElement` to `SlideRenderSpec.cs`**

In `HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs`, add after the existing `TextLineElement` record (after its closing `);` and before `DropShadowSpec`):

```csharp
public record TextRun(string Text, float FontSize, float BaselineOffsetY);

/// <remarks>
/// Like <see cref="TextLineElement"/>, structural equality is intentionally incomplete
/// (<see cref="SKTypeface"/> uses reference equality). Transition diffing uses the
/// concatenation of all <see cref="TextRun.Text"/> values plus <see cref="Bounds"/>.Top —
/// do not rely on record equality for semantic comparison.
/// </remarks>
public record MultiRunTextLineElement(
    IReadOnlyList<TextRun> Runs,
    SKRect Bounds,
    SKTypeface Typeface,
    SKColor Color,
    DropShadowSpec? Shadow
) : RenderElement(Bounds);
```

- [ ] **Step 4: Add the draw path and cross-fade key handling to `SlideRenderer.cs`**

In `HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs`, add `using System.Linq;` to the using block if not already present (it is not — this file currently only has `System`, `System.Collections.Generic`, `System.IO`, `Avalonia.Platform`, `Serilog`, `SkiaSharp`).

Replace the two `previousKeys`/`currentKeys` blocks and their surrounding loops inside `Draw` — currently:

```csharp
        // Elements in current: unchanged lines stay at 1.0, new/moved lines fade in.
        // Identity = (Text, Bounds.Top): same text at a different Y means the block
        // shifted (e.g. 3-line → 4-line), so the line must cross-fade.
        if (current != null)
        {
            var previousKeys = previous?.Elements
                .OfType<TextLineElement>()
                .Select(e => (e.Text, e.Bounds.Top))
                .ToHashSet()
                ?? new HashSet<(string, float)>();

            foreach (var element in current.Elements)
            {
                if (element is TextLineElement textEl)
                {
                    float alpha = previousKeys.Contains((textEl.Text, textEl.Bounds.Top)) ? 1f : progress;
                    DrawTextElement(canvas, textEl, alpha);
                }
            }
        }

        // Elements only in previous (by text+position): fade out
        if (previous != null && progress < 1f)
        {
            var currentKeys = current?.Elements
                .OfType<TextLineElement>()
                .Select(e => (e.Text, e.Bounds.Top))
                .ToHashSet()
                ?? new HashSet<(string, float)>();

            foreach (var element in previous.Elements)
            {
                if (element is TextLineElement textEl && !currentKeys.Contains((textEl.Text, textEl.Bounds.Top)))
                    DrawTextElement(canvas, textEl, 1f - progress);
            }
        }
```

with:

```csharp
        // Elements in current: unchanged lines stay at 1.0, new/moved lines fade in.
        // Identity = (combined text, Bounds.Top): same text at a different Y means the
        // block shifted (e.g. 3-line → 4-line), so the line must cross-fade.
        if (current != null)
        {
            var previousKeys = previous?.Elements
                .Select(GetTextIdentityKey)
                .Where(k => k.HasValue)
                .Select(k => k!.Value)
                .ToHashSet()
                ?? new HashSet<(string, float)>();

            foreach (var element in current.Elements)
            {
                if (element is TextLineElement textEl)
                {
                    float alpha = previousKeys.Contains((textEl.Text, textEl.Bounds.Top)) ? 1f : progress;
                    DrawTextElement(canvas, textEl, alpha);
                }
                else if (element is MultiRunTextLineElement multiEl)
                {
                    var key = GetTextIdentityKey(multiEl)!.Value;
                    float alpha = previousKeys.Contains(key) ? 1f : progress;
                    DrawMultiRunTextElement(canvas, multiEl, alpha);
                }
            }
        }

        // Elements only in previous (by text+position): fade out
        if (previous != null && progress < 1f)
        {
            var currentKeys = current?.Elements
                .Select(GetTextIdentityKey)
                .Where(k => k.HasValue)
                .Select(k => k!.Value)
                .ToHashSet()
                ?? new HashSet<(string, float)>();

            foreach (var element in previous.Elements)
            {
                if (element is TextLineElement textEl && !currentKeys.Contains((textEl.Text, textEl.Bounds.Top)))
                    DrawTextElement(canvas, textEl, 1f - progress);
                else if (element is MultiRunTextLineElement multiEl && !currentKeys.Contains(GetTextIdentityKey(multiEl)!.Value))
                    DrawMultiRunTextElement(canvas, multiEl, 1f - progress);
            }
        }
```

Then add these two new private methods, right after the existing `DrawTextElement` method (after its closing `}`):

```csharp
    private static (string, float)? GetTextIdentityKey(RenderElement element) => element switch
    {
        TextLineElement t => (t.Text, t.Bounds.Top),
        MultiRunTextLineElement m => (string.Concat(m.Runs.Select(r => r.Text)), m.Bounds.Top),
        _ => null
    };

    private static void DrawMultiRunTextElement(SKCanvas canvas, MultiRunTextLineElement element, float alpha)
    {
        if (alpha <= 0f) return;

        float maxFontSize = 0f;
        foreach (var run in element.Runs)
            if (run.FontSize > maxFontSize) maxFontSize = run.FontSize;

        using var refFont = new SKFont(element.Typeface, maxFontSize);
        refFont.GetFontMetrics(out var metrics);
        float baselineY = element.Bounds.Top - metrics.Ascent;

        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = element.Color.WithAlpha((byte)(element.Color.Alpha * alpha));

        if (element.Shadow is { } shadow)
        {
            float sigma = shadow.BlurRadius / 2f;
            paint.ImageFilter = SKImageFilter.CreateDropShadow(
                shadow.OffsetX, shadow.OffsetY, sigma, sigma,
                shadow.Color.WithAlpha((byte)(shadow.Color.Alpha * alpha)));
        }

        float x = element.Bounds.Left;
        foreach (var run in element.Runs)
        {
            using var runFont = new SKFont(element.Typeface, run.FontSize)
            {
                Edging = SKFontEdging.SubpixelAntialias,
                Subpixel = true,
            };
            using var measurePaint = new SKPaint(runFont);
            canvas.DrawText(run.Text, x, baselineY + run.BaselineOffsetY, runFont, paint);
            x += measurePaint.MeasureText(run.Text);
        }
    }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SlideRenderSpecTests|FullyQualifiedName~SlideRendererTests"`
Expected: PASS (all SlideRenderSpecTests + SlideRendererTests, including the 4 new ones).

- [ ] **Step 6: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 144 + 4 = 148, no regressions.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs HandsLiftedApp.Tests/Render/Skia/SlideRenderSpecTests.cs HandsLiftedApp.Tests/Render/Skia/SlideRendererTests.cs
git commit -m "feat: add MultiRunTextLineElement for mixed-size text runs (superscript verse numbers)"
```

---

### Task 3: `ScriptureItem.Design` + persistence wiring

**Files:**
- Modify: `HandsLiftedApp.Data/Models/Items/ScriptureItem.cs`
- Modify: `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs`
- Modify: `HandsLiftedApp.Core/ItemInstanceFactory.cs`
- Modify: `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs`
- Modify: `HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs`

**Interfaces:**
- Produces: `ScriptureItem.Design` (`Guid`, default `Guid.Empty`). Task 4's `ScriptureItemInstance.ResolvedDesignTheme` reads/writes it (inherited from `ScriptureItem`).
- Consumes: nothing new.

This task is independent of Tasks 1 and 2 — it's a small, mechanical, additive data-model change plus updating the two existing places that copy `ScriptureItem`'s fields across the serialize/deserialize boundary.

- [ ] **Step 1: Write the failing tests**

In `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs`, change the `SerializePlaylist_ThenDeserialize_RoundTripsScriptureItem` test method: add `Design = someDesignId` to the constructed `scriptureInstance`, and assert it on the round-tripped result. Replace:

```csharp
        var playlist = new PlaylistInstance();
        var scriptureInstance = new ScriptureItemInstance(playlist)
        {
            Title = "John 3:16-21",
            Translation = "eng_bsb",
            Book = "JHN",
            StartChapter = 3,
            StartVerse = 16,
            EndChapter = 3,
            EndVerse = 21
        };
        playlist.Items.Add(scriptureInstance);
```

with:

```csharp
        var someDesignId = Guid.NewGuid();
        var playlist = new PlaylistInstance();
        var scriptureInstance = new ScriptureItemInstance(playlist)
        {
            Title = "John 3:16-21",
            Translation = "eng_bsb",
            Book = "JHN",
            StartChapter = 3,
            StartVerse = 16,
            EndChapter = 3,
            EndVerse = 21,
            Design = someDesignId
        };
        playlist.Items.Add(scriptureInstance);
```

and add one line at the end of the method (after the existing `Assert.AreEqual(21, scriptureItem.EndVerse);`):

```csharp
        Assert.AreEqual(someDesignId, scriptureItem.Design);
```

In `HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs`, change `ToItemInstance_ScriptureItem_RoundTripsThroughDiskAndFactory` similarly. Replace:

```csharp
        var original = new ScriptureItem
        {
            Title = "John 3:16-21",
            Translation = "eng_bsb",
            Book = "JHN",
            StartChapter = 3,
            StartVerse = 16,
            EndChapter = 3,
            EndVerse = 21
        };
```

with:

```csharp
        var someDesignId = Guid.NewGuid();
        var original = new ScriptureItem
        {
            Title = "John 3:16-21",
            Translation = "eng_bsb",
            Book = "JHN",
            StartChapter = 3,
            StartVerse = 16,
            EndChapter = 3,
            EndVerse = 21,
            Design = someDesignId
        };
```

and add one line at the end of the method (after the existing `Assert.AreEqual(21, scriptureInstance.EndVerse);`):

```csharp
        Assert.AreEqual(someDesignId, scriptureInstance.Design);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~HandsLiftedDocXmlSerializerTests|FullyQualifiedName~ItemInstanceFactoryTests"`
Expected: FAIL — compile error, `ScriptureItem`/`ScriptureItemInstance` don't have a `Design` property yet.

- [ ] **Step 3: Add `Design` to `ScriptureItem`**

In `HandsLiftedApp.Data/Models/Items/ScriptureItem.cs`, add `using System;` is already present (line 1). Insert after the existing `EndVerse` property (after line 31, before the closing `}` of the class):

```csharp

        private Guid _design = Guid.Empty;
        public Guid Design { get => _design; set => this.RaiseAndSetIfChanged(ref _design, value); }
```

This exactly mirrors `SongItem.Design` (`HandsLiftedApp.Data/Models/Items/SongItem.cs:26-27`).

- [ ] **Step 4: Copy `Design` at both serialize/deserialize call sites**

In `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs`, in `SerializeItem`'s `ScriptureItemInstance` branch, add `Design = scriptureItemInstance.Design` after `EndVerse`:

```csharp
            else if (item is ScriptureItemInstance scriptureItemInstance)
            {
                return new ScriptureItem()
                {
                    UUID = scriptureItemInstance.UUID,
                    Title = scriptureItemInstance.Title,
                    Translation = scriptureItemInstance.Translation,
                    Book = scriptureItemInstance.Book,
                    StartChapter = scriptureItemInstance.StartChapter,
                    StartVerse = scriptureItemInstance.StartVerse,
                    EndChapter = scriptureItemInstance.EndChapter,
                    EndVerse = scriptureItemInstance.EndVerse,
                    Design = scriptureItemInstance.Design
                };
            }
```

In `HandsLiftedApp.Core/ItemInstanceFactory.cs`, in `ToItemInstance`'s `ScriptureItem` branch, add `Design = scriptureItem.Design` after `EndVerse`:

```csharp
            else if (deserializedItem is ScriptureItem scriptureItem)
            {
                var scripture = new ScriptureItemInstance(playlist)
                {
                    UUID = scriptureItem.UUID,
                    Title = scriptureItem.Title,
                    Translation = scriptureItem.Translation,
                    Book = scriptureItem.Book,
                    StartChapter = scriptureItem.StartChapter,
                    StartVerse = scriptureItem.StartVerse,
                    EndChapter = scriptureItem.EndChapter,
                    EndVerse = scriptureItem.EndVerse,
                    Design = scriptureItem.Design
                };
```

(The rest of both branches — the fire-and-forget `GenerateSlidesAsync()` call and its comment — is unchanged.)

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~HandsLiftedDocXmlSerializerTests|FullyQualifiedName~ItemInstanceFactoryTests"`
Expected: PASS.

- [ ] **Step 6: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 148 (this task adds no new tests, only extends 2 existing ones), no regressions.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Data/Models/Items/ScriptureItem.cs HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs HandsLiftedApp.Core/ItemInstanceFactory.cs HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs
git commit -m "feat: add ScriptureItem.Design, mirroring SongItem.Design"
```

---

### Task 4: `ScriptureItemInstance`/`ScriptureSlideInstance` — pagination integration

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs`
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`
- Modify: `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs`

**Interfaces:**
- Consumes: `ScriptureParagraphLayoutEngine.Paginate` (Task 1), `ScriptureItem.Design` (Task 3).
- Produces: `ScriptureSlideInstance.Lines` (`IReadOnlyList<ScriptureParagraphLine>`), `ScriptureItemInstance.ResolvedDesignTheme` (`BaseSlideTheme?`). Task 5's builder reads `slide.Lines` and `slide.Theme` to build the render spec.

This task depends on Task 1 (the layout engine must exist) and Task 3 (`Design` must exist on `ScriptureItem`). It does not depend on Task 2 or Task 5 — it produces the paginated data model; nothing here needs `MultiRunTextLineElement` or the new builder to exist yet.

- [ ] **Step 1: Rewrite the test file (RED)**

Replace the whole content of `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs` with:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureItemInstanceTests
{
    private const string GenesisChapterOneUsx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <usx version="3.0">
          <book code="GEN" style="id">- Genesis</book>
          <para style="mt1">Genesis</para>
          <chapter number="1" style="c" sid="GEN 1"/>
          <para style="p">
            <verse number="1" style="v" sid="GEN 1:1"/>In the beginning God created the heaven and the earth.<verse eid="GEN 1:1"/>
            <verse number="2" style="v" sid="GEN 1:2"/>And the earth was without form, and void.<verse eid="GEN 1:2"/>
            <verse number="3" style="v" sid="GEN 1:3"/>And God said, Let there be light.<verse eid="GEN 1:3"/>
          </para>
          <chapter eid="GEN 1"/>
        </usx>
        """;

    private static ScriptureLocalUsxStore MakeFakeStore(string xml)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureItemInstanceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "gen.usx"), xml);
        return new ScriptureLocalUsxStore(tempDir);
    }

    private static ScriptureLocalUsxStore MakeEmptyStore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureItemInstanceTests_" + Guid.NewGuid().ToString("N"));
        return new ScriptureLocalUsxStore(tempDir);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_ShortRange_ProducesOnePageWithHeaderAndVerseText()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual(1, instance.Slides.Count);
        var first = (ScriptureSlideInstance)instance.Slides[0];
        Assert.IsTrue(first.Lines.Any(l => l.IsHeader), "first slide must carry a header line");
        Assert.IsTrue(first.Text.Contains("In the beginning"), "flattened text should include verse content");
        Assert.IsTrue(first.Text.Contains("without form"), "flattened text should include the second verse too");
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_SlideId_UsesPageIndexNotChapterVerse()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        var first = (ScriptureSlideInstance)instance.Slides[0];
        Assert.AreEqual("page0", first.Id);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_SecondCallWithSameRange_PreservesSlideIdentity()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();
        var firstCallSlide = instance.Slides[0];

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();
        var secondCallSlide = instance.Slides[0];

        Assert.AreSame(firstCallSlide, secondCallSlide);
    }

    [TestMethod]
    public async Task ActiveSlide_TracksSelectedSlideIndex()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };
        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        instance.SelectedSlideIndex = 0;

        Assert.AreSame(instance.Slides[0], instance.ActiveSlide);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_BookFileMissing_ProducesPlaceholderPage()
    {
        var instance = new ScriptureItemInstance(null, MakeEmptyStore())
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual(1, instance.Slides.Count);
        var slide = (ScriptureSlideInstance)instance.Slides[0];
        StringAssert.Contains(slide.Text, "Scripture data not found");
        StringAssert.Contains(slide.Text, "gen");
    }

    [TestMethod]
    public void ResolvedDesignTheme_DesignEmpty_FallsBackToDefaultTheme()
    {
        var instance = new ScriptureItemInstance(null, MakeEmptyStore());

        // With no ParentPlaylist and Design left at its Guid.Empty default, resolution
        // falls back to the app's default theme rather than throwing or returning null.
        Assert.IsNotNull(instance.ResolvedDesignTheme);
    }
}
```

- [ ] **Step 2: Run tests to verify the new/changed ones fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureItemInstanceTests"`
Expected: FAIL — compile error (`ScriptureSlideInstance.Lines`/`ScriptureItemInstance.ResolvedDesignTheme` don't exist), and behaviorally the old per-verse `UpdateVerseSlides` would produce 2 slides for this 2-verse range, not 1 page.

- [ ] **Step 3: Add `Lines` to `ScriptureSlideInstance`**

In `HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs`, add `using System.Collections.Generic;` to the using block. Add this property after the existing `Text`/`Label`-setting constructor logic — insert right after the constructor's closing `}` (after line 43, before `private void RequestRender()`):

```csharp

        private IReadOnlyList<ScriptureParagraphLine> _lines = Array.Empty<ScriptureParagraphLine>();
        public IReadOnlyList<ScriptureParagraphLine> Lines
        {
            get => _lines;
            set => this.RaiseAndSetIfChanged(ref _lines, value);
        }
```

Add `using HandsLiftedApp.Core.Models.RuntimeData.Items;` to this file's using block (for `ScriptureParagraphLine`).

Also update the stale comment in the constructor. Replace:

```csharp
            // No per-item Design/theme-selection concept exists yet for scripture items
            // (unlike SongItem.Design) — every scripture slide uses the app's default theme.
            Theme = Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
```

with:

```csharp
            // Default theme here is just the constructor's own initial value — the caller
            // (ScriptureItemInstance) immediately overwrites Theme with the item's
            // ResolvedDesignTheme after constructing or reusing this slide instance.
            Theme = Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
```

- [ ] **Step 4: Rewrite `ScriptureItemInstance`'s generation pipeline**

In `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`, add `using System.Linq;` to the using block (already present, per the file read during planning). Add `ResolvedDesignTheme`, right after the existing `_store`/constructor block (after the constructor's closing `}`, before `private ObservableCollection<Slide> _slides`):

```csharp

        public BaseSlideTheme? ResolvedDesignTheme
        {
            get => ParentPlaylist?.Designs.FirstOrDefault(d => d.Id == Design)
                   ?? Globals.Instance.AppPreferences?.DefaultTheme;
            set
            {
                Design = value?.Id ?? Guid.Empty;
                _ = GenerateSlidesAsync().ContinueWith(
                    t => Log.Error(t.Exception, "Failed to generate scripture slides for {Title}", Title),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
        }
```

Add `using HandsLiftedApp.Data.SlideTheme;` to the using block (for `BaseSlideTheme`).

In the constructor, add a `Design` watcher alongside the existing dirty-flag subscription — mirroring `SongItemInstance`'s `this.WhenAnyValue(x => x.Design).Subscribe(_ => this.RaisePropertyChanged(nameof(ResolvedDesignTheme)));` (`SongItemInstance.cs:65-66`). Insert this line right after the existing `_activeSlide = this.WhenAnyValue(...)` block and before the `this.WhenAnyValue(i => i.Title, ...)` dirty-flag subscription:

```csharp
            this.WhenAnyValue(x => x.Design)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(ResolvedDesignTheme)));
```

Replace `GenerateSlidesAsync` and `UpdateVerseSlides` entirely. Current:

```csharp
        public async Task GenerateSlidesAsync()
        {
            try
            {
                var book = await _store.LoadBookAsync(Book).ConfigureAwait(false);
                var verses = ScriptureVerseRangeExtractor.Extract(book, StartChapter, StartVerse, EndChapter, EndVerse);
                UpdateVerseSlides(book.Title, verses);
            }
            catch (ScriptureBookNotFoundException ex)
            {
                Log.Error(ex, "Scripture data not found for {Book} ({Translation})", Book, Translation);
                UpdateVerseSlides(Book, MakeMissingDataPlaceholder());
            }
        }

        private System.Collections.Generic.List<ScriptureVerseRef> MakeMissingDataPlaceholder()
        {
            var text =
                $"Scripture data not found: {Book} {StartChapter}:{StartVerse}-{EndChapter}:{EndVerse} ({Translation})\n" +
                "Check Setup > Library > Scripture Data Path";
            return new System.Collections.Generic.List<ScriptureVerseRef> { new ScriptureVerseRef(StartChapter, StartVerse, text) };
        }

        private void UpdateVerseSlides(string bookTitle, System.Collections.Generic.List<ScriptureVerseRef> verses)
        {
            // GenerateSlidesAsync awaits the local disk read with .ConfigureAwait(false), so this
            // continuation still runs on a thread-pool thread (File I/O, not UI-thread work), not
            // the UI thread. Slides is bound live in ItemSlidesView once a scripture item sits in a
            // playlist, so the mutation below (and the RaisePropertyChanged(nameof(Slides)) it
            // triggers) must be marshaled back to the UI thread rather than running here.
            Dispatcher.UIThread.Post(() =>
            {
                var newSlides = new ObservableCollection<Slide>();

                foreach (var v in verses)
                {
                    var slideId = $"{v.Chapter}:{v.Verse}";
                    var label = string.IsNullOrEmpty(bookTitle) ? $"{Book} {v.Chapter}:{v.Verse}" : $"{bookTitle} {v.Chapter}:{v.Verse}";

                    var existing = Slides
                        .OfType<ScriptureSlideInstance>()
                        .FirstOrDefault(s => s.Id == slideId);

                    if (existing != null)
                    {
                        if (existing.Text != v.Text) existing.Text = v.Text;
                        if (existing.Label != label) existing.Label = label;
                        newSlides.Add(existing);
                    }
                    else
                    {
                        newSlides.Add(new ScriptureSlideInstance(this, slideId, text: v.Text, label: label));
                    }
                }

                _slides = newSlides;
                this.RaisePropertyChanged(nameof(Slides));

                // Enqueue newly created slides (and any reused slide that never got a first
                // render) for background thumbnail generation. Cached == null covers both:
                // brand-new slides from this call, and slides that were new on a prior call
                // but never got enqueued (which would otherwise stay permanently blank).
                var toRender = newSlides.OfType<ScriptureSlideInstance>()
                    .Where(s => s.Cached == null)
                    .Cast<IRenderable>()
                    .ToList();
                if (toRender.Count > 0)
                    Globals.Instance.SlideRenderQueue.EnqueueBatch(toRender);
            });
        }
```

Replace with:

```csharp
        public async Task GenerateSlidesAsync()
        {
            List<ScriptureVerseRef> verses;
            string bookTitle;
            try
            {
                var book = await _store.LoadBookAsync(Book).ConfigureAwait(false);
                verses = ScriptureVerseRangeExtractor.Extract(book, StartChapter, StartVerse, EndChapter, EndVerse);
                bookTitle = book.Title;
            }
            catch (ScriptureBookNotFoundException ex)
            {
                Log.Error(ex, "Scripture data not found for {Book} ({Translation})", Book, Translation);
                verses = MakeMissingDataPlaceholder();
                bookTitle = Book;
            }

            var referenceLabel = FormatReferenceLabel(bookTitle);
            UpdatePages(referenceLabel, verses);
        }

        private string FormatReferenceLabel(string bookTitle)
        {
            var title = string.IsNullOrEmpty(bookTitle) ? Book : bookTitle;
            return StartChapter == EndChapter && StartVerse == EndVerse
                ? $"{title} {StartChapter}:{StartVerse}"
                : $"{title} {StartChapter}:{StartVerse}-{EndChapter}:{EndVerse}";
        }

        private List<ScriptureVerseRef> MakeMissingDataPlaceholder()
        {
            var text =
                $"Scripture data not found: {Book} {StartChapter}:{StartVerse}-{EndChapter}:{EndVerse} ({Translation})\n" +
                "Check Setup > Library > Scripture Data Path";
            return new List<ScriptureVerseRef> { new ScriptureVerseRef(StartChapter, StartVerse, text) };
        }

        private void UpdatePages(string referenceLabel, List<ScriptureVerseRef> verses)
        {
            var theme = ResolvedDesignTheme ?? new BaseSlideTheme();
            var pages = ScriptureParagraphLayoutEngine.Paginate(verses, referenceLabel, theme);

            // GenerateSlidesAsync awaits the local disk read with .ConfigureAwait(false), so this
            // continuation still runs on a thread-pool thread (File I/O, not UI-thread work), not
            // the UI thread. Slides is bound live in ItemSlidesView once a scripture item sits in a
            // playlist, so the mutation below (and the RaisePropertyChanged(nameof(Slides)) it
            // triggers) must be marshaled back to the UI thread rather than running here.
            Dispatcher.UIThread.Post(() =>
            {
                var newSlides = new ObservableCollection<Slide>();

                for (int i = 0; i < pages.Count; i++)
                {
                    var page = pages[i];
                    var slideId = $"page{i}";
                    var flatText = string.Join(" ", page.Lines.SelectMany(l => l.Runs).Select(r => r.Text)).Trim();

                    var existing = Slides
                        .OfType<ScriptureSlideInstance>()
                        .FirstOrDefault(s => s.Id == slideId);

                    if (existing != null)
                    {
                        existing.Lines = page.Lines;
                        if (existing.Text != flatText) existing.Text = flatText;
                        if (existing.Label != referenceLabel) existing.Label = referenceLabel;
                        existing.Theme = theme;
                        newSlides.Add(existing);
                    }
                    else
                    {
                        var slide = new ScriptureSlideInstance(this, slideId, text: flatText, label: referenceLabel)
                        {
                            Lines = page.Lines,
                            Theme = theme
                        };
                        newSlides.Add(slide);
                    }
                }

                _slides = newSlides;
                this.RaisePropertyChanged(nameof(Slides));

                // Enqueue newly created slides (and any reused slide that never got a first
                // render) for background thumbnail generation. Cached == null covers both:
                // brand-new slides from this call, and slides that were new on a prior call
                // but never got enqueued (which would otherwise stay permanently blank).
                var toRender = newSlides.OfType<ScriptureSlideInstance>()
                    .Where(s => s.Cached == null)
                    .Cast<IRenderable>()
                    .ToList();
                if (toRender.Count > 0)
                    Globals.Instance.SlideRenderQueue.EnqueueBatch(toRender);
            });
        }
```

Add `using System.Collections.Generic;` to this file's using block if not already present (it is not — check first; the file currently has `System`, `System.Collections.ObjectModel`, `System.Linq`, `System.Reactive.Linq`, `System.Threading.Tasks`, `Avalonia.Threading`, and several `HandsLiftedApp.*` usings, per the file read during planning — `System.Collections.Generic` is not among them today since the old code always fully-qualified `System.Collections.Generic.List<...>`; the new code above uses bare `List<...>` throughout, so the using is required).

Note: `existing.Text != flatText` no longer needs to also separately invalidate on `Lines` changing — `Lines` is always recomputed in lockstep with `flatText` from the same `page.Lines`, so a content change always shows up as a `Text` change too (this is what already drives `ScriptureSlideInstance`'s existing `RequestRender()` subscription on `Text`, which needs no change).

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureItemInstanceTests"`
Expected: PASS (6 tests).

- [ ] **Step 6: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: build succeeds (the old `ScriptureSlideSpecBuilder`/tests still reference `slide.Text` only, which still exists and still works, so nothing breaks yet — Task 5 replaces that builder). Count = 148 − 6 (old `ScriptureItemInstanceTests` methods) + 6 (new ones) = 148, no regressions. (The old test file had 6 methods; the new one also has 6 — same count, different behavior.)

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs
git commit -m "feat: switch ScriptureItemInstance to page-based slide generation via ScriptureParagraphLayoutEngine"
```

---

### Task 5: `ScriptureParagraphSpecBuilder` (replaces `ScriptureSlideSpecBuilder`)

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/Builders/ScriptureParagraphSpecBuilder.cs`
- Test: `HandsLiftedApp.Tests/Render/Skia/Builders/ScriptureParagraphSpecBuilderTests.cs`
- Delete: `HandsLiftedApp.Core/Render/Skia/Builders/ScriptureSlideSpecBuilder.cs`
- Delete: `HandsLiftedApp.Tests/Render/Skia/Builders/ScriptureSlideSpecBuilderTests.cs`
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs`

**Interfaces:**
- Consumes: `ScriptureSlideInstance.Lines`/`.Theme` (Task 4), `MultiRunTextLineElement`/`TextRun` (Task 2), `ScriptureParagraphLayoutEngine`'s public ratio constants (Task 1).
- Produces: `ScriptureParagraphSpecBuilder.Build(ScriptureSlideInstance slide) : SlideRenderSpec` — the same signature `ScriptureSlideInstance.Render()` already calls, just against the new builder.

This task depends on Tasks 1, 2, and 4 all being complete (it consumes all three). It's the last content-producing task; Task 6 is pure cleanup/UI on top of this one.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Render/Skia/Builders/ScriptureParagraphSpecBuilderTests.cs`:

```csharp
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Avalonia.Media;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Tests.Render.Skia.Builders;

[TestClass]
public class ScriptureParagraphSpecBuilderTests
{
    private static BaseSlideTheme MakeTheme() => new BaseSlideTheme
    {
        FontSize = 60,
        TextColour = Colors.White,
        BackgroundColour = Colors.Black,
    };

    private static ScriptureParagraphLine HeaderLine(string text) =>
        new ScriptureParagraphLine(new[] { new ScriptureParagraphRun(text, IsSuperscript: false) }, IsHeader: true);

    private static ScriptureParagraphLine BodyLine(params ScriptureParagraphRun[] runs) =>
        new ScriptureParagraphLine(runs, IsHeader: false);

    [TestMethod]
    public void Build_WithTheme_ReturnsSolidBackground()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = MakeTheme() };
        slide.Lines = new[] { HeaderLine("Genesis 1:1") };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
    }

    [TestMethod]
    public void Build_NoTheme_ReturnsEmptyElements()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = null };
        slide.Lines = new[] { HeaderLine("Genesis 1:1") };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }

    [TestMethod]
    public void Build_NoLines_ReturnsEmptyElements()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = MakeTheme() };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }

    [TestMethod]
    public void Build_OneLineWithMarkerAndText_ReturnsOneMultiRunElementWithTwoRuns()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = MakeTheme() };
        slide.Lines = new[]
        {
            HeaderLine("Genesis 1:1"),
            BodyLine(
                new ScriptureParagraphRun("1", IsSuperscript: true),
                new ScriptureParagraphRun("In the beginning God created the heaven and the earth.", IsSuperscript: false))
        };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        Assert.AreEqual(2, spec.Elements.Count, "one element for the header line, one for the body line");
        var bodyElement = (MultiRunTextLineElement)spec.Elements[1];
        Assert.AreEqual(2, bodyElement.Runs.Count);
        Assert.AreEqual("1", bodyElement.Runs[0].Text);
        Assert.IsTrue(bodyElement.Runs[0].FontSize < bodyElement.Runs[1].FontSize, "superscript run must be smaller than body run");
        Assert.IsTrue(bodyElement.Runs[0].BaselineOffsetY < 0f, "superscript run must be raised (negative offset)");
    }

    [TestMethod]
    public void Build_HeaderLine_UsesLargerFontThanBodyLine()
    {
        var slide = new ScriptureSlideInstance(null, "page0") { Theme = MakeTheme() };
        slide.Lines = new[]
        {
            HeaderLine("Genesis 1:1"),
            BodyLine(new ScriptureParagraphRun("Body text", IsSuperscript: false))
        };

        var spec = ScriptureParagraphSpecBuilder.Build(slide);

        var headerElement = (MultiRunTextLineElement)spec.Elements[0];
        var bodyElement = (MultiRunTextLineElement)spec.Elements[1];
        Assert.IsTrue(headerElement.Runs[0].FontSize > bodyElement.Runs[0].FontSize);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureParagraphSpecBuilderTests"`
Expected: FAIL — compile error, `ScriptureParagraphSpecBuilder` doesn't exist yet.

- [ ] **Step 3: Implement `ScriptureParagraphSpecBuilder`**

`HandsLiftedApp.Core/Render/Skia/Builders/ScriptureParagraphSpecBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using SkiaSharp;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

public static class ScriptureParagraphSpecBuilder
{
    private const int CanvasWidth = ScriptureParagraphLayoutEngine.CanvasWidth;
    private const int CanvasHeight = ScriptureParagraphLayoutEngine.CanvasHeight;
    private const float HorizontalMargin = ScriptureParagraphLayoutEngine.HorizontalMargin;
    private const float HeaderSpacingBelow = 20f;

    private static DropShadowSpec? GetShadow(BaseSlideTheme theme) =>
        theme.DropShadowEnabled
            ? new DropShadowSpec(
                (float)theme.DropShadowOffsetX,
                (float)theme.DropShadowOffsetY,
                (float)theme.DropShadowBlurRadius,
                ToSkColor(theme.DropShadowColour))
            : null;

    public static SlideRenderSpec Build(ScriptureSlideInstance slide)
    {
        var bg = BuildBackground(slide);

        if (slide.Theme == null || slide.Lines.Count == 0)
            return new SlideRenderSpec(bg, Array.Empty<RenderElement>());

        var elements = BuildTextElements(slide.Lines, slide.Theme);
        return new SlideRenderSpec(bg, elements);
    }

    private static BackgroundSpec BuildBackground(ScriptureSlideInstance slide)
    {
        if (!string.IsNullOrEmpty(slide.Theme?.BackgroundGraphicFilePath))
            return new ImageBackground(slide.Theme.BackgroundGraphicFilePath);

        var bg = slide.Theme != null
            ? ToSkColor(slide.Theme.BackgroundAvaloniaColour)
            : SKColors.Black;
        return new SolidBackground(bg);
    }

    private static IReadOnlyList<RenderElement> BuildTextElements(
        IReadOnlyList<ScriptureParagraphLine> lines, BaseSlideTheme theme)
    {
        using var typeface = GetTypeface(theme);
        float bodyFontSize = theme.FontSize;
        float headerFontSize = bodyFontSize * ScriptureParagraphLayoutEngine.HeaderFontSizeRatio;
        float superscriptFontSize = bodyFontSize * ScriptureParagraphLayoutEngine.SuperscriptFontSizeRatio;
        float superscriptBaselineOffset = -(bodyFontSize * ScriptureParagraphLayoutEngine.SuperscriptBaselineOffsetRatio);
        float lineHeight = bodyFontSize * (float)theme.LineHeightEm;
        float headerLineHeight = headerFontSize * (float)theme.LineHeightEm;
        float maxWidth = CanvasWidth - 2 * HorizontalMargin;
        var color = ToSkColor(theme.TextAvaloniaColour);
        var shadow = GetShadow(theme);

        using var bodyFont = new SKFont(typeface, bodyFontSize);
        using var bodyPaint = new SKPaint(bodyFont);
        using var superscriptFont = new SKFont(typeface, superscriptFontSize);
        using var superscriptPaint = new SKPaint(superscriptFont);
        using var headerFont = new SKFont(typeface, headerFontSize);
        using var headerPaint = new SKPaint(headerFont);

        bool hasHeader = lines.Any(l => l.IsHeader);
        float totalHeight = lines.Sum(l => l.IsHeader ? headerLineHeight : lineHeight);
        if (hasHeader && lines.Any(l => !l.IsHeader))
            totalHeight += HeaderSpacingBelow;

        float startY = (CanvasHeight - totalHeight) / 2f;
        var result = new List<RenderElement>(lines.Count);
        float y = startY;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            float thisLineHeight = line.IsHeader ? headerLineHeight : lineHeight;
            float lineFontSize = line.IsHeader ? headerFontSize : bodyFontSize;
            var linePaint = line.IsHeader ? headerPaint : bodyPaint;

            float lineWidth = 0f;
            var runs = new List<TextRun>(line.Runs.Count);
            foreach (var run in line.Runs)
            {
                bool isSuperscript = !line.IsHeader && run.IsSuperscript;
                var runPaint = isSuperscript ? superscriptPaint : linePaint;
                float runFontSize = isSuperscript ? superscriptFontSize : lineFontSize;
                float runOffset = isSuperscript ? superscriptBaselineOffset : 0f;

                lineWidth += runPaint.MeasureText(run.Text);
                runs.Add(new TextRun(run.Text, runFontSize, runOffset));
            }

            float x = theme.TextAlignment switch
            {
                TextAlignment.Right => CanvasWidth - lineWidth - HorizontalMargin,
                TextAlignment.Left => HorizontalMargin,
                _ => (CanvasWidth - lineWidth) / 2f, // Center / Justify
            };

            var bounds = new SKRect(x, y, x + lineWidth, y + thisLineHeight);
            result.Add(new MultiRunTextLineElement(runs, bounds, typeface, color, shadow));

            y += thisLineHeight;
            bool isLastHeaderLineBeforeBody = line.IsHeader && (i + 1 >= lines.Count || !lines[i + 1].IsHeader);
            if (isLastHeaderLineBeforeBody)
                y += HeaderSpacingBelow;
        }

        return result;
    }

    private static SKTypeface GetTypeface(BaseSlideTheme theme)
    {
        var weight = theme.CalculatedTextFontBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = theme.CalculatedTextFontItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return SKTypeface.FromFamilyName(theme.FontFamilyAsText, weight, SKFontStyleWidth.Normal, slant)
               ?? SKTypeface.Default;
    }

    private static SKColor ToSkColor(Color color) =>
        new SKColor(color.R, color.G, color.B, color.A);
}
```

Then, in `HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs`, update `Render()` to call the new builder. Replace:

```csharp
        public void Render()
        {
            var spec = ScriptureSlideSpecBuilder.Build(this);
```

with:

```csharp
        public void Render()
        {
            var spec = ScriptureParagraphSpecBuilder.Build(this);
```

(The rest of `Render()` — the bitmap/thumbnail creation and `Dispatcher.UIThread.Post` — is unchanged.)

- [ ] **Step 4: Delete the old builder and its tests**

```bash
git rm HandsLiftedApp.Core/Render/Skia/Builders/ScriptureSlideSpecBuilder.cs HandsLiftedApp.Tests/Render/Skia/Builders/ScriptureSlideSpecBuilderTests.cs
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureParagraphSpecBuilderTests"`
Expected: PASS (5 tests).

- [ ] **Step 6: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 148 − 8 (old `ScriptureSlideSpecBuilderTests`' 8 methods, deleted) + 5 (new `ScriptureParagraphSpecBuilderTests`) = 145. Confirm `grep -rn "ScriptureSlideSpecBuilder" --include=*.cs .` (excluding `docs/`) returns nothing.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/Render/Skia/Builders/ScriptureParagraphSpecBuilder.cs HandsLiftedApp.Tests/Render/Skia/Builders/ScriptureParagraphSpecBuilderTests.cs HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs
git add -u HandsLiftedApp.Core/Render/Skia/Builders/ScriptureSlideSpecBuilder.cs HandsLiftedApp.Tests/Render/Skia/Builders/ScriptureSlideSpecBuilderTests.cs
git commit -m "feat: add ScriptureParagraphSpecBuilder, remove one-verse-per-slide ScriptureSlideSpecBuilder"
```

---

### Task 6: Minimal Theme-picker UI for Scripture items

**Files:**
- Modify: `HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml`

**Interfaces:**
- Consumes: `ScriptureItemInstance.ResolvedDesignTheme` (Task 4).
- Produces: nothing further downstream — this is the last task in the plan.

This is the last task; it's a small, targeted UI addition giving the user a way to actually change a Scripture item's `Design` (and thus its font/background) without a full Phase 4b editor. No automated test — this codebase has no Avalonia UI test harness (same precedent as `SetupWindow` and `ScriptureAddDialog` in earlier plans on this branch); verified by build-clean + full-suite-green plus a manual description.

- [ ] **Step 1: Add the `ScriptureItemInstance` `DataTemplate`**

In `HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml`, find the existing `DataTemplate x:Key="SongItemInstance"` block (the first one in the file, using a "Theme" `Button`/`Flyout`/`ComboBox` bound to `ParentPlaylist.Designs`/`SelectedItem="{Binding ResolvedDesignTheme}"`). Insert a new template immediately after its closing `</DataTemplate>` and before the next one (`PowerPointPresentationItemInstance`):

```xml
                <DataTemplate x:Key="ScriptureItemInstance" x:DataType="items1:ScriptureItemInstance">
                    <StackPanel Orientation="Horizontal">
                        <Button
                            Padding="22,6"
                            Margin="6 6 0 6"
                            Background="{DynamicResource EditButtonBackgroundBrush}"
                            BorderBrush="#b3aed9"
                            BorderThickness="1"
                            CornerRadius="4">
                            <TextBlock>Theme</TextBlock>
                            <Button.Flyout>
                                <Flyout Placement="Bottom">
                                    <ComboBox
                                        ItemsSource="{Binding ParentPlaylist.Designs}"
                                        SelectedItem="{Binding ResolvedDesignTheme}"
                                        MinWidth="180">
                                        <ComboBox.ItemTemplate>
                                            <DataTemplate>
                                                <TextBlock Text="{Binding Name}" />
                                            </DataTemplate>
                                        </ComboBox.ItemTemplate>
                                    </ComboBox>
                                </Flyout>
                            </Button.Flyout>
                        </Button>
                    </StackPanel>
                </DataTemplate>
```

This is byte-for-byte the same structure as the existing `SongItemInstance` template (same `Button`/`Flyout`/`ComboBox`/binding paths) — only the `x:Key`/`x:DataType` differ, since `items1:ScriptureItemInstance` (the `xmlns:items1="clr-namespace:HandsLiftedApp.Core.Models.RuntimeData.Items"` alias, already declared at the top of this file and already used by other templates) is the type this template renders for.

- [ ] **Step 2: Build and run the full test suite**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --nologo`
Expected: build succeeds, no XAML/compile errors.

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, same count as end of Task 5 (this task adds no tests) — confirms nothing else broke.

- [ ] **Step 3: Manual verification**

Run the app (check `docs/superpowers/HANDOVER.md` or the repo's build docs for the exact launch command if unsure). With Bible data already downloaded (via Setup's "Download Bible Data" button) and a Scripture item already inserted (via the add-item flyout's "Scripture" entry):

1. Select the Scripture item in the playlist's item list; confirm the edit-dock area at the bottom shows a "Theme" button (same visual style as a Song item's Theme button).
2. Click it, confirm a `ComboBox` flyout opens listing the playlist's available themes/designs.
3. Pick a different theme; confirm the Scripture item's slides regenerate (font/background visibly change, and if the new theme's font size is meaningfully larger/smaller, confirm the number of slides for that passage changes accordingly).

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml
git commit -m "feat: add Theme picker for Scripture items in the item edit dock"
```

---

## Final Whole-Branch Review

After all 6 tasks: full suite should be at 145 tests. Tally: 137 baseline + 7 (Task 1) + 4 (Task 2) + 0 (Task 3, extends 2 existing tests, adds none) + 0 (Task 4, rewrites 6 old methods into 6 new ones, net zero) − 8 + 5 (Task 5, deletes the old builder's 8-test file, adds a new 5-test file) + 0 (Task 6) = 145. Confirm `grep -rn "ScriptureSlideSpecBuilder" --include=*.cs .` (excluding `docs/`) returns nothing, and confirm no other code path still constructs a per-verse slide (`grep -rn "UpdateVerseSlides" --include=*.cs .` returns nothing outside `docs/`).
