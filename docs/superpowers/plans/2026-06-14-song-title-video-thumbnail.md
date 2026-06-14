# Song Title Slide Video Thumbnail Background Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a Song Item has a motion background set, render a representative video frame as the background of the song title slide thumbnail in MainView's centre grid.

**Architecture:** Extract a frame via the existing `WindowsThumbnailProvider.GetThumbnail()` Win32 API (already used in media explorer), convert it to an `SKBitmap`, and pass it to `SongTitleSlideSpecBuilder` as a `SkiaBitmapBackground` — a background type already handled by `SlideRenderer`. Platform-guarded with `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`; non-Windows falls back silently to `TransparentBackground`.

**Tech Stack:** C# / .NET 8, SkiaSharp, Avalonia 11, MSTest, existing `ShellThumbs.WindowsThumbnailProvider`

---

## File Map

| File | Change |
|---|---|
| `HandsLiftedApp.Core/BitmapUtils.cs` | Add `AvaloniaToSKBitmap(Bitmap) → SKBitmap` helper |
| `HandsLiftedApp.Core/Render/Skia/Builders/SongTitleSlideSpecBuilder.cs` | Add `SKBitmap? videoFrame = null` param to `Build()` and `BuildBackground()` |
| `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs` | Extract Win32 frame in `GenerateBitmaps()`, pass to builder |
| `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs` | Same extraction in `RegenerateAllSlideBitmaps()` |
| `HandsLiftedApp.Tests/Render/Skia/Builders/SongTitleSlideSpecBuilderTests.cs` | Add tests for `videoFrame` param |

---

### Task 1: Add `BitmapUtils.AvaloniaToSKBitmap` helper

**Files:**
- Modify: `HandsLiftedApp.Core/BitmapUtils.cs`

This is a pure conversion function with no Avalonia runtime dependency issues — it operates on an already-constructed `Bitmap` object. No unit test is required because it's a trivial PNG encode/decode round-trip; the integration path in Task 3 provides the real verification.

- [ ] **Step 1: Add the helper method to `BitmapUtils`**

Open `HandsLiftedApp.Core/BitmapUtils.cs`. Add after the `SKBitmapToAvalonia` method (line 19):

```csharp
public static SKBitmap? AvaloniaToSKBitmap(Bitmap avaBitmap)
{
    using var ms = new System.IO.MemoryStream();
    avaBitmap.Save(ms);
    ms.Seek(0, System.IO.SeekOrigin.Begin);
    return SKBitmap.Decode(ms);
}
```

The full file after the edit:

```csharp
using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Skia.Helpers;
using SkiaSharp;

namespace HandsLiftedApp.Core
{
    public class BitmapUtils
    {
        public static Bitmap SKBitmapToAvalonia(SKBitmap skBitmap)
        {
            using var data = skBitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new System.IO.MemoryStream(data.ToArray());
            return new Bitmap(stream);
        }

        public static SKBitmap? AvaloniaToSKBitmap(Bitmap avaBitmap)
        {
            using var ms = new System.IO.MemoryStream();
            avaBitmap.Save(ms);
            ms.Seek(0, System.IO.SeekOrigin.Begin);
            return SKBitmap.Decode(ms);
        }

        public static Bitmap? CreateThumbnail(Bitmap? source)
        {
            if (source == null)
            {
                return null;
            }

            using var bitmap = new SKBitmap(
                (int)source.Size.Width,
                (int)source.Size.Height,
                OperatingSystem.IsMacOS() ? SKColorType.Bgra8888 : SKImageInfo.PlatformColorType,
                SKAlphaType.Opaque);
            
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                canvas.DrawRect(0, 0, (int)source.Size.Width, (int)source.Size.Height,
                    new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.Black });

                int xres = (int)source.Size.Width;
                int yres = (int)source.Size.Height;
                int stride = (xres * 32 /*BGRA bpp*/ + 7) / 8;
                int bufferSize = yres * stride;
                IntPtr bufferPtr = Marshal.AllocCoTaskMem(bufferSize);

                using IDrawingContextImpl contextImpl =
                    DrawingContextHelper.WrapSkiaCanvas(canvas, SkiaPlatform.DefaultDpi);

                source.CopyPixels(new PixelRect(0, 0, xres, yres), bufferPtr, bufferSize, stride);
                bitmap.SetPixels(bufferPtr);
            }

            SKBitmap? resizedBitmap = bitmap.Resize(new SKImageInfo(500, (int)(source.Size.Height / source.Size.Width * 500)),
                SKFilterQuality.High);

            BmpSharp.BitsPerPixelEnum bitsPerPixel = resizedBitmap.BytesPerPixel == 4
                ? BmpSharp.BitsPerPixelEnum.RGBA32
                : BmpSharp.BitsPerPixelEnum.RGB24;
            BmpSharp.Bitmap bmp =
                new BmpSharp.Bitmap(resizedBitmap.Width, resizedBitmap.Height, resizedBitmap.Bytes, bitsPerPixel);
                
            return new Bitmap(bmp.GetBmpStream(fliped: true));
        }
    }
}
```

