# PPTX External (Hosted) Video Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a PowerPoint slide's video is an `External` reference to an `http(s)` URL (e.g. a
Vimeo embed link from "Insert Video > Online Video"), resolve and download it via `yt-dlp` into the
same local cache used for embedded video, so it plays back with no live network dependency during
the church service.

**Architecture:** `EmbeddedVideoExtractor` (existing, pure/offline/never-throws) gets a minimal
extension: an `External` reference whose `Target` is an `http(s)` URL is collected as a
`PendingExternalVideo` instead of immediately warned about. A new `ExternalVideoDownloader` (in
`HandsLiftedApp.Core.Services`, same tier as the existing `NativePowerPointImportService`) shells out
to `yt-dlp` for each pending video, called from the already-existing `ExtractEmbeddedVideos` method
right after `EmbeddedVideoExtractor.ExtractVideos` returns. No changes anywhere else in the import
pipeline — a successfully downloaded video is handled identically to an embedded one from that point
on.

**Tech Stack:** C# / .NET 10, `System.Diagnostics.Process` (shelling out to `yt-dlp`), MSTest.

**Spec:** [docs/superpowers/specs/2026-09-08-pptx-external-video-import-design.md](../specs/2026-09-08-pptx-external-video-import-design.md)

## Global Constraints

- Download at import time — never stream live during the service. Once downloaded, playback must
  not depend on the network or the hosting service.
- Any URL `yt-dlp` recognizes — no per-site allowlist. Only `http`/`https` `External` targets are
  attempted; anything else (local path, UNC path, non-http(s) scheme) keeps today's "linked to an
  external file" warning unchanged.
- `yt-dlp` is not bundled — resolved from the system PATH. Missing/failing to start is a warning, not
  a blocking error.
- Video quality capped at 1080p, preferring an already-combined stream (avoids needing `ffmpeg` where
  possible): `-f "best[height<=1080]/bestvideo[height<=1080]+bestaudio" --merge-output-format mp4`.
- Fixed 15-minute download timeout, not user-configurable in v1.
- No fine-grained download progress — rides inside the existing indeterminate "Importing..." banner.
- Every failure mode degrades to the existing fail-soft contract: warning appended, static PNG
  stands, nothing else in the import is affected.
- `ExternalVideoDownloader`'s actual subprocess execution is not unit-testable without a real
  `yt-dlp` install and network access — same accepted boundary as `NativePowerPointImportService`'s
  real-Office dependency. What must be unit-tested: the URL classification in
  `EmbeddedVideoExtractor`, and `ExternalVideoDownloader`'s pure helpers (padding, already-downloaded
  check, argument-list construction).
- Test framework is MSTest, matching every existing test in `HandsLiftedApp.Tests`.
- `HandsLiftedApp.Core` does **not** have `ImplicitUsings` enabled (unlike
  `HandsLiftedApp.Importer.PowerPointLib`, which does) — every new file in Core needs explicit
  `using` statements for `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, etc.
- Tooling rule: never shell out to `find`/`grep`; use Glob/Grep tools, scoped to a project subfolder.

---

### Task 1: Classify `External` `http(s)` video targets in `EmbeddedVideoExtractor`

**Files:**
- Modify: `HandsLiftedApp.Importer.PowerPointLib/EmbeddedVideoExtractor.cs`
- Test: `HandsLiftedApp.Tests/Importer/PowerPointLib/EmbeddedVideoExtractorTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `HandsLiftedApp.Importer.PowerPointLib.PendingExternalVideo` — `public record PendingExternalVideo(int SlideNumber, string Url);`
  - `PowerPointVideoExtractionResult.PendingExternalVideos` — `List<PendingExternalVideo> { get; init; } = new();`
  - `PowerPointVideoExtractionResult.ShownSlideCount` — `int { get; set; }` (0 unless extraction got
    far enough to count shown slides). Consumed by Task 2's `ExternalVideoDownloader` to reproduce
    the same zero-pad digit width `EmbeddedVideoExtractor` used for `Slide.{padded}.ext` filenames.

- [ ] **Step 1: Write the failing tests**

Add these three test methods to the end of the existing `EmbeddedVideoExtractorTests` class (inside
the closing `}` of the class, after `ExtractVideos_UnsupportedVideoExtension_KeepsPngAndRecordsWarning`):

