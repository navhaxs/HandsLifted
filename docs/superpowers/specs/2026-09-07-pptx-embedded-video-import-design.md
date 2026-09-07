# PPTX Embedded Video Import — Design

**Status:** Approved by user, ready for implementation planning.
**Date:** 2026-09-07

## Problem

Both existing PowerPoint import paths (`PowerPointPresentationItemInstance.SyncViaSyncfusion` /
`SyncViaNativeInterop`) rasterize every slide to a static PNG:

```
.pptx → PDF (Syncfusion.Presentation, or PowerPoint COM SaveAs) → ConvertPDF (PDFium) → Slide.NN.png
```

A slide containing an embedded video becomes a single frozen frame (or blank, depending on the
exporter) with no playback and no audio. There is no code path anywhere in the importer pipeline
that extracts or plays embedded video media from a `.pptx`.

## Goal

When a slide's main content is an embedded video, import it as a real playable video slide instead
of a static image — using VisionScreens' existing video-slide rendering (`VideoSlideInstance`),
completely unmodified.

## Scope (v1)

- **Whole-slide video only.** If a slide has an embedded video, that slide becomes a video slide.
  Any other shapes/text on that slide are not composited in — same as today, where only a flattened
  static image represents the slide's content. Compositing video over a static background is a much
  larger change (a new layered slide-render type) and is explicitly out of scope.
- **Embedded video only.** A `<a:videoFile>` relationship with `TargetMode="External"` (i.e. the
  video is a link to a file on disk / network, not packaged into the `.pptx`) is not extracted —
  the slide keeps its static PNG, and a warning is recorded.
- **First video per slide.** If a slide somehow has more than one video shape, only the first
  (document order) is used; the rest are ignored (no warning beyond the general "couldn't be fully
  imported" case, since this is expected to be vanishingly rare).
- **No PowerPoint-authored playback settings.** Trim in/out points, start-on-click vs autoplay,
  volume, and loop settings authored in the deck are **not** read. The extracted video plays with
  VisionScreens' own video-slide defaults (same behavior as a user manually adding that video file
  as a media item).
- **Unsupported video codec/extension** (not in `Constants.SUPPORTED_VIDEO`) → keep the static PNG,
  record a warning.