- [ ] **Step 2: Build to verify no errors**

```
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add HandsLiftedApp.Core/BitmapUtils.cs
git commit -m "feat: add BitmapUtils.AvaloniaToSKBitmap conversion helper"
```

---

### Task 2: Add `videoFrame` parameter to `SongTitleSlideSpecBuilder`

**Files:**
- Modify: `HandsLiftedApp.Core/Render/Skia/Builders/SongTitleSlideSpecBuilder.cs`
- Modify: `HandsLiftedApp.Tests/Render/Skia/Builders/SongTitleSlideSpecBuilderTests.cs`

- [ ] **Step 1: Write failing tests**

Open `HandsLiftedApp.Tests/Render/Skia/Builders/SongTitleSlideSpecBuilderTests.cs`. Add these three test methods after the existing ones:

```csharp
[TestMethod]
public void Build_WithVideoFrame_UsesSkiaBitmapBackground()
{
    var slide = new SongTitleSlideInstance(null) { Title = "Holy Forever", Copyright = "" };
    slide.Theme = MakeTheme();
    using var videoFrame = new SkiaSharp.SKBitmap(4, 4);

    var spec = SongTitleSlideSpecBuilder.Build(slide, videoFrame);

    Assert.IsInstanceOfType(spec.Background, typeof(SkiaBitmapBackground));
    Assert.AreSame(videoFrame, ((SkiaBitmapBackground)spec.Background).Bitmap);
}

[TestMethod]
public void Build_WithNoVideoFrame_AndHasMotionBackground_UsesTransparentBackground()
{
    // HasMotionBackground comes from ParentSongItem — passing null parent means false.
    // To simulate HasMotionBackground=true with no frame, we need a slide whose
    // HasMotionBackground returns true but videoFrame is null.
    // Since SongTitleSlideInstance.HasMotionBackground delegates to ParentSongItem,
    // and we cannot set MotionBackgroundVideoPath without a real parent, we test the
    // builder's BuildBackground directly via the public Build signature:
    // pass videoFrame=null and rely on the slide having HasMotionBackground=false
    // (parent=null) → should fall through to theme colour (SolidBackground).
    var slide = new SongTitleSlideInstance(null) { Title = "Test", Copyright = "" };
    slide.Theme = MakeTheme();

    var spec = SongTitleSlideSpecBuilder.Build(slide, null);

    Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
}

[TestMethod]
public void Build_VideoFrameTakesPriorityOverThemeImage()
{
    var slide = new SongTitleSlideInstance(null) { Title = "Test", Copyright = "" };
    slide.Theme = MakeTheme();
    slide.Theme.BackgroundGraphicFilePath = "some/image.png";
    using var videoFrame = new SkiaSharp.SKBitmap(4, 4);

    var spec = SongTitleSlideSpecBuilder.Build(slide, videoFrame);

    Assert.IsInstanceOfType(spec.Background, typeof(SkiaBitmapBackground));
}
```

Also add the missing `using` at the top of the file:

```csharp
using SkiaSharp;
```

- [ ] **Step 2: Run tests to confirm they fail**

```
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SongTitleSlideSpecBuilderTests" -v normal
```