```csharp
    [TestMethod]
    public void ExtractVideos_ExternalHttpVideo_AddsToPendingExternalVideosWithoutWarning()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: "https://player.vimeo.com/video/1117314702?app_id=122963", VideoTargetMode: "External"),
        });
        var siblingPng = Path.Combine(_tempDir, "Slide.1.png");
        File.WriteAllText(siblingPng, "png-bytes");

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.Warnings.Count);
        Assert.AreEqual(0, result.SlideIndexToVideoFile.Count);
        Assert.AreEqual(1, result.PendingExternalVideos.Count);
        Assert.AreEqual(1, result.PendingExternalVideos[0].SlideNumber);
        Assert.AreEqual("https://player.vimeo.com/video/1117314702?app_id=122963", result.PendingExternalVideos[0].Url);
        Assert.IsTrue(File.Exists(siblingPng), "PNG must be left alone until the downloader actually succeeds");
    }

    [TestMethod]
    public void ExtractVideos_ExternalNonHttpTarget_StillWarnsAndDoesNotAddToPending()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: "file:///C:/videos/clip.mp4", VideoTargetMode: "External"),
        });

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.PendingExternalVideos.Count);
        Assert.AreEqual(1, result.Warnings.Count);
        StringAssert.Contains(result.Warnings[0], "linked to an external file");
    }

    [TestMethod]
    public void ExtractVideos_ShownSlideCount_MatchesNumberOfShownSlides()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: null, VideoTargetMode: null),
            new TestSlide(Hidden: true, VideoTarget: null, VideoTargetMode: null),
            new TestSlide(Hidden: false, VideoTarget: null, VideoTargetMode: null),
        });

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(2, result.ShownSlideCount);
    }
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter EmbeddedVideoExtractorTests
```

Expected: build error — `PendingExternalVideos` and `ShownSlideCount` don't exist on
`PowerPointVideoExtractionResult` yet.

- [ ] **Step 3: Add `PendingExternalVideo` and extend `PowerPointVideoExtractionResult`**

In `HandsLiftedApp.Importer.PowerPointLib/EmbeddedVideoExtractor.cs`, replace the existing
`PowerPointVideoExtractionResult` class (currently lines 6-13) with:

```csharp
public class PowerPointVideoExtractionResult
{
    /// <summary>1-based "shown slide" index (matches ConvertPDF's page numbering — hidden slides excluded) -> extracted video file path.</summary>
    public Dictionary<int, string> SlideIndexToVideoFile { get; init; } = new();

    /// <summary>Human-readable messages for slides that had a video but couldn't import it.</summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>Slides whose video is an http(s) External reference — not yet resolved. A separate,
    /// slower step (ExternalVideoDownloader) attempts to download these.</summary>
    public List<PendingExternalVideo> PendingExternalVideos { get; init; } = new();

    /// <summary>Count of shown (non-hidden) slides, so a caller can reproduce the same zero-pad digit
    /// width this class used for Slide.{padded}.ext filenames, without needing maxDigits exposed
    /// directly. 0 if extraction returned before any slides were counted.</summary>
    public int ShownSlideCount { get; set; }
}

public record PendingExternalVideo(int SlideNumber, string Url);
```

- [ ] **Step 4: Set `ShownSlideCount` and classify `http(s)` External targets**

In the same file, in `ExtractVideos`, immediately after the existing
`if (shownSlideParts.Count == 0) return result;` line, add one line setting `ShownSlideCount` before
`maxDigits` is computed:

```csharp
            if (shownSlideParts.Count == 0) return result;

            result.ShownSlideCount = shownSlideParts.Count;
            int maxDigits = (int)Math.Floor(Math.Log10(shownSlideParts.Count) + 1);
```

Then, in `ExtractVideoForSlide`, replace the existing `TargetMode == "External"` block:

```csharp
        var targetMode = relationship.Attribute("TargetMode")?.Value;
        if (string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add($"Slide {slideNumber}: video is linked to an external file and can't be imported.");
            return;
        }
```

with:

```csharp
        var targetMode = relationship.Attribute("TargetMode")?.Value;
        if (string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase))
        {
            var externalTarget = relationship.Attribute("Target")?.Value;
            if (Uri.TryCreate(externalTarget, UriKind.Absolute, out var externalUri) &&
                (externalUri.Scheme == Uri.UriSchemeHttp || externalUri.Scheme == Uri.UriSchemeHttps))
            {
                result.PendingExternalVideos.Add(new PendingExternalVideo(slideNumber, externalUri.ToString()));
                return;
            }

            result.Warnings.Add($"Slide {slideNumber}: video is linked to an external file and can't be imported.");
            return;
        }
```

No new `using` needed — `Uri` is in `System`, and this project's `ImplicitUsings` is enabled (same as
the rest of this file).

- [ ] **Step 5: Run tests to verify they pass**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter EmbeddedVideoExtractorTests
```

Expected: all 8 tests pass (the 5 existing plus the 3 new ones).

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Importer.PowerPointLib/EmbeddedVideoExtractor.cs
git add HandsLiftedApp.Tests/Importer/PowerPointLib/EmbeddedVideoExtractorTests.cs
git commit -m "feat: classify http(s) External PowerPoint video targets as pending downloads"
```

---

### Task 2: `ExternalVideoDownloader`

**Files:**
- Create: `HandsLiftedApp.Core/Services/ExternalVideoDownloader.cs`
- Test: `HandsLiftedApp.Tests/Services/ExternalVideoDownloaderTests.cs`

**Interfaces:**
- Consumes: `HandsLiftedApp.Importer.PowerPointLib.PendingExternalVideo` (Task 1). `HandsLiftedApp.Core`
  already references the `HandsLiftedApp.Importer.PowerPointLib` project, so no new project reference
  is needed.
- Produces:
  - `HandsLiftedApp.Core.Services.ExternalVideoDownloader.DownloadPendingVideos(IReadOnlyList<PendingExternalVideo> pendingVideos, int shownSlideCount, string outputDirectory, List<string> warnings)` — `public static void`, never throws (every internal failure appends to `warnings` instead).
  - `ExternalVideoDownloader.PadSlideNumber(int slideNumber, int shownSlideCount) -> string` — `internal static`.
  - `ExternalVideoDownloader.AlreadyDownloaded(string outputDirectory, string paddedNumber) -> bool` — `internal static`.
  - `ExternalVideoDownloader.BuildArguments(string url, string outputPathTemplate) -> IReadOnlyList<string>` — `internal static`.

- [ ] **Step 1: Write the failing tests**

Create `HandsLiftedApp.Tests/Services/ExternalVideoDownloaderTests.cs`:

```csharp
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Services;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class ExternalVideoDownloaderTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ExternalVideoDownloaderTests_" + Guid.NewGuid().ToString("N"));
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
    public void PadSlideNumber_SingleDigitTotal_NoPadding()
    {
        var result = ExternalVideoDownloader.PadSlideNumber(slideNumber: 1, shownSlideCount: 5);

        Assert.AreEqual("1", result);
    }

    [TestMethod]
    public void PadSlideNumber_DoubleDigitTotal_PadsToTwoDigits()
    {
        var result = ExternalVideoDownloader.PadSlideNumber(slideNumber: 3, shownSlideCount: 12);

        Assert.AreEqual("03", result);
    }

    [TestMethod]
    public void AlreadyDownloaded_MatchingVideoFileExists_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Slide.1.mp4"), "video-bytes");

        Assert.IsTrue(ExternalVideoDownloader.AlreadyDownloaded(_tempDir, "1"));
    }

    [TestMethod]
    public void AlreadyDownloaded_OnlyPngExists_ReturnsFalse()
    {
        // The static-image step always runs first, so a PNG existing alone must not be mistaken
        // for an already-downloaded video (that PNG is exactly what still needs replacing).
        File.WriteAllText(Path.Combine(_tempDir, "Slide.1.png"), "png-bytes");

        Assert.IsFalse(ExternalVideoDownloader.AlreadyDownloaded(_tempDir, "1"));
    }

    [TestMethod]
    public void AlreadyDownloaded_NothingExists_ReturnsFalse()
    {
        Assert.IsFalse(ExternalVideoDownloader.AlreadyDownloaded(_tempDir, "1"));
    }

    [TestMethod]
    public void BuildArguments_ReturnsExpectedArgumentList()
    {
        var result = ExternalVideoDownloader.BuildArguments(
            "https://player.vimeo.com/video/1117314702?app_id=122963",
            "/cache/Slide.1.%(ext)s");

        CollectionAssert.AreEqual(
            new[]
            {
                "-f", "best[height<=1080]/bestvideo[height<=1080]+bestaudio",
                "--merge-output-format", "mp4",
                "-o", "/cache/Slide.1.%(ext)s",
                "--no-playlist",
                "--newline",
                "https://player.vimeo.com/video/1117314702?app_id=122963",
            },
            result.ToArray());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter ExternalVideoDownloaderTests
```

