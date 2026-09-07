# PPTX Embedded Video Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a PowerPoint slide's main content is an embedded video, import it as a real playable
video slide (audio + playback) instead of the current frozen static frame.

**Architecture:** A new `EmbeddedVideoExtractor` (pure zip/OOXML parsing, no COM/Syncfusion
dependency) pulls embedded video files out of the `.pptx` package and drops them into the same
per-file cache directory the existing PDF→PNG pipeline already writes to, replacing the PNG for
that slide. The existing `ApplySlidesFromDirectory` → `GenerateSlides` → `CreateItem.GenerateMediaContentSlide`
pipeline already picks `VideoSlideInstance` vs `ImageSlideInstance` purely by file extension, so no
rendering code changes at all — only the file that lands in the cache directory changes.

**Tech Stack:** C# / .NET 10, `System.IO.Compression` (ZipArchive), `System.Xml.Linq`, MSTest.

**Spec:** [docs/superpowers/specs/2026-09-07-pptx-embedded-video-import-design.md](../specs/2026-09-07-pptx-embedded-video-import-design.md)

## Global Constraints

- V1 is whole-slide-video only — no compositing video over other slide content.
- Embedded video only — a `TargetMode="External"` (linked) video is not extracted; slide keeps its
  static PNG and a warning is recorded.
- First video per slide only if a slide somehow has more than one.
- No PowerPoint-authored trim/volume/autoplay/loop settings are read — extracted video plays with
  VisionScreens' own video-slide defaults.
- `HandsLiftedApp.Importer.PowerPointLib` **cannot reference `HandsLiftedApp.Core`** — `Core`
  depends on `PowerPointLib`, not the other way around, and `Core.Constants` is `internal` besides.
  The supported-video-extension list must be duplicated locally in `EmbeddedVideoExtractor`, with a
  comment noting it must be kept in sync with `HandsLiftedApp.Core.Constants.SUPPORTED_VIDEO`.
