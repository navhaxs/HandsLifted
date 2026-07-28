# Scripture Data Model + Slide Generation (Phase 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Given a scripture passage reference (translation, book, chapter/verse range), produce a correctly-ordered, correctly-diffed list of one-slide-per-verse runtime slide objects — mirroring the existing `SongItem`/`SongSlide`/`SongItemInstance` pattern, but scoped narrowly: no self-rendering, no thumbnails, no Library/persistence wiring. Those are Phase 3 and Phase 4's jobs respectively.

**Architecture:** Two new plain data classes in `HandsLiftedApp.Data` (`ScriptureItem : Item`, `ScriptureSlide : Slide`), and two new runtime classes in `HandsLiftedApp.Core` (`ScriptureSlideInstance : ScriptureSlide, ISlideInstance` and `ScriptureItemInstance : ScriptureItem, IItemInstance, IItemDirtyBit`), following the exact file-placement and namespace conventions of their Song equivalents (namespace matches the base class's own namespace, not the physical project the file lives in — this is real, established precedent, see `SongSlideInstance.cs:21` living in `HandsLiftedApp.Core/Models/RuntimeData/Slides/` under `namespace HandsLiftedApp.Data.Slides`).

**Tech Stack:** .NET 8, MSTest, ReactiveUI (`WhenAnyValue`/`RaiseAndSetIfChanged`/`ObservableAsPropertyHelper`) — matching `SongItemInstance.cs`'s established reactive patterns.

## Global Constraints

- Target framework: `net8.0`, MSTest, matches Phase 1.
- `HandsLiftedApp.Importer.Scripture` (Phase 1's project) is referenced by `HandsLiftedApp.Core.csproj` for the first time in this phase (add alongside the other importer references at `HandsLiftedApp.Core.csproj:72-82`).
- **Deviation from the design spec, deliberate:** the spec (`docs/superpowers/specs/2026-07-26-scripture-slide-type-design.md`) described `ScriptureItem` caching parsed `CachedVerses` content. This plan does NOT do that — `HandsLiftedApp.Data` has no dependency on `HandsLiftedApp.Importer.Scripture` today (confirmed dependency direction: `Data → Utils` only; importer projects are consumed by `Core`, not `Data`), and adding one would invert that layering for a benefit Phase 1 already provides: `ScriptureSourceLoader` already caches raw USX XML in memory and on disk, so re-fetching the same book is already fast without a network round-trip. `ScriptureItem` stores only the passage reference (translation/book/chapter/verse range); `ScriptureItemInstance` re-fetches (cache-backed) and re-parses each time `GenerateSlidesAsync()` is called.
- **Splitting rule confirmed: one verse per slide** (not one-paragraph-per-slide — this was explicitly re-confirmed after Phase 1 raised the question, overriding an earlier mid-session suggestion to group by paragraph). Phase 1's `ScriptureParagraph.IsVerseContinuation` flag exists specifically so this phase can correctly *merge* a verse's text back together when the source USX split it across two `<para>` elements — see Task 2.
- No self-rendering: `ScriptureSlideInstance` does NOT implement `IRenderable`, has no `Theme` property, and never populates `Cached`/`Thumbnail` (they stay `null`). This is confirmed safe — Avalonia's `Image.Source` binds gracefully to a null `Bitmap?` (blank tile, no crash). Phase 3 adds the spec builder and wires up self-rendering exactly the way `SongSlideInstance.cs:83-95`'s `RequestRender()`/`Render()` do.
- No Library/persistence: no `ScriptureLibrary`, no `LibraryType.Scripture` enum value, no `CreateItem`/`ItemInstanceFactory` wiring. Nothing consumes those until Phase 4's editor UI exists.

---

### Task 1: Data-layer models (`ScriptureItem`, `ScriptureSlide`) + Core project reference

**Files:**
- Create: `HandsLiftedApp.Data/Models/Items/ScriptureItem.cs`
- Create: `HandsLiftedApp.Data/Slides/ScriptureSlide.cs`
- Modify: `HandsLiftedApp.Core/HandsLiftedApp.Core.csproj` (add `HandsLiftedApp.Importer.Scripture` project reference)
- Test: `HandsLiftedApp.Tests/Models/Items/ScriptureItemTests.cs`
- Test: `HandsLiftedApp.Tests/Slides/ScriptureSlideTests.cs`

**Interfaces:**
- Produces: `ScriptureItem : Item` with settable `Translation` (string), `Book` (string), `StartChapter`/`StartVerse`/`EndChapter`/`EndVerse` (int) — namespace `HandsLiftedApp.Data.Models.Items`.
- Produces: `ScriptureSlide : Slide` with constructor `ScriptureSlide(ScriptureItem? parentScriptureItem, string id)`, settable `Text`/`Label` (string), `ParentScriptureItem` (get-only `ScriptureItem?`) — namespace `HandsLiftedApp.Data.Slides`. Task 3 (`ScriptureSlideInstance`) and Task 4 (`ScriptureItemInstance`) build on these directly.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Models/Items/ScriptureItemTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Tests.Models.Items;

[TestClass]
public class ScriptureItemTests
{
    [TestMethod]
    public void ScriptureItem_DefaultsToChapterOneVerseOne()
    {
        var item = new ScriptureItem();

        Assert.AreEqual("", item.Translation);
        Assert.AreEqual("", item.Book);
        Assert.AreEqual(1, item.StartChapter);
        Assert.AreEqual(1, item.StartVerse);
        Assert.AreEqual(1, item.EndChapter);
        Assert.AreEqual(1, item.EndVerse);
    }

    [TestMethod]
    public void ScriptureItem_PropertiesAreSettable()
    {
        var item = new ScriptureItem
        {
            Translation = "eng_bsb",
            Book = "JHN",
            StartChapter = 3,
            StartVerse = 16,
            EndChapter = 3,
            EndVerse = 21
        };

        Assert.AreEqual("eng_bsb", item.Translation);
        Assert.AreEqual("JHN", item.Book);
        Assert.AreEqual(3, item.StartChapter);
        Assert.AreEqual(16, item.StartVerse);
        Assert.AreEqual(3, item.EndChapter);
        Assert.AreEqual(21, item.EndVerse);
    }
}
```

`HandsLiftedApp.Tests/Slides/ScriptureSlideTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Tests.Slides;

[TestClass]
public class ScriptureSlideTests
{
    [TestMethod]
    public void ScriptureSlide_ExposesTextLabelAndParent()
    {
        var item = new ScriptureItem { Book = "JHN" };
        var slide = new ScriptureSlide(item, "3:16") { Text = "For God so loved the world...", Label = "John 3:16" };

        Assert.AreEqual("3:16", slide.Id);
        Assert.AreEqual("For God so loved the world...", slide.Text);
        Assert.AreEqual("John 3:16", slide.Label);
        Assert.AreEqual("For God so loved the world...", slide.SlideText);
        Assert.AreEqual("John 3:16", slide.SlideLabel);
        Assert.AreSame(item, slide.ParentScriptureItem);
    }

    [TestMethod]
    public void ScriptureSlide_EqualityIsById()
    {
        var slideA = new ScriptureSlide(null, "3:16") { Text = "first text" };
        var slideB = new ScriptureSlide(null, "3:16") { Text = "different text" };
        var slideC = new ScriptureSlide(null, "3:17");

        Assert.IsTrue(slideA.Equals(slideB));
        Assert.IsFalse(slideA.Equals(slideC));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureItemTests|FullyQualifiedName~ScriptureSlideTests"`
Expected: FAIL — compile error, `ScriptureItem`/`ScriptureSlide` don't exist yet.

- [ ] **Step 3: Implement the models**

`HandsLiftedApp.Data/Models/Items/ScriptureItem.cs`:

```csharp
using System;
using System.Xml.Serialization;
using ReactiveUI;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("Scripture", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class ScriptureItem : Item
    {
        private string _translation = "";
        public string Translation { get => _translation; set => this.RaiseAndSetIfChanged(ref _translation, value); }

        private string _book = "";
        public string Book { get => _book; set => this.RaiseAndSetIfChanged(ref _book, value); }

        private int _startChapter = 1;
        public int StartChapter { get => _startChapter; set => this.RaiseAndSetIfChanged(ref _startChapter, value); }

        private int _startVerse = 1;
        public int StartVerse { get => _startVerse; set => this.RaiseAndSetIfChanged(ref _startVerse, value); }

        private int _endChapter = 1;
        public int EndChapter { get => _endChapter; set => this.RaiseAndSetIfChanged(ref _endChapter, value); }

        private int _endVerse = 1;
        public int EndVerse { get => _endVerse; set => this.RaiseAndSetIfChanged(ref _endVerse, value); }
    }
}
```

`HandsLiftedApp.Data/Slides/ScriptureSlide.cs`:

```csharp
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;

namespace HandsLiftedApp.Data.Slides
{
    public class ScriptureSlide : Slide
    {
        public string Id { get; set; }

        public ScriptureSlide(ScriptureItem? parentScriptureItem, string id)
        {
            ParentScriptureItem = parentScriptureItem;
            Id = id;
        }

        private string _text = "";
        public string Text
        {
            get => _text;
            set => this.RaiseAndSetIfChanged(ref _text, value);
        }

        private string _label = "";
        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }

        public override string? SlideText => Text;

        public override string? SlideLabel => Label;

        public ScriptureItem? ParentScriptureItem { get; } = null;

        public override bool Equals(Object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                ScriptureSlide p = (ScriptureSlide)obj;
                return (Id == p.Id);
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureItemTests|FullyQualifiedName~ScriptureSlideTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Add the Core → Importer.Scripture project reference**

Edit `HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`, in the `<ItemGroup>` containing the other `ProjectReference` entries (around line 72-82), add a new line alongside the other importer references:

```xml
        <ProjectReference Include="..\HandsLiftedApp.Importer.Scripture\HandsLiftedApp.Importer.Scripture.csproj" />
```

- [ ] **Step 6: Run the full suite to confirm the new reference builds cleanly**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 93 (Phase 1) + 4 (this task) = 97, no build errors from the new project reference.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Data/Models/Items/ScriptureItem.cs HandsLiftedApp.Data/Slides/ScriptureSlide.cs HandsLiftedApp.Core/HandsLiftedApp.Core.csproj HandsLiftedApp.Tests/Models/Items/ScriptureItemTests.cs HandsLiftedApp.Tests/Slides/ScriptureSlideTests.cs
git commit -m "feat: add ScriptureItem/ScriptureSlide data models, reference Importer.Scripture from Core"
```

---

### Task 2: Verse-range extraction + split-verse merge (pure logic)

**Files:**
- Create: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureVerseRangeExtractor.cs`
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureVerseRangeExtractorTests.cs`

**Interfaces:**
- Consumes: `ScriptureBook`/`ScriptureParagraph`/`ScriptureVerseSegment` from `HandsLiftedApp.Importer.Scripture.Models` (Phase 1).
- Produces: `public readonly record struct ScriptureVerseRef(int Chapter, int Verse, string Text);` and `public static class ScriptureVerseRangeExtractor { public static List<ScriptureVerseRef> Extract(ScriptureBook book, int startChapter, int startVerse, int endChapter, int endVerse); }` — Task 4 (`ScriptureItemInstance`) calls this directly.

**Why this is its own task:** this is the one genuinely tricky piece of logic in Phase 2 — flattening `ScriptureBook`'s paragraph-grouped, sometimes-split verse segments into exactly one entry per logical verse, in range, chapter-aware. It deserves isolated, thorough testing against the exact same `BsbShapedUsx` fixture Phase 1 used, before `ScriptureItemInstance` (Task 4) wires it into the reactive/diffing machinery.

**Design note:** Phase 1's `UsxScriptureParser` can legitimately split one verse's text across two adjacent `ScriptureParagraph`s (verse 27 splits into three fragments across q1/q2/q2 in the BSB fixture), each fragment marked via `ScriptureParagraph.IsVerseContinuation`. Naively iterating segments without checking this flag would produce two or three separate one-verse-per-slide entries all labeled with the same verse — wrong. `Extract` must merge a continuation fragment into the immediately-preceding result entry (by chapter+verse match) rather than emitting a new one.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureVerseRangeExtractorTests.cs`:

```csharp
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.Scripture;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureVerseRangeExtractorTests
{
    // Same fixture as Phase 1's UsxScriptureParserTests.BsbShapedUsx — kept in sync
    // deliberately so this test exercises the exact same split-verse shapes.
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
          <chapter number="2" style="c" sid="GEN 2" />
          <para style="p">
            <verse number="1" style="v" sid="GEN 2:1" />Thus the heavens and the earth were completed.<verse eid="GEN 2:1" /></para>
          <chapter eid="GEN 2" />
        </usx>
        """;

    [TestMethod]
    public void Extract_FullRange_MergesVerseFiveSplitAcrossParagraphs()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(BsbShapedUsx));

        var verses = ScriptureVerseRangeExtractor.Extract(book, startChapter: 1, startVerse: 3, endChapter: 1, endVerse: 5);

        Assert.AreEqual(3, verses.Count);
        Assert.AreEqual(5, verses[2].Verse);
        Assert.AreEqual(1, verses[2].Chapter);
        Assert.AreEqual(
            "God called the light “day,” and the darkness He called “night.” And there was evening, and there was morning — the first day.",
            verses[2].Text);
    }

    [TestMethod]
    public void Extract_FullRange_MergesVerseTwentySevenSplitAcrossThreeParagraphs()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(BsbShapedUsx));

        var verses = ScriptureVerseRangeExtractor.Extract(book, startChapter: 1, startVerse: 27, endChapter: 1, endVerse: 27);

        Assert.AreEqual(1, verses.Count);
        Assert.AreEqual(27, verses[0].Verse);
        Assert.AreEqual(
            "So God created man in His own image; in the image of God He created him; male and female He created them.",
            verses[0].Text);
    }

    [TestMethod]
    public void Extract_RangeExcludingVerseFive_StopsAtVerseFour()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(BsbShapedUsx));

        var verses = ScriptureVerseRangeExtractor.Extract(book, startChapter: 1, startVerse: 3, endChapter: 1, endVerse: 4);

        Assert.AreEqual(2, verses.Count);
        Assert.AreEqual(4, verses[1].Verse);
    }

    [TestMethod]
    public void Extract_RangeSpanningChapters_IncludesBothChapters()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(BsbShapedUsx));

        var verses = ScriptureVerseRangeExtractor.Extract(book, startChapter: 1, startVerse: 27, endChapter: 2, endVerse: 1);

        Assert.AreEqual(2, verses.Count);
        Assert.AreEqual(1, verses[0].Chapter);
        Assert.AreEqual(27, verses[0].Verse);
        Assert.AreEqual(2, verses[1].Chapter);
        Assert.AreEqual(1, verses[1].Verse);
        Assert.AreEqual("Thus the heavens and the earth were completed.", verses[1].Text);
    }

    [TestMethod]
    public void Extract_EmptyRange_ReturnsEmptyList()
    {
        var book = UsxScriptureParser.Parse(XDocument.Parse(BsbShapedUsx));

        var verses = ScriptureVerseRangeExtractor.Extract(book, startChapter: 1, startVerse: 100, endChapter: 1, endVerse: 200);

        Assert.AreEqual(0, verses.Count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureVerseRangeExtractorTests"`
Expected: FAIL — compile error, `ScriptureVerseRangeExtractor`/`ScriptureVerseRef` don't exist yet.

- [ ] **Step 3: Implement the extractor**

`HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureVerseRangeExtractor.cs`:

```csharp
using System.Collections.Generic;
using HandsLiftedApp.Importer.Scripture.Models;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public readonly record struct ScriptureVerseRef(int Chapter, int Verse, string Text);

    public static class ScriptureVerseRangeExtractor
    {
        public static List<ScriptureVerseRef> Extract(
            ScriptureBook book, int startChapter, int startVerse, int endChapter, int endVerse)
        {
            var result = new List<ScriptureVerseRef>();

            foreach (var paragraph in book.Paragraphs)
            {
                for (var i = 0; i < paragraph.Verses.Count; i++)
                {
                    var segment = paragraph.Verses[i];

                    if (!IsWithinRange(paragraph.StartChapter, segment.VerseNumber, startChapter, startVerse, endChapter, endVerse))
                    {
                        continue;
                    }

                    var isContinuationOfPrevious = i == 0
                        && paragraph.IsVerseContinuation
                        && result.Count > 0
                        && result[^1].Chapter == paragraph.StartChapter
                        && result[^1].Verse == segment.VerseNumber;

                    if (isContinuationOfPrevious)
                    {
                        var previous = result[^1];
                        result[^1] = previous with { Text = previous.Text + " " + segment.Text };
                    }
                    else
                    {
                        result.Add(new ScriptureVerseRef(paragraph.StartChapter, segment.VerseNumber, segment.Text));
                    }
                }
            }

            return result;
        }

        private static bool IsWithinRange(
            int chapter, int verse, int startChapter, int startVerse, int endChapter, int endVerse)
        {
            if (chapter < startChapter || chapter > endChapter)
            {
                return false;
            }

            if (chapter == startChapter && verse < startVerse)
            {
                return false;
            }

            if (chapter == endChapter && verse > endVerse)
            {
                return false;
            }

            return true;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureVerseRangeExtractorTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 97 + 5 = 102.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureVerseRangeExtractor.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureVerseRangeExtractorTests.cs
git commit -m "feat: add ScriptureVerseRangeExtractor, merging split verses across paragraphs"
```

---

### Task 3: `ScriptureSlideInstance` (minimal runtime slide)

**Files:**
- Create: `HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs`
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Slides/ScriptureSlideInstanceTests.cs`

**Interfaces:**
- Consumes: `ScriptureSlide`/`ScriptureItem` (Task 1).
- Produces: `public class ScriptureSlideInstance : ScriptureSlide, ISlideInstance` with constructor `ScriptureSlideInstance(ScriptureItem? parentScriptureItem, string id, string? text = null, string? label = null)`, plus `Cached`/`Thumbnail` (`Bitmap?`, always null in this phase), `SlideTimerConfig` (always null), `SlideThumbnailBadge` (always null). Task 4 (`ScriptureItemInstance`) constructs these directly.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Models/RuntimeData/Slides/ScriptureSlideInstanceTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Slides;

[TestClass]
public class ScriptureSlideInstanceTests
{
    [TestMethod]
    public void Constructor_SetsTextAndLabelWhenProvided()
    {
        var slide = new ScriptureSlideInstance(null, "1:1", text: "In the beginning...", label: "Genesis 1:1");

        Assert.AreEqual("1:1", slide.Id);
        Assert.AreEqual("In the beginning...", slide.Text);
        Assert.AreEqual("Genesis 1:1", slide.Label);
    }

    [TestMethod]
    public void Constructor_LeavesDefaultsWhenTextAndLabelOmitted()
    {
        var slide = new ScriptureSlideInstance(null, "1:1");

        Assert.AreEqual("", slide.Text);
        Assert.AreEqual("", slide.Label);
    }

    [TestMethod]
    public void CachedAndThumbnail_DefaultToNull()
    {
        var slide = new ScriptureSlideInstance(null, "1:1");

        Assert.IsNull(slide.Cached);
        Assert.IsNull(slide.Thumbnail);
        Assert.IsNull(slide.SlideTimerConfig);
        Assert.IsNull(slide.SlideThumbnailBadge);
    }

    [TestMethod]
    public void ParentScriptureItem_IsPassedThroughToBase()
    {
        var item = new ScriptureItem { Book = "JHN" };
        var slide = new ScriptureSlideInstance(item, "3:16");

        Assert.AreSame(item, slide.ParentScriptureItem);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureSlideInstanceTests"`
Expected: FAIL — compile error, `ScriptureSlideInstance` doesn't exist yet.

- [ ] **Step 3: Implement `ScriptureSlideInstance`**

`HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs`:

```csharp
using Avalonia.Media.Imaging;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    // No IRenderable / self-rendering yet — Phase 3 adds ScriptureSlideSpecBuilder
    // and wires up reactive rendering the way SongSlideInstance does.
    public class ScriptureSlideInstance : ScriptureSlide, ISlideInstance
    {
        public ScriptureSlideInstance(ScriptureItem? parentScriptureItem, string id, string? text = null, string? label = null)
            : base(parentScriptureItem, id)
        {
            if (text != null) Text = text;
            if (label != null) Label = label;
        }

        private Bitmap? _cached;
        public Bitmap? Cached
        {
            get => _cached;
            set => this.RaiseAndSetIfChanged(ref _cached, value);
        }

        private Bitmap? _thumbnail;
        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }

        public ItemAutoAdvanceTimer? SlideTimerConfig => null;

        public SlideThumbnailBadge? SlideThumbnailBadge => null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureSlideInstanceTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 102 + 4 = 106.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs HandsLiftedApp.Tests/Models/RuntimeData/Slides/ScriptureSlideInstanceTests.cs
git commit -m "feat: add minimal ScriptureSlideInstance (no self-rendering yet)"
```

---

### Task 4: `ScriptureItemInstance` (fetch, generate, diff)

**Files:**
- Create: `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs`

**Interfaces:**
- Consumes: `ScriptureItem` (Task 1), `ScriptureSlideInstance` (Task 3), `ScriptureVerseRangeExtractor.Extract(...)` (Task 2), `ScriptureSourceLoader`/`UsxScriptureParser` (Phase 1).
- Produces: `public class ScriptureItemInstance : ScriptureItem, IItemInstance, IItemDirtyBit` with constructor `ScriptureItemInstance(PlaylistInstance? parentPlaylist, ScriptureSourceLoader? loader = null)`, `Task GenerateSlidesAsync()`, and the `IItemInstance`/`IItemDirtyBit` members (`ParentPlaylist`, `SelectedSlideIndex`, `ActiveSlide`, `Slides`, `ItemDataModified`). This is the last file in Phase 2 — Phase 3 extends `ScriptureSlideInstance` (adds rendering) and Phase 4 builds the editor UI that constructs and calls into this class.

**Design note (diffing):** mirrors `SongItemInstance.UpdateStanzaSlides()` (`SongItemInstance.cs:186-336`) exactly in spirit: rebuild the slide list from scratch on every `GenerateSlidesAsync()` call, but reuse existing `ScriptureSlideInstance` objects (updating their `Text`/`Label` in place) wherever a slide with the same `Id` (here: `"{chapter}:{verse}"`) already exists, so slide *identity* survives a regenerate (e.g. the user tweaks the end verse and regenerates — slides for unchanged verses keep the same object reference, which matters for UI selection state and any future rendering-cache reuse).

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs`:

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.Scripture;
using HandsLiftedApp.Tests.Importer.Scripture;

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

    private static ScriptureSourceLoader MakeFakeLoader(string xml)
    {
        // Reuses Phase 1's internal FakeHttpMessageHandler (HandsLiftedApp.Tests/Importer/Scripture/FakeHttpMessageHandler.cs) —
        // `internal` is assembly-scoped, so it's visible here without changing its accessibility.
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml) });
        return new ScriptureSourceLoader(new HttpClient(handler), System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "HandsLiftedScriptureItemInstanceTests_" + System.Guid.NewGuid().ToString("N")));
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_ProducesOneSlidePerVerseInRange()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeLoader(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();

        Assert.AreEqual(2, instance.Slides.Count);
        var first = (ScriptureSlideInstance)instance.Slides[0];
        var second = (ScriptureSlideInstance)instance.Slides[1];
        Assert.AreEqual("In the beginning God created the heaven and the earth.", first.Text);
        Assert.AreEqual("And the earth was without form, and void.", second.Text);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_SecondCallWithSameRange_PreservesSlideIdentity()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeLoader(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        var firstCallSlide = instance.Slides[0];

        await instance.GenerateSlidesAsync();
        var secondCallSlide = instance.Slides[0];

        Assert.AreSame(firstCallSlide, secondCallSlide);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_NarrowerRangeOnRegenerate_ShrinksSlideList()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeLoader(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 3
        };
        await instance.GenerateSlidesAsync();
        Assert.AreEqual(3, instance.Slides.Count);

        instance.EndVerse = 2;
        await instance.GenerateSlidesAsync();

        Assert.AreEqual(2, instance.Slides.Count);
    }

    [TestMethod]
    public async Task ActiveSlide_TracksSelectedSlideIndex()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeLoader(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };
        await instance.GenerateSlidesAsync();

        instance.SelectedSlideIndex = 1;

        Assert.AreSame(instance.Slides[1], instance.ActiveSlide);
    }
}
```

**Note for the implementer:** do not modify `HandsLiftedApp.Tests/Importer/Scripture/FakeHttpMessageHandler.cs` (from Phase 1) — it's already `internal` (assembly-scoped, not namespace-scoped), so the `using HandsLiftedApp.Tests.Importer.Scripture;` import above makes it visible here without any accessibility change.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureItemInstanceTests"`
Expected: FAIL — compile error, `ScriptureItemInstance` doesn't exist yet.

- [ ] **Step 3: Implement `ScriptureItemInstance`**

`HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.Scripture;
using ReactiveUI;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class ScriptureItemInstance : ScriptureItem, IItemInstance, IItemDirtyBit
    {
        public PlaylistInstance? ParentPlaylist { get; set; }

        public event EventHandler? ItemDataModified;

        private readonly ScriptureSourceLoader _loader;

        public ScriptureItemInstance(PlaylistInstance? parentPlaylist, ScriptureSourceLoader? loader = null) : base()
        {
            ParentPlaylist = parentPlaylist;
            _loader = loader ?? new ScriptureSourceLoader();

            // Deliberately no .ObserveOn(RxApp.MainThreadScheduler) here (unlike SongItemInstance's
            // equivalent chain): that scheduler depends on Avalonia.ReactiveUI's dispatcher registration,
            // which isn't guaranteed to run in a unit-test host, and this phase does nothing that
            // requires cross-thread marshaling. Keeping it synchronous makes ActiveSlide update
            // deterministically and immediately when SelectedSlideIndex or Slides changes.
            _activeSlide = this.WhenAnyValue(x => x.SelectedSlideIndex, x => x.Slides,
                    (selectedSlideIndex, slides) => slides.ElementAtOrDefault(selectedSlideIndex))
                .ToProperty(this, x => x.ActiveSlide);

            this.WhenAnyValue(
                i => i.Title,
                i => i.Translation,
                i => i.Book,
                i => i.StartChapter,
                i => i.StartVerse,
                i => i.EndChapter,
                i => i.EndVerse
            ).Subscribe(_ =>
            {
                ItemDataModified?.Invoke(this, EventArgs.Empty);
            });
        }

        private ObservableCollection<Slide> _slides = new ObservableCollection<Slide>();
        public ObservableCollection<Slide> Slides => _slides;

        private int _selectedSlideIndex = -1;
        public int SelectedSlideIndex
        {
            get => _selectedSlideIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedSlideIndex, value);
        }

        private readonly ObservableAsPropertyHelper<Slide> _activeSlide;
        public Slide ActiveSlide => _activeSlide.Value;

        public async Task GenerateSlidesAsync()
        {
            var book = await _loader.LoadBookAsync(Translation, Book).ConfigureAwait(false);
            var verses = ScriptureVerseRangeExtractor.Extract(book, StartChapter, StartVerse, EndChapter, EndVerse);
            UpdateVerseSlides(verses);
        }

        private void UpdateVerseSlides(System.Collections.Generic.List<ScriptureVerseRef> verses)
        {
            var newSlides = new ObservableCollection<Slide>();

            foreach (var v in verses)
            {
                var slideId = $"{v.Chapter}:{v.Verse}";
                var label = $"{Book} {v.Chapter}:{v.Verse}";

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
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureItemInstanceTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 106 + 4 = 110, no regressions.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs
git commit -m "feat: add ScriptureItemInstance — fetch, extract verse range, diff into slides"
```

---

## What This Phase Does Not Cover

- No self-rendering / `Cached`/`Thumbnail` population, no `ScriptureSlideSpecBuilder`, no `LivePane`/`ProjectorWindow` wiring — Phase 3.
- No `ScriptureLibrary`, no `LibraryType.Scripture`, no `CreateItem`/`ItemInstanceFactory` wiring, no save/load to disk — Phase 4, alongside the editor UI that's the first real consumer of persistence.
- No editor UI, no "Add Scripture" library entry point — Phase 4.
- No `DataTemplate` for `ScriptureSlideInstance` in `MainView.axaml`'s thumbnail strip — needed before this is visible in a running app, but naturally belongs with Phase 3's rendering work (the same phase that gives the thumbnail something real to paint).