Expected: build error — `HandsLiftedApp.Core.Services.ExternalVideoDownloader` doesn't exist yet.

- [ ] **Step 3: Implement `ExternalVideoDownloader`**

Create `HandsLiftedApp.Core/Services/ExternalVideoDownloader.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using HandsLiftedApp.Importer.PowerPointLib;
using Serilog;

namespace HandsLiftedApp.Core.Services;

/// <summary>
/// Resolves and downloads PowerPoint slides' externally-linked (TargetMode="External") video
/// references that point at a hosted video service (e.g. a Vimeo embed URL) via yt-dlp, so they play
/// back exactly like a locally-embedded video with no live network dependency afterward. Requires
/// yt-dlp on the system PATH — not bundled. Never throws: every failure degrades to a warning message
/// and the slide's existing static PNG stands in, matching EmbeddedVideoExtractor's contract.
/// </summary>
public static class ExternalVideoDownloader
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { "mp4", "flv", "mov", "mkv", "avi", "wmv", "webm" };

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    public static void DownloadPendingVideos(
        IReadOnlyList<PendingExternalVideo> pendingVideos,
        int shownSlideCount,
        string outputDirectory,
        List<string> warnings)
    {
        foreach (var pending in pendingVideos)
        {
            DownloadOne(pending, shownSlideCount, outputDirectory, warnings);
        }
    }

    internal static string PadSlideNumber(int slideNumber, int shownSlideCount)
    {
        int maxDigits = (int)Math.Floor(Math.Log10(shownSlideCount) + 1);
        return slideNumber.ToString(new string('0', maxDigits));
    }

    internal static bool AlreadyDownloaded(string outputDirectory, string paddedNumber)
    {
        var prefix = $"Slide.{paddedNumber}.";
        return Directory.GetFiles(outputDirectory).Any(f =>
            Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            SupportedExtensions.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant()));
    }

    internal static IReadOnlyList<string> BuildArguments(string url, string outputPathTemplate) => new[]
    {
        "-f", "best[height<=1080]/bestvideo[height<=1080]+bestaudio",
        "--merge-output-format", "mp4",
        "-o", outputPathTemplate,
        "--no-playlist",
        "--newline",
        url,
    };

    private static void DownloadOne(PendingExternalVideo pending, int shownSlideCount, string outputDirectory, List<string> warnings)
    {
        var paddedNumber = PadSlideNumber(pending.SlideNumber, shownSlideCount);

        if (AlreadyDownloaded(outputDirectory, paddedNumber))
        {
            Log.Debug("Slide {SlideNumber}: external video already downloaded, skipping", pending.SlideNumber);
            return;
        }

        var outputTemplate = Path.Combine(outputDirectory, $"Slide.{paddedNumber}.%(ext)s");
        var startInfo = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var arg in BuildArguments(pending.Url, outputTemplate))
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception)
        {
            process = null;
        }

        if (process == null)
        {
            warnings.Add($"Slide {pending.SlideNumber}: needs yt-dlp to import this video — install it (see https://github.com/yt-dlp/yt-dlp#installation) and re-sync.");
            return;
        }

        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) stderr.AppendLine(e.Data); };
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        bool exited = process.WaitForExit((int)DownloadTimeout.TotalMilliseconds);
        if (!exited)
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception) { }
            warnings.Add($"Slide {pending.SlideNumber}: video download timed out after {DownloadTimeout.TotalMinutes:0} minutes.");
            return;
        }

        if (process.ExitCode != 0)
        {
            var detail = LastNonEmptyLine(stderr.ToString());
            warnings.Add(detail != null
                ? $"Slide {pending.SlideNumber}: video download failed — {detail}"
                : $"Slide {pending.SlideNumber}: video download failed.");
            return;
        }

        var downloadedPath = FindNonImageFileWithPrefix(outputDirectory, paddedNumber);
        if (downloadedPath == null)
        {
            warnings.Add($"Slide {pending.SlideNumber}: video download reported success but no output file was found.");
            return;
        }

        var ext = Path.GetExtension(downloadedPath).TrimStart('.').ToLowerInvariant();
        if (!SupportedExtensions.Contains(ext))
        {
            warnings.Add($"Slide {pending.SlideNumber}: downloaded video format '.{ext}' isn't supported.");
            File.Delete(downloadedPath);
            return;
        }

        var siblingPng = Path.Combine(outputDirectory, $"Slide.{paddedNumber}.png");
        if (File.Exists(siblingPng)) File.Delete(siblingPng);

        Log.Debug("Slide {SlideNumber}: downloaded external video to {Path}", pending.SlideNumber, downloadedPath);
    }

    // Excludes only image extensions (rather than requiring a supported video extension) so that a
    // download producing an unexpected-but-real format still gets found and reported with the
    // specific "unsupported format" warning, instead of the vaguer "no output file was found" one.
    private static string? FindNonImageFileWithPrefix(string outputDirectory, string paddedNumber)
    {
        var prefix = $"Slide.{paddedNumber}.";
        return Directory.GetFiles(outputDirectory)
            .FirstOrDefault(f =>
                Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                !ImageExtensions.Contains(Path.GetExtension(f)));
    }

    private static string? LastNonEmptyLine(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[^1] : null;
    }
}
```

