# Song Title Slide — Video Thumbnail Background

**Date**: 2026-06-14  
**Status**: Approved

## Problem

When a Song Item has a motion background set, the `SongTitleSlideInstance` thumbnail (the first slide thumbnail shown in MainView's centre grid) renders with a transparent/black background. The video is not reflected in the thumbnail at all.

## Goal

Render a frame from the motion background video as the background of the song title slide thumbnail only. All other song slide thumbnails remain unchanged (transparent background over live video, as today).

## Scope

- **In scope**: `SongTitleSlideInstance` thumbnail only (slide index 0 — the title/copyright slide)
- **Out of scope**: `SongSlideInstance` thumbnails (verses, choruses, etc.)
- **Out of scope**: The live/projector rendering pipeline (unchanged)

## Approach: Win32 Shell Thumbnail

Use the existing `WindowsThumbnailProvider.GetThumbnail()` (already in the codebase, used by the media explorer / library view). Windows Shell selects a representative frame automatically (~1/3 into the video). Platform-guarded with `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`; falls back to `TransparentBackground` on non-Windows.

## Data Flow

```
SongTitleSlideInstance.GenerateBitmaps()
  │
  ├─ HasMotionBackground && IsOSPlatform(Windows)?
  │     → WindowsThumbnailProvider.GetThumbnail(videoPath, 1920, 1080, ThumbnailOptions.None)
  │     → Avalonia.Bitmap → SKBitmap  (via PNG MemoryStream, new BitmapUtils helper)
  │
  ├─ SongTitleSlideSpecBuilder.Build(this, videoFrame?)
  │     videoFrame != null  → SkiaBitmapBackground(videoFrame)   [already handled by SlideRenderer]
  │     videoFrame == null  → TransparentBackground               [existing fallback]
  │
  └─ SlideRenderer.RenderToSKBitmap(spec)
       videoFrame?.Dispose()
```

`RegenerateAllSlideBitmaps()` in `SongItemInstance` follows the same pattern for the title slide path.

## File Changes

### 1. `BitmapUtils.cs` (new helper)

Add `AvaloniaToSKBitmap(Bitmap avaBitmap) → SKBitmap`:
- Encode `Avalonia.Media.Imaging.Bitmap` to PNG `MemoryStream`
- `SKBitmap.Decode(stream)`

### 2. `SongTitleSlideSpecBuilder.cs`

```csharp
// Before
public static SlideRenderSpec Build(SongTitleSlideInstance slide)

// After  
public static SlideRenderSpec Build(SongTitleSlideInstance slide, SKBitmap? videoFrame = null)
```

`BuildBackground()` gains the same optional param. Priority:
1. `videoFrame != null` → `SkiaBitmapBackground(videoFrame)`
2. `slide.HasMotionBackground` → `TransparentBackground` (no frame available)
3. theme image → `ImageBackground`
4. theme colour → `SolidBackground`

### 3. `SongTitleSlideInstance.cs`

`GenerateBitmaps()` gains video frame extraction:

```csharp
private void GenerateBitmaps()
{
    SKBitmap? videoFrame = null;
    if (HasMotionBackground && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var videoPath = ParentSongItem?.MotionBackgroundVideoPath;
        if (!string.IsNullOrWhiteSpace(videoPath))
        {
            var avaBmp = WindowsThumbnailProvider.GetThumbnail(
                videoPath, 1920, 1080, ThumbnailOptions.None);
            if (avaBmp != null)
                videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
        }
    }

    var spec = SongTitleSlideSpecBuilder.Build(this, videoFrame);
    using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
    videoFrame?.Dispose();
    Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
    Thumbnail = BitmapUtils.CreateThumbnail(Cached);
}
```

### 4. `SongItemInstance.cs`

`RegenerateAllSlideBitmaps()` — add the same extraction block before building the title slide spec:

```csharp
// Also regenerate title slide bitmap
if (titleSlide is SongTitleSlideInstance titleInstance)
{
    SKBitmap? videoFrame = null;
    if (HasMotionBackground && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var avaBmp = WindowsThumbnailProvider.GetThumbnail(
            MotionBackgroundVideoPath, 1920, 1080, ThumbnailOptions.None);
        if (avaBmp != null)
            videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
    }
    var spec = SongTitleSlideSpecBuilder.Build(titleInstance, videoFrame);
    using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
    videoFrame?.Dispose();
    titleInstance.Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
    titleInstance.Thumbnail = BitmapUtils.CreateThumbnail(titleInstance.Cached);
}
```

## Threading

No async changes required. `GetThumbnail` is synchronous. `GenerateBitmaps()` already runs on main thread (via `ObserveOn(RxApp.MainThreadScheduler)` + debounce). Windows Shell caches thumbnails, so repeated calls are fast (<10ms after first hit).

## Error Handling

- `GetThumbnail` throws `FileNotFoundException` if path missing — guard with `File.Exists` or catch. Existing code already guards `HasMotionBackground` behind `IsValidVideoFile`.
- `GetThumbnail` throws `COMException` for some formats — wrap in try/catch, log, fall back to `null` (→ `TransparentBackground`).
- `SKBitmap.Decode` returns `null` on failure — null-check per CLAUDE.md pattern.

## Non-Goals

- Cross-platform frame extraction (libmpv) — defer; isolate behind platform guard now
- Caching the extracted `SKBitmap` across regenerations — Shell API caches internally; not needed
- Changing live/projector rendering behaviour
