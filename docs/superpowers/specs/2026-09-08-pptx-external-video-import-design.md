# PPTX External (Hosted) Video Import — Design

**Status:** Approved by user, ready for implementation planning.
**Date:** 2026-09-08

## Problem

The just-shipped embedded-video import (`docs/superpowers/specs/2026-09-07-pptx-embedded-video-import-design.md`)
only handles video that's physically packaged inside the `.pptx` (`TargetMode` absent/`Internal`).
When a slide's video is inserted via PowerPoint's "Insert Video > Online Video" (or similar), the
relationship's `TargetMode` is `External` and `Target` is a URL into a hosted video service — e.g.
`https://player.vimeo.com/video/1117314702?app_id=122963`. Today that's treated identically to a
linked local file: the slide keeps its static PNG, and a warning says the video "is linked to an
external file and can't be imported."

A `player.vimeo.com` embed URL is an HTML iframe player page, not a direct media file — mpv (this
app's video engine) can't play it as-is. Resolving it to an actual video file requires a tool built
for exactly this (`yt-dlp`), which the app doesn't currently invoke, bundle, or configure for.

## Goal

When an `External` video reference's `Target` is an `http(s)` URL, resolve and download the actual
video via `yt-dlp` into the same local cache used for embedded video, so it plays exactly like any
other video slide — with no live network dependency during the church service itself.

## Decisions (from brainstorming)

- **Download at import time, not live streaming.** Once imported, playback must not depend on the
  venue's internet or the hosting service being reachable during a live service. This is the same
  reliability posture the embedded-video feature already has.
- **Any URL `yt-dlp` recognizes** — no per-site allowlist/regex. If `yt-dlp` can extract it, VisionScreens
  will try. If it can't, the failure surfaces as the existing warning-banner mechanism.
- **`yt-dlp` is not bundled.** The app looks for it on the system PATH. If missing, the slide gets the
  same fail-soft warning treatment as any other unresolvable video, with an instruction to install it.
- **Video quality capped at 1080p.** Format selector prefers a single combined stream up to 1080p,
  falling back to separate best-video(≤1080p)+best-audio only if no combined stream exists.
- **No fine-grained download progress bar in v1.** The download rides inside the existing
  indeterminate "Importing..." banner (`IsBusy`), same as PDF conversion does today.
- **Fixed 15-minute timeout**, not user-configurable in v1 (YAGNI — sermon-length video is long but
  not unbounded, and there's no existing per-task timeout configuration UI to extend).

## Architecture

### `EmbeddedVideoExtractor` (existing, `HandsLiftedApp.Importer.PowerPointLib`) — minimal extension

Its "never throws, fast, offline, pure zip/XML" character is preserved. The only change: when a
video relationship's `TargetMode` is `External`, check whether `Target` is an `http` or `https` URL
(`Uri.TryCreate(target, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https")`):

- **Yes** → don't warn yet. Record it as a `PendingExternalVideo(SlideNumber, Url)` on the result,
  for the (separate, slower) downloader to attempt.
- **No** (a local path, a UNC path, a non-http(s) scheme, or an unparseable target) → unchanged:
  warn "video is linked to an external file and can't be imported", static PNG stands.

`PowerPointVideoExtractionResult` gains:

```csharp
public List<PendingExternalVideo> PendingExternalVideos { get; init; } = new();
public int ShownSlideCount { get; init; } // count of shown slides, so callers can independently
                                           // compute the same zero-pad digit width this class used
```

```csharp
public record PendingExternalVideo(int SlideNumber, string Url);
```

`ShownSlideCount` exists so a separate consumer (`ExternalVideoDownloader`, below) can reproduce the
identical `(int)Math.Floor(Math.Log10(count) + 1)` zero-pad formula without either duplicating
`EmbeddedVideoExtractor`'s internal `maxDigits` plumbing or exposing it as a raw parameter. This is
the same intentional small duplication pattern the existing spec already established between
`EmbeddedVideoExtractor` and `ConvertPDF` — documented, not accidental.

### `ExternalVideoDownloader` (new, `HandsLiftedApp.Core.Services`)

Sits at the same tier as `NativePowerPointImportService` — the app's only other "shell out to an
external tool" logic — since it needs `Process`, network I/O, and can be slow. Called from
`PowerPointPresentationItemInstance.ExtractEmbeddedVideos`, right after
`EmbeddedVideoExtractor.ExtractVideos` returns, for each `PendingExternalVideo`:

```csharp
public static class ExternalVideoDownloader
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

    public static void DownloadPendingVideos(
        IReadOnlyList<PendingExternalVideo> pendingVideos,
        int shownSlideCount,
        string outputDirectory,
        List<string> warnings)
}
```

For each pending video, in order:

1. **Compute the padded slide number** the same way `EmbeddedVideoExtractor` does
   (`(int)Math.Floor(Math.Log10(shownSlideCount) + 1)` digits).
2. **Skip if already downloaded.** If `Slide.{padded}.<ext>` already exists for any
   `ext` in `HandsLiftedApp.Core.Constants.SUPPORTED_VIDEO`, do nothing — this is what stops a
   video re-downloading from Vimeo on every playlist load (the cache-hit path calls
   `ExtractEmbeddedVideos` too, per the fix already shipped in the embedded-video feature).
3. **Resolve `yt-dlp`.** Attempt to start it (`FileName = "yt-dlp"`, PATH-resolved). If starting the
   process throws (not found), warn `"Slide {N}: needs yt-dlp to import this video — install it
   (see https://github.com/yt-dlp/yt-dlp#installation) and re-sync."` and move to the next pending
   video. `Constants.SUPPORTED_VIDEO` is directly reachable here (unlike in `PowerPointLib`, `Core`
   can reference its own `Constants` with no dependency-direction problem).
4. **Run the download**, with `ArgumentList`:
   ```
   -f "best[height<=1080]/bestvideo[height<=1080]+bestaudio"
   --merge-output-format mp4
   -o "{outputDirectory}/Slide.{padded}.%(ext)s"
   --no-playlist
   --newline
   {url}
   ```
   `WaitForExit(DownloadTimeout)`. If it doesn't exit in time: `Kill(entireProcessTree: true)`, warn
   `"Slide {N}: video download timed out after 15 minutes."`.
5. **Validate.** If the process exited non-zero, warn with a short excerpt of its captured stderr
   (last non-empty line, truncated to a reasonable length so the banner stays readable) — this is
   also where a missing-`ffmpeg` failure (needed when `yt-dlp` must merge separate video/audio
   streams) surfaces, verbatim from `yt-dlp`'s own error text. If it exited 0, look for the produced
   `Slide.{padded}.*` file; if its extension isn't in `Constants.SUPPORTED_VIDEO`, warn "downloaded
   but format isn't supported" and delete the file. Otherwise: delete the sibling PNG if present —
   from here it's handled identically to a locally-embedded video by the rest of the pipeline.

### Call site

`PowerPointPresentationItemInstance.ExtractEmbeddedVideos` (existing, already wrapped in try/catch
per the last fix wave) gains one more step after building the warnings list from
`EmbeddedVideoExtractor`'s result:

```csharp
private void ExtractEmbeddedVideos(string targetDirectory)
{
    try
    {
        var result = EmbeddedVideoExtractor.ExtractVideos(SourcePresentationFile, targetDirectory);

        ExternalVideoDownloader.DownloadPendingVideos(
            result.PendingExternalVideos, result.ShownSlideCount, targetDirectory, result.Warnings);

        var warningsFile = ...; // unchanged from here — result.Warnings now also carries any
                                 // download failures, so the existing sidecar-write logic needs no
                                 // further changes.
        ...
    }
    ...
}
```

No changes needed anywhere else in `SyncViaSyncfusion`/`SyncViaNativeInterop` — this is entirely
inside the already-existing `ExtractEmbeddedVideos` method, so it automatically runs on both import
paths and both cold-cache and warm-cache-hit branches, exactly like embedded video does today.

## Data flow

```
EmbeddedVideoExtractor.ExtractVideos(pptx, dir)
  - embedded (Internal) video -> extracted as today
  - External, http(s) Target  -> PendingExternalVideos.Add(slideNumber, url)   [NEW]
  - External, other Target    -> Warnings.Add(...) unchanged
  ↓
ExternalVideoDownloader.DownloadPendingVideos(pending, shownSlideCount, dir, warnings)   [NEW]
  for each pending video:
    already downloaded?        -> skip
    yt-dlp missing?             -> warning, skip
    yt-dlp exits non-zero/timeout -> warning (incl. stderr excerpt / timeout), skip
    yt-dlp produces unsupported ext -> warning, delete file, skip
    success                     -> delete sibling PNG (same as embedded case)
  ↓
(rest of pipeline unchanged: ApplySlidesFromDirectory / CollectSlideFilesInOrder /
 GenerateSlides / CreateItem.GenerateMediaContentSlide -> VideoSlideInstance)
```

## Error handling

Every failure mode here degrades to the existing fail-soft contract: slide keeps its static PNG, a
warning is appended, nothing else in the import is affected. `ExternalVideoDownloader` itself doesn't
need its own outer try/catch at the `DownloadPendingVideos` level beyond what's natural per-video,
since it already runs inside `ExtractEmbeddedVideos`'s existing try/catch (added in the prior fix
wave) — an unexpected exception anywhere in the download logic is caught there, logged, and the
import still completes with whatever slides succeeded.

## Testing

`ExternalVideoDownloader`'s actual subprocess execution is not unit-testable without a real `yt-dlp`
installation and real network access — the same boundary `NativePowerPointImportService` already has
with real PowerPoint/Office, an accepted pattern in this codebase. What IS unit-testable, and should
be:

- The URL classification in `EmbeddedVideoExtractor` (an `http(s)` `External` target becomes a
  `PendingExternalVideo`, not an immediate warning; other `External` targets are unaffected) —
  extends the existing `EmbeddedVideoExtractorTests.cs` fixture-building approach.
- `ExternalVideoDownloader`'s "already downloaded" check, as a small testable static method taking
  a directory and a padded slide number — plain filesystem test, no subprocess involved.
- The `yt-dlp` argument list construction, as a small testable pure static method
  (`BuildArguments(url, outputPathTemplate) -> IReadOnlyList<string>`) — asserts the exact argument
  list without running any process.
- The zero-pad formula parity between `EmbeddedVideoExtractor.ShownSlideCount` usage and
  `ExternalVideoDownloader`'s own padding — a test constructing a known `shownSlideCount` and
  asserting the expected padded filename.

Manual end-to-end verification (added to this feature's own checklist, alongside the existing
embedded-video manual tests): a real deck with a Vimeo-embedded video, with `yt-dlp` installed and
without it, confirming the warning text and the successful-download-and-playback path respectively.

## Out of scope / explicit limitations

- No bundled `yt-dlp` — installation is the user's responsibility (v1 decision; revisit if this
  becomes a support burden).
- No `ffmpeg` bundling either — if a specific site requires merging separate video/audio and
  `ffmpeg` isn't present, that surfaces as `yt-dlp`'s own error text in the warning, not a
  VisionScreens-specific message.
- No download-progress percentage in the UI — indeterminate "Importing..." only.
- No user-configurable quality cap or timeout — fixed at 1080p / 15 minutes.
- No legal/ToS vetting of downloading from a given hosting service or specific video's privacy
  settings — that judgment call belongs to the church using the feature, same as it already does for
  any video they choose to embed or link.
- Everything already out of scope for the embedded-video feature (video-over-static-content
  compositing, PowerPoint-authored trim/volume/autoplay, multiple videos per slide) remains out of
  scope here too.