- Applies to the native `.pptx` PowerPoint import only. Google Slides import (which exports PDF
  directly via Google's API, never touching a local `.pptx`) is unaffected and out of scope.

## Architecture

New static class `EmbeddedVideoExtractor` in `HandsLiftedApp.Importer.PowerPointLib`, mirroring the
existing `EmbeddedFontExtractor.cs` (same project) — zip + OOXML relationship parsing, defensive,
never throws.

```csharp
public static class EmbeddedVideoExtractor
{
    public static PowerPointVideoExtractionResult ExtractVideos(string pptxPath, string outputDirectory);
}

public class PowerPointVideoExtractionResult
{
    // 1-based "shown slide" index -> path of the video file written into outputDirectory
    public Dictionary<int, string> SlideIndexToVideoFile { get; init; }
    // Human-readable messages for slides that had a video but couldn't import it
    public List<string> Warnings { get; init; }
}
```

### Slide numbering must match `ConvertPDF`'s page numbering exactly

`ConvertPDF.Convert` writes `Slide.{padded}.png` where the number is the PDF page index (1-based),
and PDF export (both Syncfusion's `PresentationToPdfConverter` with `ShowHiddenSlides = false`, and
PowerPoint's own `SaveAs` PDF) already **excludes hidden slides** from the page count.

`EmbeddedVideoExtractor` must reproduce identical numbering so extracted videos land on the correct
slide:

1. Parse `ppt/presentation.xml` → `<p:sldIdLst>` for the ordered list of slide relationship IDs.
2. Parse `ppt/_rels/presentation.xml.rels` to resolve each `r:id` to its slide part
   (`ppt/slides/slideK.xml`).
3. Open each slide part and check its root `<p:sld>` element's `show` attribute — `show="0"` means
   hidden; skip it (don't assign it a shown-slide number).
4. Number the remaining (shown) slides 1..N in document order — this must equal the same N used for
   `Slide.NN.png`.
5. Zero-pad using the same formula `ConvertPDF` uses: `Math.Floor(Log10(shownCount) + 1)` digits.

### Per-slide video detection

For each shown slide's XML (`ppt/slides/slideK.xml`):

1. Search for `<a:videoFile r:link="rIdX"/>` (DrawingML namespace) — PowerPoint's schema uses
   `r:link` for both embedded and externally-linked video; the distinction is in the relationship's
   `TargetMode`, not the attribute name.
2. Resolve `rIdX` via `ppt/slides/_rels/slideK.xml.rels`.
3. If the relationship's `TargetMode` is `External` → not embedded, skip with warning ("Slide N: video
   is linked to an external file and can't be imported").
4. Otherwise the `Target` resolves (relative to `ppt/slides/`) to `ppt/media/videoY.<ext>`. If
   `<ext>` (lowercased) isn't in `Constants.SUPPORTED_VIDEO` → skip with warning ("Slide N: video
   format '.<ext>' isn't supported").
5. Otherwise: copy the media part's bytes to `outputDirectory/Slide.{padded N}.<ext>`, delete the
   sibling `Slide.{padded N}.png` (already written by the PDF/PNG step), and record
   `SlideIndexToVideoFile[N] = thatPath`.

Any exception while processing a single slide is caught and treated as "skip this slide, no
video" (optionally with a generic warning) — never propagates. A top-level exception (e.g. the
`.pptx` can't be opened as a zip at all, which shouldn't happen since conversion already succeeded
against the same file) results in an empty result with no warnings — video is additive, not core,
so total failure here degrades silently back to the current all-static-images behavior.

### Call site

In `PowerPointPresentationItemInstance`, both `SyncViaSyncfusion` and `SyncViaNativeInterop` already
converge on `ApplySlidesFromDirectory(targetDirectory)` after producing PNGs. Add one call
immediately before it (factored into a small shared private method so it isn't duplicated):

```csharp
private void ExtractEmbeddedVideos(string targetDirectory)
{
    var result = EmbeddedVideoExtractor.ExtractVideos(SourcePresentationFile, targetDirectory);
    if (result.Warnings.Count > 0)
    {
        File.WriteAllLines(Path.Combine(targetDirectory, VideoWarningsFileName), result.Warnings);
    }
}
```

This only runs on a **cold** sync (cache miss). On a cache hit, `ApplySlidesFromDirectory` reads
whatever is already in the (persistent, content-hash-keyed) cache directory from the prior run —
video files and the warnings sidecar included — so behavior is identical without re-running
extraction.

### `ApplySlidesFromDirectory` changes

1. Extend the file-extension whitelist from `{png, jpg, jpeg}` to also include
   `Constants.SUPPORTED_VIDEO`. Since the extractor deletes the PNG it replaces, there is still
   exactly one media file per shown-slide index, so natural-sort filename ordering is unaffected.
2. Read the warnings sidecar file (if present) and set it on a new property (below), joined with
   newlines. If absent, clear the property (so a re-sync of a deck that no longer has video-import
   issues clears a stale warning).

### New property + UI surfacing

`PowerPointPresentationItemInstance` gets:

```csharp
private string? _lastSyncWarning;
public string? LastSyncWarning
{
    get => _lastSyncWarning;
    set => this.RaiseAndSetIfChanged(ref _lastSyncWarning, value);
}
```

`PowerPointPresentationItemStatusView.axaml` (already shows a busy/importing banner bound to
`IsBusy`) gets a second, mutually-exclusive banner:

```xml
<Border IsVisible="{Binding LastSyncWarning, Converter={x:Static ObjectConverters.IsNotNull}}"
        Background="{DynamicResource ... warning-ish brush ...}">
    <TextBlock Margin="10 6 10 0" Text="{Binding LastSyncWarning}" TextWrapping="Wrap" />
</Border>
```

Non-blocking, non-modal — a persistent small notice on the item itself, not an interrupting dialog
(this is a partial degradation, not a failure like the Google Slides import-failure case).

## Data flow summary

```
SourcePresentationFile (.pptx)
  ├─ (existing, unchanged) pptx → PDF → Slide.NN.png
  └─ (new) EmbeddedVideoExtractor.ExtractVideos(pptx, targetDirectory)
       - shown-slide numbering matches PDF page numbering exactly (hidden slides excluded)
       - per slide: <a:videoFile r:link> → relationship → embedded? supported ext? 
         yes: copy to Slide.NN.<ext>, delete sibling Slide.NN.png
         no:  keep Slide.NN.png, record warning
       - warnings (if any) → sidecar file in targetDirectory
  ↓
ApplySlidesFromDirectory(targetDirectory)
  [extension whitelist += Constants.SUPPORTED_VIDEO; reads warnings sidecar → LastSyncWarning]
  ↓
GenerateSlides() → CreateItem.GenerateMediaContentSlide  — UNCHANGED
  (already branches VideoSlideInstance vs ImageSlideInstance purely by file extension)
  ↓
existing render pipeline — UNCHANGED (MpvVideoSlideRenderer plays it like any other video item)
```

## Testing

Unit tests for `EmbeddedVideoExtractor` in `HandsLiftedApp.Tests`, building a minimal valid `.pptx`
package in-memory via `System.IO.Compression.ZipArchive` (presentation.xml + its rels, 2-3 slide
parts + their rels, one with an embedded `<a:videoFile>`, one hidden, one with an
`TargetMode="External"` link) rather than committing binary fixture files — matches this repo's
existing test style (no committed `.pptx` fixtures exist for the font extractor either, which has
no dedicated test file currently).

Cases to cover:
- Single embedded video, supported extension → extracted, correct filename/index, sibling PNG
  removed, no warning.
- Hidden slide before/after a video slide → numbering still lines up with what `ConvertPDF` would
  produce (i.e. hidden slides don't consume a number).
- Externally-linked video (`TargetMode="External"`) → not extracted, warning recorded, PNG kept.
- Unsupported extension → not extracted, warning recorded, PNG kept.
- No video on any slide → empty result, no warnings, no files touched.
- Malformed/corrupt presentation part → doesn't throw, empty-ish result.

Manual verification (COM/native path requires real PowerPoint + a real authored `.pptx`, same
constraint as the existing native-import manual test in
`docs/superpowers/plans/2026-07-15-native-pptx-import.md`):
- Import a deck with one whole-slide video (Syncfusion path, then native COM path) → slide plays
  video with audio in Live/Projector output.
- Import a deck with a video slide + a hidden slide before it → video lands on the correct slide.
- Import a deck with a linked (not embedded) video → warning banner appears, slide shows static
  image.
- Re-sync from a warm cache (no source-file changes) → same result without re-running extraction
  (verify via log line absence / no helper process launch for native path).

## Out of scope / explicit limitations (documented, not fixed here)

- Video composited over other slide content (background + video region).
- PowerPoint-authored trim/volume/autoplay/loop settings.
- Multiple videos on one slide (first wins, silently for the extras).
- Google Slides import (different pipeline, no local `.pptx`).
- Non-Windows video extraction — no restriction actually applies (pure zip/XML, no COM), noted only
  because the *native COM* PDF path is Windows-only; Syncfusion path + video extraction both work
  cross-platform.