Note: `HandsLiftedApp.Core` does not have `ImplicitUsings` enabled, so every `using` above is
required — don't drop any of them assuming they're implicit.

- [ ] **Step 4: Run tests to verify they pass**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter ExternalVideoDownloaderTests
```

Expected: all 6 tests pass.

- [ ] **Step 5: Build the full Core project to confirm nothing else broke**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug
```

Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Services/ExternalVideoDownloader.cs
git add HandsLiftedApp.Tests/Services/ExternalVideoDownloaderTests.cs
git commit -m "feat: add ExternalVideoDownloader to fetch hosted-video PowerPoint references via yt-dlp"
```

---

### Task 3: Wire `ExternalVideoDownloader` into `ExtractEmbeddedVideos`

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs:274-299` (`ExtractEmbeddedVideos`)

**Interfaces:**
- Consumes: `ExternalVideoDownloader.DownloadPendingVideos` (Task 2); `PowerPointVideoExtractionResult.PendingExternalVideos`/`.ShownSlideCount` (Task 1).
- Produces: nothing new — this task only adds one call inside an existing method.

No automated test for this task, for the same reason Task 3 of the embedded-video plan had none:
`ExtractEmbeddedVideos` is exercised through `SyncViaSyncfusion`/`SyncViaNativeInterop`, which drive
real conversion tools and are not unit-tested anywhere in this codebase. Verified by build + the
manual end-to-end steps in Task 4.

- [ ] **Step 1: Add the `DownloadPendingVideos` call**

Replace the existing `ExtractEmbeddedVideos` method (currently lines 274-299) with:

```csharp
        private void ExtractEmbeddedVideos(string targetDirectory)
        {
            try
            {
                var result = EmbeddedVideoExtractor.ExtractVideos(SourcePresentationFile, targetDirectory);

                ExternalVideoDownloader.DownloadPendingVideos(
                    result.PendingExternalVideos, result.ShownSlideCount, targetDirectory, result.Warnings);

                var warningsFile = Path.Combine(targetDirectory, EmbeddedVideoExtractor.WarningsFileName);

                if (result.Warnings.Count > 0)
                {
                    File.WriteAllLines(warningsFile, result.Warnings);
                }
                else if (File.Exists(warningsFile))
                {
                    File.Delete(warningsFile);
                }

                Log.Debug("Embedded video extraction for {SourceFile}: {VideoCount} video(s), {WarningCount} warning(s)",
                    SourcePresentationFile, result.SlideIndexToVideoFile.Count, result.Warnings.Count);
            }
            catch (Exception e)
            {
                Log.Warning(e,
                    "Embedded video extraction failed for {SourceFile}; slides will keep their static images",
                    SourcePresentationFile);
            }
        }
```

`ExternalVideoDownloader` lives in `HandsLiftedApp.Core.Services`, already imported via the existing
`using HandsLiftedApp.Core.Services;` at the top of this file (line 1) — no new `using` needed.