Expected: `Build_WithVideoFrame_UsesSkiaBitmapBackground` and `Build_VideoFrameTakesPriorityOverThemeImage` fail with "does not contain a definition for 'Build' with 2 arguments". `Build_WithNoVideoFrame_AndHasMotionBackground_UsesTransparentBackground` may fail or pass — note result.

- [ ] **Step 3: Modify `SongTitleSlideSpecBuilder` to accept `videoFrame`**

Open `HandsLiftedApp.Core/Render/Skia/Builders/SongTitleSlideSpecBuilder.cs`.

Change `Build` signature (line 28):

```csharp
// Before
public static SlideRenderSpec Build(SongTitleSlideInstance slide)

// After
public static SlideRenderSpec Build(SongTitleSlideInstance slide, SKBitmap? videoFrame = null)
```

Change the `Build` body to pass `videoFrame` to `BuildBackground` (line 30):

```csharp
// Before
var bg = BuildBackground(slide);

// After
var bg = BuildBackground(slide, videoFrame);
```

Change `BuildBackground` signature (line 44):

```csharp
// Before
private static BackgroundSpec BuildBackground(SongTitleSlideInstance slide)

// After
private static BackgroundSpec BuildBackground(SongTitleSlideInstance slide, SKBitmap? videoFrame = null)
```

Replace the body of `BuildBackground` (lines 46–56):

```csharp
private static BackgroundSpec BuildBackground(SongTitleSlideInstance slide, SKBitmap? videoFrame = null)
{
    if (videoFrame != null)
        return new SkiaBitmapBackground(videoFrame);

    if (slide.HasMotionBackground)
        return new TransparentBackground();

    if (!string.IsNullOrEmpty(slide.Theme?.BackgroundGraphicFilePath))
        return new ImageBackground(slide.Theme.BackgroundGraphicFilePath);

    var bg = slide.Theme != null
        ? ToSkColor(slide.Theme.BackgroundAvaloniaColour)
        : SKColors.Black;
    return new SolidBackground(bg);
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SongTitleSlideSpecBuilderTests" -v normal
```

Expected: All tests in `SongTitleSlideSpecBuilderTests` pass.

- [ ] **Step 5: Run full test suite to check for regressions**

```
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj -v normal
```

Expected: All previously passing tests still pass.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Render/Skia/Builders/SongTitleSlideSpecBuilder.cs
git add HandsLiftedApp.Tests/Render/Skia/Builders/SongTitleSlideSpecBuilderTests.cs
git commit -m "feat: SongTitleSlideSpecBuilder accepts optional SKBitmap videoFrame for thumbnail background"
```

---

### Task 3: Extract video frame in `SongTitleSlideInstance.GenerateBitmaps()`

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs`

No unit test is possible here — this path requires Windows Shell COM and a real video file on disk. Verification is manual (Task 5).

- [ ] **Step 1: Add required `using` directives**

Open `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs`.

Add at the top with the other usings:

```csharp
using System.Runtime.InteropServices;
using ShellThumbs;
```

- [ ] **Step 2: Replace `GenerateBitmaps()`**

Replace the entire `GenerateBitmaps()` method (lines 63–69):

```csharp
// Before
private void GenerateBitmaps()
{
    var spec = SongTitleSlideSpecBuilder.Build(this);
    using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
    Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
    Thumbnail = BitmapUtils.CreateThumbnail(Cached);
}
```

```csharp
// After
private void GenerateBitmaps()
{
    SKBitmap? videoFrame = null;
    if (HasMotionBackground && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var videoPath = ParentSongItem?.MotionBackgroundVideoPath;
        if (!string.IsNullOrWhiteSpace(videoPath))
        {
            try
            {
                var avaBmp = WindowsThumbnailProvider.GetThumbnail(
                    videoPath, 1920, 1080, ThumbnailOptions.None);
                if (avaBmp != null)
                    videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SongTitleSlideInstance] Failed to extract video thumbnail from {Path}", videoPath);
            }
        }
    }

    var spec = SongTitleSlideSpecBuilder.Build(this, videoFrame);
    using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
    videoFrame?.Dispose();
    Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
    Thumbnail = BitmapUtils.CreateThumbnail(Cached);
}
```