- Shown-slide numbering (the extractor's slide index) must exactly match `ConvertPDF`'s PDF-page
  numbering — both exclude hidden slides (`<p:sld show="0">`), and both zero-pad using the same
  `(int)Math.Floor(Math.Log10(count) + 1)` digit-count formula.
- Test framework is MSTest (`[TestClass]`/`[TestMethod]`/`Assert`/`StringAssert`/`CollectionAssert`),
  matching every existing test in `HandsLiftedApp.Tests`. No committed binary `.pptx` fixture files —
  build minimal test packages in-memory via `System.IO.Compression.ZipArchive`, matching this repo's
  existing style (no committed fixtures exist for `EmbeddedFontExtractor` either).
- Tooling rule: never shell out to `find`/`grep`; use Glob/Grep tools, scoped to a project subfolder.

---

### Task 1: `EmbeddedVideoExtractor`

**Files:**
- Create: `HandsLiftedApp.Importer.PowerPointLib/EmbeddedVideoExtractor.cs`
- Modify: `HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj` (add project reference)
- Test: `HandsLiftedApp.Tests/Importer/PowerPointLib/EmbeddedVideoExtractorTests.cs`

**Interfaces:**
- Consumes: nothing from other tasks (this is the foundation task).
- Produces:
  - `HandsLiftedApp.Importer.PowerPointLib.PowerPointVideoExtractionResult` — public class with
    `Dictionary<int, string> SlideIndexToVideoFile { get; init; }` and `List<string> Warnings { get; init; }`.
  - `HandsLiftedApp.Importer.PowerPointLib.EmbeddedVideoExtractor.ExtractVideos(string pptxPath, string outputDirectory) -> PowerPointVideoExtractionResult` — static, never throws.
  - `HandsLiftedApp.Importer.PowerPointLib.EmbeddedVideoExtractor.WarningsFileName` — `public const string`, used by Task 2/3 as the sidecar filename.

- [ ] **Step 1: Add the test project reference**

`HandsLiftedApp.Tests` doesn't currently reference `HandsLiftedApp.Importer.PowerPointLib`. Add it
to `HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj`, inside the existing `<ItemGroup>` that has the
other `ProjectReference` entries:

```xml
    <ProjectReference Include="..\HandsLiftedApp.Importer.PowerPointLib\HandsLiftedApp.Importer.PowerPointLib.csproj" />
```

- [ ] **Step 2: Write the failing tests**

Create `HandsLiftedApp.Tests/Importer/PowerPointLib/EmbeddedVideoExtractorTests.cs`:

```csharp
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.PowerPointLib;

namespace HandsLiftedApp.Tests.Importer.PowerPointLib;

[TestClass]
public class EmbeddedVideoExtractorTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "EmbeddedVideoExtractorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private sealed record TestSlide(bool Hidden, string? VideoTarget, string? VideoTargetMode);

    private string CreateTestPptx(TestSlide[] slides)
    {
        var pptxPath = Path.Combine(_tempDir, "test.pptx");
        using (var archive = ZipFile.Open(pptxPath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "ppt/presentation.xml", BuildPresentationXml(slides.Length));
            WriteEntry(archive, "ppt/_rels/presentation.xml.rels", BuildPresentationRelsXml(slides.Length));

            for (int i = 0; i < slides.Length; i++)
            {
                int slideNum = i + 1;
                var slide = slides[i];

                WriteEntry(archive, $"ppt/slides/slide{slideNum}.xml",
                    BuildSlideXml(slide.Hidden, hasVideo: slide.VideoTarget != null));

                if (slide.VideoTarget != null)
                {
                    WriteEntry(archive, $"ppt/slides/_rels/slide{slideNum}.xml.rels",
                        BuildSlideRelsXml(slide.VideoTarget, slide.VideoTargetMode));
                }
            }

            WriteEntry(archive, "ppt/media/media1.mp4", "dummy-mp4-bytes");
            WriteEntry(archive, "ppt/media/media1.mpg", "dummy-mpg-bytes");
        }
        return pptxPath;
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    private static string BuildPresentationXml(int slideCount)
    {
        var sldIds = string.Join("", Enumerable.Range(0, slideCount)
            .Select(i => $"<p:sldId id=\"{256 + i}\" r:id=\"rId{2 + i}\"/>"));
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                             xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:sldIdLst>{sldIds}</p:sldIdLst>
            </p:presentation>
            """;
    }

    private static string BuildPresentationRelsXml(int slideCount)
    {
        var rels = string.Join("", Enumerable.Range(0, slideCount)
            .Select(i => $"<Relationship Id=\"rId{2 + i}\" " +
                "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" " +
                $"Target=\"slides/slide{i + 1}.xml\"/>"));
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">{rels}</Relationships>
            """;
    }

    private static string BuildSlideXml(bool hidden, bool hasVideo)
    {
        var showAttr = hidden ? " show=\"0\"" : "";
        var videoElement = hasVideo
            ? """<p:pic><p:nvPicPr><p:nvPr><a:videoFile r:link="rId1"/></p:nvPr></p:nvPicPr></p:pic>"""
            : "";
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:sld{showAttr}
                   xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                   xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                   xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:cSld><p:spTree>{videoElement}</p:spTree></p:cSld>
            </p:sld>
            """;
    }

    private static string BuildSlideRelsXml(string videoTarget, string? targetMode)
    {
        var targetModeAttr = targetMode != null ? $" TargetMode=\"{targetMode}\"" : "";
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/video" Target="{videoTarget}"{targetModeAttr}/>
            </Relationships>
            """;
    }

    [TestMethod]
    public void ExtractVideos_NoVideoAnywhere_ReturnsEmptyResult()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: null, VideoTargetMode: null),
            new TestSlide(Hidden: false, VideoTarget: null, VideoTargetMode: null),
        });

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.SlideIndexToVideoFile.Count);
        Assert.AreEqual(0, result.Warnings.Count);
    }

    [TestMethod]
    public void ExtractVideos_SingleEmbeddedVideo_ExtractsAndDeletesSiblingPng()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: "../media/media1.mp4", VideoTargetMode: null),
        });
        var siblingPng = Path.Combine(_tempDir, "Slide.1.png");
        File.WriteAllText(siblingPng, "png-bytes"); // simulates the PDF/PNG step having already run

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.Warnings.Count);
        Assert.IsTrue(result.SlideIndexToVideoFile.ContainsKey(1));
        var extractedPath = result.SlideIndexToVideoFile[1];
        Assert.AreEqual(Path.Combine(_tempDir, "Slide.1.mp4"), extractedPath);
        Assert.AreEqual("dummy-mp4-bytes", File.ReadAllText(extractedPath));
        Assert.IsFalse(File.Exists(siblingPng));
    }

    [TestMethod]
    public void ExtractVideos_HiddenSlideBeforeVideoSlide_NumbersByShownPositionNotPhysicalPosition()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: null, VideoTargetMode: null),                    // shown #1
            new TestSlide(Hidden: true, VideoTarget: null, VideoTargetMode: null),                     // hidden, not numbered
            new TestSlide(Hidden: false, VideoTarget: "../media/media1.mp4", VideoTargetMode: null),   // shown #2
        });

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.Warnings.Count);
        Assert.IsTrue(result.SlideIndexToVideoFile.ContainsKey(2));
        Assert.IsFalse(result.SlideIndexToVideoFile.ContainsKey(3));
        Assert.AreEqual(Path.Combine(_tempDir, "Slide.2.mp4"), result.SlideIndexToVideoFile[2]);
    }

    [TestMethod]
    public void ExtractVideos_ExternallyLinkedVideo_KeepsPngAndRecordsWarning()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: "file:///C:/videos/clip.mp4", VideoTargetMode: "External"),
        });
        var siblingPng = Path.Combine(_tempDir, "Slide.1.png");
        File.WriteAllText(siblingPng, "png-bytes");

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.SlideIndexToVideoFile.Count);
        Assert.AreEqual(1, result.Warnings.Count);
        StringAssert.Contains(result.Warnings[0], "linked to an external file");
        Assert.IsTrue(File.Exists(siblingPng));
    }

    [TestMethod]
    public void ExtractVideos_UnsupportedVideoExtension_KeepsPngAndRecordsWarning()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: "../media/media1.mpg", VideoTargetMode: null),
        });
        var siblingPng = Path.Combine(_tempDir, "Slide.1.png");
        File.WriteAllText(siblingPng, "png-bytes");

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.SlideIndexToVideoFile.Count);
        Assert.AreEqual(1, result.Warnings.Count);
        StringAssert.Contains(result.Warnings[0], "isn't supported");
        Assert.IsTrue(File.Exists(siblingPng));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail to compile**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter EmbeddedVideoExtractorTests
```

Expected: build error — `EmbeddedVideoExtractor` and `PowerPointVideoExtractionResult` don't exist yet.

- [ ] **Step 4: Implement `EmbeddedVideoExtractor`**

Create `HandsLiftedApp.Importer.PowerPointLib/EmbeddedVideoExtractor.cs`:

```csharp
using System.IO.Compression;
using System.Xml.Linq;

namespace HandsLiftedApp.Importer.PowerPointLib;

public class PowerPointVideoExtractionResult
{
    /// <summary>1-based "shown slide" index (matches ConvertPDF's page numbering — hidden slides excluded) -> extracted video file path.</summary>
    public Dictionary<int, string> SlideIndexToVideoFile { get; init; } = new();

    /// <summary>Human-readable messages for slides that had a video but couldn't import it.</summary>
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Pulls embedded videos straight out of a .pptx package so a slide whose main content is a video
/// can be imported as a real playable video slide instead of the static frame the PDF/PNG
/// rasterization pipeline produces. Never throws: video import is additive, not core — any failure
/// just means that slide keeps its static image.
/// </summary>
public static class EmbeddedVideoExtractor
{
    public const string WarningsFileName = "video-import-warnings.txt";

    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

    // Keep in sync with HandsLiftedApp.Core.Constants.SUPPORTED_VIDEO. This project can't reference
    // Core (Core depends on this project), so the list is duplicated here.
    private static readonly HashSet<string> SupportedVideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { "mp4", "flv", "mov", "mkv", "avi", "wmv", "webm" };

    public static PowerPointVideoExtractionResult ExtractVideos(string pptxPath, string outputDirectory)
    {
        var result = new PowerPointVideoExtractionResult();

        try
        {
            using var archive = ZipFile.OpenRead(pptxPath);

            var presentationEntry = archive.GetEntry("ppt/presentation.xml");
            var presentationRelsEntry = archive.GetEntry("ppt/_rels/presentation.xml.rels");
            if (presentationEntry == null || presentationRelsEntry == null) return result;

            XDocument presentationDoc;
            using (var stream = presentationEntry.Open()) presentationDoc = XDocument.Load(stream);

            XDocument presentationRelsDoc;
            using (var stream = presentationRelsEntry.Open()) presentationRelsDoc = XDocument.Load(stream);

            var presentationRelTargets = presentationRelsDoc.Root?
                .Elements(Rel + "Relationship")
                .Where(e => e.Attribute("Id") != null && e.Attribute("Target") != null)
                .ToDictionary(e => e.Attribute("Id")!.Value, e => e.Attribute("Target")!.Value);
            if (presentationRelTargets == null) return result;

            var sldIdList = presentationDoc.Root?.Element(P + "sldIdLst")?.Elements(P + "sldId").ToList();
            if (sldIdList == null || sldIdList.Count == 0) return result;

            // Ordered list of slide part paths that are actually shown, matching the numbering
            // ConvertPDF/PowerPoint's own PDF export produce (hidden slides excluded).
            var shownSlideParts = new List<string>();
            foreach (var sldId in sldIdList)
            {
                var rid = sldId.Attribute(R + "id")?.Value;
                if (rid == null || !presentationRelTargets.TryGetValue(rid, out var target)) continue;

                var slidePartPath = ResolvePresentationRelTarget(target);
                var slideEntry = archive.GetEntry(slidePartPath);
                if (slideEntry == null) continue;

                XDocument slideDoc;
                try
                {
                    using var slideStream = slideEntry.Open();
                    slideDoc = XDocument.Load(slideStream);
                }
                catch (Exception)
                {
                    continue;
                }

                var show = slideDoc.Root?.Attribute("show")?.Value;
                if (show == "0") continue; // hidden slide - excluded from PDF page count too

                shownSlideParts.Add(slidePartPath);
            }

            if (shownSlideParts.Count == 0) return result;

            int maxDigits = (int)Math.Floor(Math.Log10(shownSlideParts.Count) + 1);

            for (int i = 0; i < shownSlideParts.Count; i++)
            {
                try
                {
                    ExtractVideoForSlide(archive, shownSlideParts[i], slideNumber: i + 1, maxDigits, outputDirectory, result);
                }
                catch (Exception)
                {
                    // Skip this slide's video; its static PNG (already produced by the PDF/PNG step) stands in.
                }
            }
        }
        catch (Exception)
        {
            return new PowerPointVideoExtractionResult();
        }

        return result;
    }

    private static void ExtractVideoForSlide(ZipArchive archive, string slidePartPath, int slideNumber,
        int maxDigits, string outputDirectory, PowerPointVideoExtractionResult result)
    {
        var slideEntry = archive.GetEntry(slidePartPath);
        if (slideEntry == null) return;

        XDocument slideDoc;
        using (var stream = slideEntry.Open()) slideDoc = XDocument.Load(stream);

        var videoFileElement = slideDoc.Descendants(A + "videoFile").FirstOrDefault();
        if (videoFileElement == null) return; // no video on this slide

        var rid = videoFileElement.Attribute(R + "link")?.Value;
        if (rid == null) return;

        var slashIndex = slidePartPath.LastIndexOf('/');
        var slideDir = slidePartPath[..slashIndex];
        var slideFileName = slidePartPath[(slashIndex + 1)..];
        var relsPartPath = $"{slideDir}/_rels/{slideFileName}.rels";

        var relsEntry = archive.GetEntry(relsPartPath);
        if (relsEntry == null)
        {
            result.Warnings.Add($"Slide {slideNumber}: video reference found but its relationships couldn't be read.");
            return;
        }

        XDocument relsDoc;
        using (var stream = relsEntry.Open()) relsDoc = XDocument.Load(stream);

        var relationship = relsDoc.Root?.Elements(Rel + "Relationship")
            .FirstOrDefault(e => e.Attribute("Id")?.Value == rid);
        if (relationship == null)
        {
            result.Warnings.Add($"Slide {slideNumber}: video reference found but its relationship couldn't be resolved.");
            return;
        }

        var targetMode = relationship.Attribute("TargetMode")?.Value;
        if (string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add($"Slide {slideNumber}: video is linked to an external file and can't be imported.");
            return;
        }

        var target = relationship.Attribute("Target")?.Value;
        if (string.IsNullOrEmpty(target))
        {
            result.Warnings.Add($"Slide {slideNumber}: video reference is missing its target.");
            return;
        }

        var mediaPartPath = ResolveRelativePartPath(slideDir, target);
        var ext = Path.GetExtension(mediaPartPath).TrimStart('.').ToLowerInvariant();
        if (!SupportedVideoExtensions.Contains(ext))
        {
            result.Warnings.Add($"Slide {slideNumber}: video format '.{ext}' isn't supported.");
            return;
        }

        var mediaEntry = archive.GetEntry(mediaPartPath);
        if (mediaEntry == null)
        {
            result.Warnings.Add($"Slide {slideNumber}: embedded video file couldn't be found in the package.");
            return;
        }

        var paddedNumber = slideNumber.ToString(new string('0', maxDigits));
        var destPath = Path.Combine(outputDirectory, $"Slide.{paddedNumber}.{ext}");
        using (var mediaStream = mediaEntry.Open())
        using (var destStream = File.Create(destPath))
        {
            mediaStream.CopyTo(destStream);
        }

        var siblingPng = Path.Combine(outputDirectory, $"Slide.{paddedNumber}.png");
        if (File.Exists(siblingPng)) File.Delete(siblingPng);

        result.SlideIndexToVideoFile[slideNumber] = destPath;
    }

    private static string ResolvePresentationRelTarget(string target) =>
        target.StartsWith('/') ? target.TrimStart('/') : "ppt/" + target;

    private static string ResolveRelativePartPath(string baseDir, string relativeTarget)
    {
        if (relativeTarget.StartsWith('/')) return relativeTarget.TrimStart('/');

        var combined = baseDir + "/" + relativeTarget;
        var stack = new List<string>();
        foreach (var segment in combined.Split('/'))
        {
            if (segment.Length == 0 || segment == ".") continue;
            if (segment == "..")
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            }
            else
            {
                stack.Add(segment);
            }
        }
        return string.Join('/', stack);
    }
}
```

(`System.IO.Compression` and `System.Xml.Linq` are the only explicit usings needed — `ImplicitUsings`
is enabled on this project, same as `EmbeddedFontExtractor.cs` right next to it.)

- [ ] **Step 5: Run tests to verify they pass**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter EmbeddedVideoExtractorTests
```

Expected: all 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Importer.PowerPointLib/EmbeddedVideoExtractor.cs
git add HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj
git add HandsLiftedApp.Tests/Importer/PowerPointLib/EmbeddedVideoExtractorTests.cs
git commit -m "feat: add EmbeddedVideoExtractor for pulling embedded video out of pptx packages"
```

---

### Task 2: Testable slide-file collection + warning surfacing on `PowerPointPresentationItemInstance`

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs:38-44` (add `LastSyncWarning`)
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs:237-257` (`ApplySlidesFromDirectory`)
- Test: `HandsLiftedApp.Tests/Models/RuntimeData/Items/PowerPointPresentationItemInstanceTests.cs`

**Interfaces:**
- Consumes: `EmbeddedVideoExtractor.WarningsFileName` (Task 1).
- Produces:
  - `PowerPointPresentationItemInstance.LastSyncWarning` — `string?` ReactiveUI property.
  - `PowerPointPresentationItemInstance.CollectSlideFilesInOrder(string targetDirectory) -> List<string>` — `internal static`.
  - `PowerPointPresentationItemInstance.ReadSyncWarnings(string targetDirectory) -> string?` — `internal static`.
  (Both `internal` methods are visible to `HandsLiftedApp.Tests` via the existing
  `[assembly: InternalsVisibleTo("HandsLiftedApp.Tests")]` in `HandsLiftedApp.Core/AssemblyInfo.cs`.)

- [ ] **Step 1: Write the failing tests**

Create `HandsLiftedApp.Tests/Models/RuntimeData/Items/PowerPointPresentationItemInstanceTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Importer.PowerPointLib;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class PowerPointPresentationItemInstanceTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PowerPointPresentationItemInstanceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void CollectSlideFilesInOrder_MixOfImagesAndVideos_ReturnsAllInNaturalSortOrder()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Slide.1.png"), "png");
        File.WriteAllText(Path.Combine(_tempDir, "Slide.2.mp4"), "mp4");
        File.WriteAllText(Path.Combine(_tempDir, "Slide.10.png"), "png");
        File.WriteAllText(Path.Combine(_tempDir, "Slide.9.webm"), "webm");
        // Non-media files must be ignored (e.g. the intermediate PDF, or our own warnings sidecar).
        File.WriteAllText(Path.Combine(_tempDir, "test.pdf"), "pdf");
        File.WriteAllText(Path.Combine(_tempDir, EmbeddedVideoExtractor.WarningsFileName), "warning");

        var result = PowerPointPresentationItemInstance.CollectSlideFilesInOrder(_tempDir);

        CollectionAssert.AreEqual(
            new[] { "Slide.1.png", "Slide.2.mp4", "Slide.9.webm", "Slide.10.png" },
            result.Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public void ReadSyncWarnings_WarningsFilePresent_ReturnsJoinedLines()
    {
        File.WriteAllLines(Path.Combine(_tempDir, EmbeddedVideoExtractor.WarningsFileName),
            new[]
            {
                "Slide 2: video is linked to an external file and can't be imported.",
                "Slide 4: video format '.mpg' isn't supported.",
            });

        var result = PowerPointPresentationItemInstance.ReadSyncWarnings(_tempDir);

        Assert.AreEqual(
            "Slide 2: video is linked to an external file and can't be imported." + Environment.NewLine +
            "Slide 4: video format '.mpg' isn't supported.",
            result);
    }

    [TestMethod]
    public void ReadSyncWarnings_NoWarningsFile_ReturnsNull()
    {
        var result = PowerPointPresentationItemInstance.ReadSyncWarnings(_tempDir);

        Assert.IsNull(result);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter PowerPointPresentationItemInstanceTests
```

Expected: build error — `CollectSlideFilesInOrder` and `ReadSyncWarnings` don't exist yet.

- [ ] **Step 3: Add `LastSyncWarning` property**

In `HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs`, immediately
after the existing `LastSyncDateTime` property (currently lines 38-44):

```csharp
        private DateTime? _lastSyncDateTime = null;

        public DateTime? LastSyncDateTime
        {
            get => _lastSyncDateTime;
            set => this.RaiseAndSetIfChanged(ref _lastSyncDateTime, value);
        }

        private string? _lastSyncWarning;

        public string? LastSyncWarning
        {
            get => _lastSyncWarning;
            set => this.RaiseAndSetIfChanged(ref _lastSyncWarning, value);
        }
```

- [ ] **Step 4: Add the two testable static helpers and rewrite `ApplySlidesFromDirectory`**

Replace the existing `ApplySlidesFromDirectory` method (currently lines 237-257) with:

```csharp
        internal static List<string> CollectSlideFilesInOrder(string targetDirectory)
        {
            var acceptedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
            foreach (var videoExt in Constants.SUPPORTED_VIDEO)
            {
                acceptedExtensions.Add("." + videoExt);
            }

            return Directory.GetFiles(targetDirectory)
                .Where(f => acceptedExtensions.Contains(Path.GetExtension(f)))
                .OrderBy(x => x, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
                .ToList();
        }

        internal static string? ReadSyncWarnings(string targetDirectory)
        {
            var warningsFile = Path.Combine(targetDirectory, EmbeddedVideoExtractor.WarningsFileName);
            return File.Exists(warningsFile)
                ? string.Join(Environment.NewLine, File.ReadAllLines(warningsFile))
                : null;
        }

        private void ApplySlidesFromDirectory(string targetDirectory)
        {
            var newItems = new TrulyObservableCollection<GroupItem>();
            foreach (var filePath in CollectSlideFilesInOrder(targetDirectory))
            {
                newItems.Add(new MediaItem { SourceMediaFilePath = filePath });
            }

            Items = newItems;
            LastSyncWarning = ReadSyncWarnings(targetDirectory);

            Log.Debug("Generating slides");
            GenerateSlides();

            Log.Debug("Import OK");
            LastSyncDateTime = DateTime.Now;
        }
```

`Constants` (namespace `HandsLiftedApp.Core`) and `EmbeddedVideoExtractor` (already imported via the
existing `using HandsLiftedApp.Importer.PowerPointLib;` at the top of this file) are both already
reachable — no new `using` needed.

- [ ] **Step 5: Run tests to verify they pass**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter PowerPointPresentationItemInstanceTests
```

Expected: all 3 tests pass.

- [ ] **Step 6: Build the full Core project to confirm nothing else broke**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug
```

Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs
git add HandsLiftedApp.Tests/Models/RuntimeData/Items/PowerPointPresentationItemInstanceTests.cs
git commit -m "feat: extract testable slide-file collection + sync-warning reading on PowerPointPresentationItemInstance"
```

---

### Task 3: Wire `EmbeddedVideoExtractor` into both sync paths

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs:129-172` (`SyncViaSyncfusion`)
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs:174-235` (`SyncViaNativeInterop`)

**Interfaces:**
- Consumes: `EmbeddedVideoExtractor.ExtractVideos` and `EmbeddedVideoExtractor.WarningsFileName` (Task 1); `ApplySlidesFromDirectory`/`ReadSyncWarnings` (Task 2, unchanged by this task).
- Produces: `PowerPointPresentationItemInstance.ExtractEmbeddedVideos(string targetDirectory)` — private, called from both sync methods.

No automated test for this task: both methods drive real Syncfusion conversion or a real PowerPoint
COM helper process, which is already untested today (no existing test touches `SyncViaSyncfusion` or
`SyncViaNativeInterop`) and can't be exercised without a real license / real Office install. This is
an existing boundary in the codebase, not a regression introduced here. Verified by build + the
manual end-to-end steps in Task 5.

- [ ] **Step 1: Add the `ExtractEmbeddedVideos` helper**

In `PowerPointPresentationItemInstance.cs`, add a new private method directly above `ApplySlidesFromDirectory`:

```csharp
        private void ExtractEmbeddedVideos(string targetDirectory)
        {
            var result = EmbeddedVideoExtractor.ExtractVideos(SourcePresentationFile, targetDirectory);
            var warningsFile = Path.Combine(targetDirectory, EmbeddedVideoExtractor.WarningsFileName);

            if (result.Warnings.Count > 0)
            {
                File.WriteAllLines(warningsFile, result.Warnings);
            }
            else if (File.Exists(warningsFile))
            {
                File.Delete(warningsFile);
            }
        }
```

- [ ] **Step 2: Call it from `SyncViaSyncfusion`, right before `ApplySlidesFromDirectory`**

In the cold-path branch of `SyncViaSyncfusion` (after the `ConvertPDF.Convert(...)` call, before
`ApplySlidesFromDirectory(targetDirectory);`):

```csharp
                    Log.Debug($"Converting PDF to slides: {SourcePresentationFile}");
                    ConvertPDF.Convert(new ImportTask
                    {
                        InputFile = convertedPdfPath,
                        OutputDirectory = targetDirectory,
                        ExportFileFormat = ImportTask.ExportFileFormatType.PDF
                    }, new ImportTaskReporter(stats => { }));

                    ExtractEmbeddedVideos(targetDirectory);

                    ApplySlidesFromDirectory(targetDirectory);
```

- [ ] **Step 3: Call it from `SyncViaNativeInterop`, right before `ApplySlidesFromDirectory`**

Same pattern, in the cold-path branch of `SyncViaNativeInterop` (after its own `ConvertPDF.Convert(...)` call):

```csharp
                    Log.Debug($"Converting PDF to slides: {SourcePresentationFile}");
                    ConvertPDF.Convert(new ImportTask
                    {
                        InputFile = result.OutputFilePath,
                        OutputDirectory = targetDirectory,
                        ExportFileFormat = ImportTask.ExportFileFormatType.PDF
                    }, new ImportTaskReporter(stats => { }));

                    ExtractEmbeddedVideos(targetDirectory);

                    ApplySlidesFromDirectory(targetDirectory);
```

- [ ] **Step 4: Build to confirm no errors**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug
```

Expected: 0 errors.

- [ ] **Step 5: Run the full test suite to confirm no regressions**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj
```

Expected: all tests pass (same pass count as before this task, plus Tasks 1-2's new tests).

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs
git commit -m "feat: extract embedded videos during PowerPoint import (both Syncfusion and native COM paths)"
```

---

### Task 4: Surface sync warnings in the UI

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Items/PowerPointPresentationItemStatusView.axaml`

**Interfaces:**
- Consumes: `PowerPointPresentationItemInstance.LastSyncWarning` (Task 2).
- Produces: a visible warning banner in the existing per-item status view (no code-behind changes).

No automated test — this is a pure XAML/Avalonia visual change with no unit-testable logic (the data
it binds to, `LastSyncWarning`, is already covered by Task 2's tests). Verified by manually running
the app (Step 2 below), per this repo's standing rule that Avalonia UI changes must be clicked
through in a running app before being considered done.

- [ ] **Step 1: Update the view**

Replace the contents of `HandsLiftedApp.Core/Views/Items/PowerPointPresentationItemStatusView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:items="clr-namespace:HandsLiftedApp.Core.Models.RuntimeData.Items"
             mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="150"
             x:DataType="items:PowerPointPresentationItemInstance"
             x:Class="HandsLiftedApp.Core.Views.Items.PowerPointPresentationItemStatusView">
    <StackPanel>
        <Border MinHeight="2" IsVisible="{Binding IsBusy}">
            <StackPanel>
                <ProgressBar IsIndeterminate="True" MinHeight="2" />
                <TextBlock Margin="10 6 10 0">Importing <Run Text="{Binding SourcePresentationFile}" />...</TextBlock>
            </StackPanel>
        </Border>
        <Border IsVisible="{Binding LastSyncWarning, Converter={x:Static ObjectConverters.IsNotNull}}">
            <TextBlock Margin="10 6 10 0"
                       Foreground="#FFA500"
                       TextWrapping="Wrap"
                       Text="{Binding LastSyncWarning}" />
        </Border>
        <!-- <Button Click="Button_OnClick" IsEnabled="{Binding !IsBusy}">Sync</Button> -->
    </StackPanel>
</UserControl>
```

Note the root `UserControl`'s old `IsVisible="{Binding IsBusy}"` binding is removed (it would have
hidden the warning banner too, since the whole control disappeared once `IsBusy` went false) and
moved down onto the busy `Border` alone, which is the only piece that should disappear when the
import finishes.

- [ ] **Step 2: Build and manually verify**

```powershell
dotnet build HandsLiftedApp.Desktop/HandsLiftedApp.Desktop.csproj -c Debug
```

Run `HandsLiftedApp.Desktop`. Add a `.pptx` item to a playlist and Sync it:
- While syncing: progress bar + "Importing..." text shows, no warning banner.
- After a clean sync (no video import issues): neither banner shows.
- After a sync where `LastSyncWarning` is set (can be forced by temporarily setting it in the
  debugger, or by doing the linked-video manual test from Task 5 once that's available): the orange
  warning text appears and persists after `IsBusy` goes false.

- [ ] **Step 3: Commit**

```bash
git add HandsLiftedApp.Core/Views/Items/PowerPointPresentationItemStatusView.axaml
git commit -m "feat: show a warning banner when PowerPoint import couldn't fully import a video"
```

---

### Task 5: Manual end-to-end verification

No code changes — this task confirms Tasks 1-4 work together against real `.pptx` files and (for the
native path) a real PowerPoint installation, which no automated test in this plan can cover.

**Preconditions:** Microsoft PowerPoint installed (for the native-path checks only); a few small
test `.pptx` decks authored for each scenario below.

- [ ] **Step 1: Whole-slide video, Syncfusion path**

With native PowerPoint import disabled (Setup > Integrations checkbox unchecked, the default):
import a deck with one slide whose only content is an embedded video. Confirm: that slide plays the
video with audio in the Live/Projector output, other slides still show as static images, no warning
banner appears.

- [ ] **Step 2: Whole-slide video, native COM path**

Enable native PowerPoint import (requires Microsoft Office installed) and repeat Step 1 with the same
deck. Confirm identical result.

- [ ] **Step 3: Hidden slide before a video slide**

Import a deck with: slide 1 (normal), slide 2 (hidden, via PowerPoint's "Hide Slide"), slide 3 (video).
Confirm the video lands on the slide that's actually shown third-in-file/second-in-show-order — i.e.
verify by scrubbing through the imported item that the video appears at the correct visible position,
not shifted by one from the hidden slide being counted.

- [ ] **Step 4: Externally linked (not embedded) video**

Import a deck with a video inserted via "Insert Video > From a File" *without* PowerPoint's "Link to
File" unchecked (i.e. force a linked, not embedded, video) — or any deck known to reference an
external video path. Confirm: the warning banner appears with wording like "video is linked to an
external file and can't be imported", and that slide shows its static image.

- [ ] **Step 5: Warm-cache re-sync**

Re-sync the same `.pptx` file from Step 1 without changing it. Confirm: the video still plays (warm
cache dir still has the extracted video file from the first run), and check the log — no "Importing
PowerPoint file..." line should reappear (cache-hit path taken, `ExtractEmbeddedVideos` doesn't
re-run).

- [ ] **Step 6: Commit test notes**

```bash
git commit --allow-empty -m "test: manual end-to-end verification for pptx embedded video import (see plan for steps)"
```

---

## Self-Review

**Spec coverage checklist:**

| Spec requirement | Task |
|---|---|
| Extract embedded video, matching PDF-page numbering (hidden slides excluded) | Task 1 |
| Externally-linked video → warning, keep PNG | Task 1 |
| Unsupported extension → warning, keep PNG | Task 1 |
| First video per slide only | Task 1 (`FirstOrDefault()` on `Descendants(A + "videoFile")`) |
| Extend `ApplySlidesFromDirectory`'s extension whitelist to include video | Task 2 |
| Warnings sidecar read into `LastSyncWarning`, cleared when absent | Task 2, Task 3 |
| Call extractor from both `SyncViaSyncfusion` and `SyncViaNativeInterop`, before `ApplySlidesFromDirectory` | Task 3 |
| Cache-hit path skips re-extraction | Task 3 (call site only in the cold-path branch, matching existing `ApplySlidesFromDirectory`-on-cache-hit code already there) |
| UI warning banner, non-blocking | Task 4 |
| Manual verification (both import paths, hidden slides, linked video, warm cache) | Task 5 |
| Unit tests without committed binary fixtures | Task 1, Task 2 |

No gaps found.

**Placeholder scan:** none — every step has complete code or an exact command.

**Type consistency:** `PowerPointVideoExtractionResult.SlideIndexToVideoFile` / `.Warnings` (Task 1)
are used with identical names/types in Task 3's `ExtractEmbeddedVideos`. `EmbeddedVideoExtractor.WarningsFileName`
(Task 1) is used identically in Task 2's `ReadSyncWarnings` and Task 3's `ExtractEmbeddedVideos`.
`CollectSlideFilesInOrder`/`ReadSyncWarnings` (Task 2) signatures match their use in the rewritten
`ApplySlidesFromDirectory` and in Task 2's own tests. `LastSyncWarning` (Task 2) matches its binding
in Task 4's XAML.