- [ ] **Step 2: Build to confirm no errors**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug
```

Expected: 0 errors.

- [ ] **Step 3: Run the full test suite to confirm no regressions**

```powershell
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj
```

Expected: all tests pass (same pass count as before this task, plus Tasks 1-2's new tests).

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs
git commit -m "feat: download hosted-video PowerPoint references during import"
```

---

### Task 4: Manual end-to-end verification

No code changes — this task confirms Tasks 1-3 work against a real deck with a real hosted-video
reference, which no automated test in this plan can cover (no real network, no real `yt-dlp`
installation, no real GUI in most execution environments for this plan).

**Preconditions:** A `.pptx` deck with a slide whose video was inserted via "Insert Video > Online
Video" (or otherwise produces an `External` relationship with an `http(s)` `Target`, e.g. the Vimeo
player URL that motivated this feature). `yt-dlp` available to install/uninstall on the test machine
for the two contrasting checks below.

- [ ] **Step 1: With `yt-dlp` installed**

Import the deck. Confirm: the slide plays the downloaded video with audio in the Live/Projector
output, no warning banner for that slide, and the cache directory
(`%LocalAppData%\VisionScreens\ImportCache\<hash>`) contains a `Slide.NN.mp4` (or whatever format was
downloaded) file for it.

- [ ] **Step 2: Without `yt-dlp` installed**

Temporarily rename/remove `yt-dlp` from PATH (or test on a machine without it). Re-sync the same
deck's cache-hit directory removed, or a fresh copy of the file. Confirm: the warning banner shows
"needs yt-dlp to import this video — install it..." and the slide shows its static PNG.

- [ ] **Step 3: Re-sync after a successful download**

With `yt-dlp` installed, sync the deck once (Step 1), confirm success, then sync again without
changing the file. Confirm: no second download happens (check logs for the absence of a new
`Log.Debug("downloaded external video...")` line, and/or that the sync completes near-instantly
compared to the first run) — `AlreadyDownloaded` should short-circuit it.

- [ ] **Step 4: Unresolvable/failing video**

Use a deck referencing a private, deleted, or otherwise `yt-dlp`-unresolvable video URL. Confirm: the
warning banner shows a "video download failed — <detail>" message derived from `yt-dlp`'s own error
output, and the slide shows its static PNG.

- [ ] **Step 5: Commit test notes**

```bash
git commit --allow-empty -m "test: manual end-to-end verification for pptx external video import (see plan for steps)"
```

---

## Self-Review

**Spec coverage checklist:**

| Spec requirement | Task |
|---|---|
| Classify `External` `http(s)` targets as pending, not immediate warning | Task 1 |
| Other `External` targets keep today's warning unchanged | Task 1 |
| `ShownSlideCount` exposed so padding stays in sync with `ConvertPDF`/embedded numbering | Task 1 |
| `yt-dlp` resolved from PATH, not bundled; missing → warning | Task 2 |
| 1080p cap, prefer combined stream, `--merge-output-format mp4` | Task 2 (`BuildArguments`) |
| 15-minute timeout, kill on timeout | Task 2 |
| Skip if already downloaded (idempotent across cache-hit re-syncs) | Task 2 (`AlreadyDownloaded`) |
| Unsupported downloaded format → warn + delete | Task 2 |
| Sibling PNG deleted only on success | Task 2 |
| Called from `ExtractEmbeddedVideos`, so both Sync paths and both cache branches get it automatically | Task 3 |
| No progress bar — rides the existing "Importing..." banner | No code needed — already true, nothing to add |
| Manual verification (installed / not installed / re-sync / failure) | Task 4 |
| Unit tests without real subprocess/network | Task 1, Task 2 |

No gaps found.

**Placeholder scan:** none — every step has complete code or an exact command.

**Type consistency:** `PendingExternalVideo(int SlideNumber, string Url)` (Task 1) matches its use in
Task 2's `DownloadPendingVideos`/`DownloadOne` parameters and Task 3's call site.
`PowerPointVideoExtractionResult.ShownSlideCount`/`.PendingExternalVideos` (Task 1) match their use in
Task 3's `ExtractEmbeddedVideos`. `ExternalVideoDownloader.DownloadPendingVideos`,
`.PadSlideNumber`, `.AlreadyDownloaded`, `.BuildArguments` (Task 2) match their use in Task 2's own
tests and Task 3's call site.