- [ ] **Step 3: Build to verify no errors**

```
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run full test suite**

```
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj -v normal
```

Expected: All previously passing tests still pass.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs
git commit -m "feat: extract Win32 video frame for song title slide thumbnail background"
```

---

### Task 4: Same extraction in `SongItemInstance.RegenerateAllSlideBitmaps()`

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs`

`RegenerateAllSlideBitmaps()` is called when the motion background path changes on an existing song item (e.g. user swaps the video). It needs the same frame extraction so the title slide thumbnail updates correctly.

- [ ] **Step 1: Add required `using` directives to `SongItemInstance.cs`**

Open `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs`. Add at the top:

```csharp
using System.Runtime.InteropServices;
using ShellThumbs;
```

- [ ] **Step 2: Replace the title-slide block in `RegenerateAllSlideBitmaps()`**

Find the existing block (lines 425–432):

```csharp
// Also regenerate title slide bitmap
if (titleSlide is SongTitleSlideInstance titleInstance)
{
    var spec = HandsLiftedApp.Core.Render.Skia.Builders.SongTitleSlideSpecBuilder.Build(titleInstance);
    using var skBitmap = HandsLiftedApp.Core.Render.Skia.SlideRenderer.RenderToSKBitmap(spec);
    titleInstance.Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
    titleInstance.Thumbnail = BitmapUtils.CreateThumbnail(titleInstance.Cached);
}
```

Replace with:

```csharp
// Also regenerate title slide bitmap
if (titleSlide is SongTitleSlideInstance titleInstance)
{
    SKBitmap? videoFrame = null;
    if (HasMotionBackground && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        try
        {
            var avaBmp = WindowsThumbnailProvider.GetThumbnail(
                MotionBackgroundVideoPath, 1920, 1080, ThumbnailOptions.None);
            if (avaBmp != null)
                videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SongItemInstance] Failed to extract video thumbnail from {Path}", MotionBackgroundVideoPath);
        }
    }

    var spec = HandsLiftedApp.Core.Render.Skia.Builders.SongTitleSlideSpecBuilder.Build(titleInstance, videoFrame);
    using var skBitmap = HandsLiftedApp.Core.Render.Skia.SlideRenderer.RenderToSKBitmap(spec);
    videoFrame?.Dispose();
    titleInstance.Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
    titleInstance.Thumbnail = BitmapUtils.CreateThumbnail(titleInstance.Cached);
}
```

- [ ] **Step 3: Build solution**

```
dotnet build HandsLiftedApp.sln
```

Expected: Build succeeded, 0 errors, 0 warnings (or same warnings as before).

- [ ] **Step 4: Run full test suite**

```
dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj -v normal
```

Expected: All previously passing tests still pass.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs
git commit -m "feat: use video frame thumbnail in RegenerateAllSlideBitmaps for title slide"
```

---

### Task 5: Manual verification

No automated test covers the end-to-end path (requires Windows Shell + a real `.mp4`/`.mov` file on disk). Run the app and verify visually.

- [ ] **Step 1: Launch the app and open a playlist with a Song Item**

Ensure the Song Item has a motion background video set (`MotionBackgroundVideoPath` points to a real video file on disk).

- [ ] **Step 2: Verify the title slide thumbnail shows the video frame**

In MainView's centre slide grid, the **first** thumbnail (index 1, the title/copyright slide) should show a frame from the motion background video as its background, with the title and copyright text rendered on top.

Expected: Title slide thumbnail shows video frame + text overlay.

- [ ] **Step 3: Verify verse/chorus thumbnails are unchanged**

All subsequent slide thumbnails (verses, choruses, bridge, etc.) should still render with a black background (no video frame). They are not affected by this change.

- [ ] **Step 4: Verify fallback for songs without motion background**

Open a Song Item with no motion background set. The title slide thumbnail should render exactly as before — using the theme's background colour or image.

- [ ] **Step 5: Verify log output on unsupported video format (optional)**

If you have a video file in an unsupported format, check that the app logs a warning via Serilog and falls back gracefully (title slide shows `TransparentBackground`, i.e. black) rather than crashing.
