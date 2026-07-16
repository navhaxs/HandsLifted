# LibMpv.Thumbnailing — Cross-Platform Video Thumbnail Extractor

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a cross-platform mpv-backed thumbnail extractor as an **opt-in** codepath alongside the existing Win32 implementation. Win32 remains the default on Windows; mpv is used when explicitly enabled or when running on non-Windows.

**Architecture:** A new `LibMpv.Thumbnailing` project exposes `MpvThumbnailExtractor.ExtractAsync()` that spins up a headless `MpvContext`, seeks to a target frame via software render, and returns an Avalonia `WriteableBitmap`. A new `ThumbnailEngineSettings` class (in `HandsLiftedApp.Core`) holds a static `UseMpvEngine` flag (default: `false` on Windows, `true` on non-Windows). All three call sites in `HandsLiftedApp.Core` check this flag: `SongTitleSlideInstance` and `SongItemInstance` choose between `MpvThumbnailExtractor` and `WindowsThumbnailProvider`; `LibraryQueryView` chooses between `MpvThumbnailImageLoader` and `WindowsThumbnailImageLoader`. The Win32 code is **not removed**.

**Tech Stack:** C# / .NET 8, `LibMpv.Context` (existing), Avalonia `WriteableBitmap`, xUnit

## Global Constraints

- Target framework: `net8.0`
- `AllowUnsafeBlocks`: required (matches `LibMpv.Context`)
- `Nullable`: enabled
- `ImplicitUsings`: enabled
- No new NuGet packages — only project references and packages already in the solution
- The extractor must **not** reuse `Globals.Instance.MpvContextInstance` or the motion-background context; it creates and disposes its own `MpvContext` per call
- Default timeout for frame extraction: 15 seconds
- Do NOT set `vo=null` — the software render API replaces the VO; setting `vo=null` prevents frame delivery

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `Libraries/LibMpv/LibMpv.Thumbnailing/LibMpv.Thumbnailing.csproj` | Create | Project definition |
| `Libraries/LibMpv/LibMpv.Thumbnailing/MpvThumbnailExtractor.cs` | Create | Headless mpv frame extractor |
| `Libraries/LibMpv/LibMpv.Thumbnailing.Tests/LibMpv.Thumbnailing.Tests.csproj` | Create | xUnit test project |
| `Libraries/LibMpv/LibMpv.Thumbnailing.Tests/MpvThumbnailExtractorTests.cs` | Create | Integration tests |
| `HandsLiftedApp.Core/HandsLiftedApp.Core.csproj` | Modify | Add reference to `LibMpv.Thumbnailing` |
| `HandsLiftedApp.Core/Utils/ThumbnailEngineSettings.cs` | Create | Opt-in flag + engine selection helper |
| `HandsLiftedApp.Core/Utils/MpvThumbnailImageLoader.cs` | Create | `IAsyncImageLoader` impl wrapping extractor |
| `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs` | Modify | Add mpv opt-in path alongside Win32 |
| `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs` | Modify | Add mpv opt-in path alongside Win32 |
| `HandsLiftedApp.Core/Views/LibraryView/LibraryQueryView.axaml.cs` | Modify | Select loader via ThumbnailEngineSettings |
| `HandsLiftedApp.Core/Utils/ShellThumbs.cs` | **Keep** | Win32 default path — not removed |
| `HandsLiftedApp.Core/Utils/WindowsThumbnailImageLoader.cs` | **Keep** | Win32 default path — not removed |

---

## Task 1: Project Scaffold

**Files:**
- Create: `Libraries/LibMpv/LibMpv.Thumbnailing/LibMpv.Thumbnailing.csproj`
- Create: `Libraries/LibMpv/LibMpv.Thumbnailing.Tests/LibMpv.Thumbnailing.Tests.csproj`
- Modify: `HandsLiftedApp.sln` (via `dotnet sln add`)
- Modify: `HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`

**Interfaces:**
- Produces: `LibMpv.Thumbnailing` project, buildable and referenceable

- [ ] **Step 1: Create the library project file**

```xml
<!-- Libraries/LibMpv/LibMpv.Thumbnailing/LibMpv.Thumbnailing.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\src\LibMpv.Context\LibMpv.Context.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the test project file**

```xml
<!-- Libraries/LibMpv/LibMpv.Thumbnailing.Tests/LibMpv.Thumbnailing.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\LibMpv.Thumbnailing\LibMpv.Thumbnailing.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add both projects to the solution**

```bash
cd C:/Users/Jeremy/RiderProjects/HandsLifted
dotnet sln HandsLiftedApp.sln add Libraries/LibMpv/LibMpv.Thumbnailing/LibMpv.Thumbnailing.csproj
dotnet sln HandsLiftedApp.sln add Libraries/LibMpv/LibMpv.Thumbnailing.Tests/LibMpv.Thumbnailing.Tests.csproj
```

Expected output: `Project ... added to the solution.` for each.

- [ ] **Step 4: Add reference in HandsLiftedApp.Core.csproj**

In `HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`, inside the existing `<ItemGroup>` with `ProjectReference` entries, add:

```xml
<ProjectReference Include="..\Libraries\LibMpv\LibMpv.Thumbnailing\LibMpv.Thumbnailing.csproj"/>
```

- [ ] **Step 5: Verify the solution builds**

```bash
dotnet build HandsLiftedApp.sln --no-incremental -v quiet
```

Expected: no errors.

- [ ] **Step 6: Commit scaffold**

```bash
git add Libraries/LibMpv/LibMpv.Thumbnailing/ Libraries/LibMpv/LibMpv.Thumbnailing.Tests/ HandsLiftedApp.sln HandsLiftedApp.Core/HandsLiftedApp.Core.csproj
git commit -m "chore: add LibMpv.Thumbnailing project scaffold"
```

---

## Task 2: MpvThumbnailExtractor (TDD)

**Files:**
- Create: `Libraries/LibMpv/LibMpv.Thumbnailing.Tests/MpvThumbnailExtractorTests.cs`
- Create: `Libraries/LibMpv/LibMpv.Thumbnailing.Tests/TestFixtures/test-video.mkv` (tiny 2-second video, see step 1)
- Create: `Libraries/LibMpv/LibMpv.Thumbnailing/MpvThumbnailExtractor.cs`

**Interfaces:**
- Produces:
  ```csharp
  // namespace LibMpv.Thumbnailing
  public static class MpvThumbnailExtractor
  {
      public static Task<WriteableBitmap?> ExtractAsync(
          string filePath,
          double seekFraction = 0.1,
          int maxWidth = 1280,
          int maxHeight = 720,
          CancellationToken ct = default);
  }
  ```

**Key implementation details to understand before coding:**

`MpvContext.StartSoftwareRendering(UpdateCallback)` creates an `MPV_RENDER_API_TYPE_SW` render context. Internally it calls `StopRendering()` first — safe on a fresh context. The `UpdateCallback` fires (on mpv's render thread) whenever mpv has a new frame ready to be pulled via `SoftwareRender()`.

`MpvContext.SoftwareRender(width, height, surfaceAddress, format)` writes pixels to a pinned memory address. Format `"bgra"` matches Avalonia's `PixelFormat.Bgra8888`. Avalonia's `WriteableBitmap.Lock()` returns an `ILockedFramebuffer` whose `.Address` is a `nint` — pass it directly to `SoftwareRender`.

**Synchronization approach:** Use `TaskCompletionSource` wired to the `UpdateCallback`. The callback fires multiple times (once per decoded frame). For seeking: after `FileLoaded`, reset a `SemaphoreSlim` to capture the next post-seek frame signal. Use a 5-second timeout on the seek phase so a failed seek falls back to the first-frame result.

- [ ] **Step 1: Create a tiny test video file**

Use ffmpeg to generate a 2-second test video (240×180, color bars):

```bash
ffmpeg -f lavfi -i testsrc=duration=2:size=240x180:rate=25 -c:v libx264 -preset ultrafast \
  "Libraries/LibMpv/LibMpv.Thumbnailing.Tests/TestFixtures/test-video.mkv"
```

If ffmpeg is not available, download any small public-domain video and place it at that path. The file must be a valid video file readable by mpv.

Add it to the test project csproj so it copies to output:

```xml
<!-- inside LibMpv.Thumbnailing.Tests.csproj -->
<ItemGroup>
  <Content Include="TestFixtures\test-video.mkv">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

```csharp
// Libraries/LibMpv/LibMpv.Thumbnailing.Tests/MpvThumbnailExtractorTests.cs
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using LibMpv.Thumbnailing;
using Xunit;

namespace LibMpv.Thumbnailing.Tests;

public class MpvThumbnailExtractorTests
{
    private static readonly string TestVideoPath =
        Path.Combine(Path.GetDirectoryName(typeof(MpvThumbnailExtractorTests).Assembly.Location)!,
            "TestFixtures", "test-video.mkv");

    [Fact]
    public async Task ExtractAsync_ValidVideo_ReturnsNonNullBitmap()
    {
        var result = await MpvThumbnailExtractor.ExtractAsync(TestVideoPath);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExtractAsync_ValidVideo_ReturnsCorrectDimensions()
    {
        var result = await MpvThumbnailExtractor.ExtractAsync(
            TestVideoPath, maxWidth: 320, maxHeight: 240);

        Assert.NotNull(result);
        Assert.True(result!.PixelSize.Width <= 320);
        Assert.True(result!.PixelSize.Height <= 240);
        Assert.True(result!.PixelSize.Width > 0);
        Assert.True(result!.PixelSize.Height > 0);
    }

    [Fact]
    public async Task ExtractAsync_NonExistentFile_ReturnsNull()
    {
        var result = await MpvThumbnailExtractor.ExtractAsync("/does/not/exist.mp4");

        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractAsync_ValidVideo_CanBeCalledConcurrently()
    {
        var tasks = new[]
        {
            MpvThumbnailExtractor.ExtractAsync(TestVideoPath),
            MpvThumbnailExtractor.ExtractAsync(TestVideoPath),
        };

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.NotNull(r));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test Libraries/LibMpv/LibMpv.Thumbnailing.Tests/ --no-build -v normal 2>&1 | head -40
```

Expected: `Build FAILED` (class doesn't exist yet) or `ExtractAsync_ValidVideo_ReturnsNonNullBitmap FAILED` with `TypeNotFoundException`.

- [ ] **Step 4: Implement MpvThumbnailExtractor**

```csharp
// Libraries/LibMpv/LibMpv.Thumbnailing/MpvThumbnailExtractor.cs
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using LibMpv.Client;
using Serilog;

namespace LibMpv.Thumbnailing;

public static class MpvThumbnailExtractor
{
    private static readonly TimeSpan FileLoadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SeekTimeout = TimeSpan.FromSeconds(5);

    public static async Task<WriteableBitmap?> ExtractAsync(
        string filePath,
        double seekFraction = 0.1,
        int maxWidth = 1280,
        int maxHeight = 720,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            return await Task.Run(() => ExtractInternal(filePath, seekFraction, maxWidth, maxHeight, cts.Token), cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("[MpvThumbnailExtractor] Extraction timed out or cancelled for {Path}", filePath);
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[MpvThumbnailExtractor] Extraction failed for {Path}", filePath);
            return null;
        }
    }

    private static WriteableBitmap? ExtractInternal(
        string filePath,
        double seekFraction,
        int maxWidth,
        int maxHeight,
        CancellationToken ct)
    {
        var ctx = new MpvContext();
        try
        {
            // Configure for headless thumbnail extraction.
            // Do NOT set vo=null — the SW render API replaces the VO and needs frame delivery.
            ctx.SetPropertyString("pause", "yes");
            ctx.SetPropertyString("aid", "no");    // skip audio decode — faster
            ctx.SetPropertyString("hr-seek", "no"); // keyframe-only seek — faster

            var fileLoadedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ctx.FileLoaded += (_, _) => fileLoadedTcs.TrySetResult(true);
            ctx.EndFile  += (_, _) => fileLoadedTcs.TrySetCanceled();

            // frameSignal fires each time mpv has a new frame ready to pull via SoftwareRender.
            var frameSignal = new SemaphoreSlim(0);
            ctx.StartSoftwareRendering(() =>
            {
                try { frameSignal.Release(); }
                catch { /* semaphore disposed */ }
            });

            ctx.Command("loadfile", filePath);

            // Wait for file to load
            fileLoadedTcs.Task.Wait((int)FileLoadTimeout.TotalMilliseconds, ct);
            if (!fileLoadedTcs.Task.IsCompletedSuccessfully)
                return null;

            // Determine output dimensions from video dimensions
            int vidW = 0, vidH = 0;
            try
            {
                int.TryParse(ctx.GetPropertyString("width"), out vidW);
                int.TryParse(ctx.GetPropertyString("height"), out vidH);
            }
            catch { }

            if (vidW <= 0 || vidH <= 0)
                return null;

            double scale = Math.Min((double)maxWidth / vidW, (double)maxHeight / vidH);
            int outW = Math.Max(1, (int)Math.Round(vidW * scale));
            int outH = Math.Max(1, (int)Math.Round(vidH * scale));

            // Wait for first decodable frame (mpv fires UpdateCallback when frame ready)
            if (!frameSignal.Wait((int)FileLoadTimeout.TotalMilliseconds, ct))
                return null;

            var bitmap = RenderFrame(ctx, outW, outH);

            // Seek to target position for a better thumbnail frame
            if (seekFraction > 0)
            {
                double duration = 0;
                try { double.TryParse(ctx.GetPropertyString("duration"), out duration); }
                catch { }

                if (duration > 1.0)
                {
                    double seekSec = Math.Clamp(duration * seekFraction, 0.0, duration - 0.1);
                    ctx.Command("seek", seekSec.ToString("F3", CultureInfo.InvariantCulture), "absolute");

                    // Drain any pre-seek frames already in the semaphore, then wait for the post-seek frame
                    while (frameSignal.CurrentCount > 0)
                        frameSignal.Wait(0);

                    if (frameSignal.Wait((int)SeekTimeout.TotalMilliseconds, ct))
                        bitmap = RenderFrame(ctx, outW, outH);
                    // else: seek timed out — keep the first-frame bitmap
                }
            }

            return bitmap;
        }
        finally
        {
            ctx.Dispose();
        }
    }

    private static WriteableBitmap RenderFrame(MpvContext ctx, int outW, int outH)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(outW, outH),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var buf = bitmap.Lock();
        ctx.SoftwareRender(outW, outH, buf.Address, "bgra");

        return bitmap;
    }
}
```

> **Note on `MpvContext.Dispose()`:** Confirm that `MpvContext` implements `IDisposable`. If not, call `ctx.Terminate()` or equivalent cleanup method instead. Check `Libraries/LibMpv/src/LibMpv.Context/MpvContext.cs` for the disposal pattern.

- [ ] **Step 5: Run tests**

```bash
dotnet test Libraries/LibMpv/LibMpv.Thumbnailing.Tests/ -v normal
```

Expected: all 4 tests pass. If the `EndFile` event is not exposed on `MpvContext`, remove the `ctx.EndFile` subscription line — the `FileLoadTimeout` handles the failure case.

- [ ] **Step 6: Commit**

```bash
git add Libraries/LibMpv/LibMpv.Thumbnailing/ Libraries/LibMpv/LibMpv.Thumbnailing.Tests/
git commit -m "feat: add MpvThumbnailExtractor for cross-platform video frame extraction"
```

---

## Task 3: ThumbnailEngineSettings + MpvThumbnailImageLoader

**Files:**
- Create: `HandsLiftedApp.Core/Utils/ThumbnailEngineSettings.cs`
- Create: `HandsLiftedApp.Core/Utils/MpvThumbnailImageLoader.cs`
- Modify: `HandsLiftedApp.Core/Views/LibraryView/LibraryQueryView.axaml.cs`

**Interfaces:**
- Consumes: `MpvThumbnailExtractor.ExtractAsync(string, ...) → Task<WriteableBitmap?>`
- Produces:
  ```csharp
  // namespace HandsLiftedApp.Core.Utils
  public static class ThumbnailEngineSettings
  {
      public static bool UseMpvEngine { get; set; }  // false on Windows by default
  }

  public class MpvThumbnailImageLoader : IAsyncImageLoader { ... }
  ```

- [ ] **Step 1: Add IsWriteableBitmapBitmap test (sanity check)**

Add to `Libraries/LibMpv/LibMpv.Thumbnailing.Tests/MpvThumbnailExtractorTests.cs` after existing tests:

```csharp
[Fact]
public async Task ExtractAsync_ReturnsWriteableBitmap_AssignableToAvaloniaBitmap()
{
    var result = await MpvThumbnailExtractor.ExtractAsync(TestVideoPath);

    Assert.NotNull(result);
    Avalonia.Media.Imaging.Bitmap bmp = result!;  // compile-time check: WriteableBitmap : Bitmap
    Assert.True(bmp.PixelSize.Width > 0);
}
```

Run:

```bash
dotnet test Libraries/LibMpv/LibMpv.Thumbnailing.Tests/ -v normal
```

Expected: all 5 tests pass.

- [ ] **Step 2: Create ThumbnailEngineSettings**

```csharp
// HandsLiftedApp.Core/Utils/ThumbnailEngineSettings.cs
using System.Runtime.InteropServices;

namespace HandsLiftedApp.Core.Utils;

public static class ThumbnailEngineSettings
{
    // On Windows: defaults to false (Win32 is the default path).
    // On non-Windows: always true (Win32 is unavailable).
    // Set to true at app startup to opt into mpv-backed thumbnailing on Windows.
    public static bool UseMpvEngine { get; set; } =
        !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}
```

- [ ] **Step 3: Implement MpvThumbnailImageLoader**

```csharp
// HandsLiftedApp.Core/Utils/MpvThumbnailImageLoader.cs
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia.Media.Imaging;
using LibMpv.Thumbnailing;
using Serilog;

namespace HandsLiftedApp.Core.Utils;

public class MpvThumbnailImageLoader : IAsyncImageLoader
{
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _cache = new();

    public async Task<Bitmap?> ProvideImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url) || !File.Exists(url))
            throw new FileNotFoundException("File does not exist.", url);

        var bitmap = await _cache.GetOrAdd(url, LoadAsync).ConfigureAwait(false);

        if (bitmap == null)
            _cache.TryRemove(url, out _);

        return bitmap;
    }

    private static Task<Bitmap?> LoadAsync(string path) =>
        MpvThumbnailExtractor.ExtractAsync(path, maxWidth: 1280, maxHeight: 720)
            .ContinueWith<Bitmap?>(t =>
            {
                if (t.IsFaulted)
                {
                    Log.Error(t.Exception, "[MpvThumbnailImageLoader] Failed to load thumbnail for {Path}", path);
                    return null;
                }
                return t.Result;
            });

    public void Dispose() { }
}
```

- [ ] **Step 4: Update LibraryQueryView to select loader via flag**

Open `HandsLiftedApp.Core/Views/LibraryView/LibraryQueryView.axaml.cs`. Find (approximately line 26):

```csharp
AsyncImageLoader.ImageLoader.AsyncImageLoader = new WindowsThumbnailImageLoader();
```

Replace with:

```csharp
AsyncImageLoader.ImageLoader.AsyncImageLoader = ThumbnailEngineSettings.UseMpvEngine
    ? new MpvThumbnailImageLoader()
    : new WindowsThumbnailImageLoader();
```

Add at the top if not present:

```csharp
using HandsLiftedApp.Core.Utils;
```

Do **not** remove `using ShellThumbs;` or `using HandsLiftedApp.Core.Utils;` — both loaders must remain available.

- [ ] **Step 5: Verify build**

```bash
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -v quiet
```

Expected: no errors.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Utils/ThumbnailEngineSettings.cs \
        HandsLiftedApp.Core/Utils/MpvThumbnailImageLoader.cs \
        HandsLiftedApp.Core/Views/LibraryView/LibraryQueryView.axaml.cs
git commit -m "feat: add MpvThumbnailImageLoader with opt-in flag via ThumbnailEngineSettings"
```

---

## Task 4: Add Mpv Opt-In Path in Slide Generation

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs`
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs`

**Interfaces:**
- Consumes: `ThumbnailEngineSettings.UseMpvEngine → bool`
- Consumes: `MpvThumbnailExtractor.ExtractAsync → Task<WriteableBitmap?>`
- Consumes: `WindowsThumbnailProvider.GetThumbnail` (existing Win32 path, kept intact)
- Consumes: `BitmapUtils.AvaloniaToSKBitmap(Bitmap) → SKBitmap` (already exists)

**Context:** Both call sites run on background threads, so `.GetAwaiter().GetResult()` on the mpv async call is safe. The Win32 path remains unchanged and guarded by `IsOSPlatform(Windows)`. The `RuntimeInformation` and `ShellThumbs` usings stay in both files.

- [ ] **Step 1: Update SongTitleSlideInstance.cs**

Open `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs`.

Find this block (approximately line 76–94):

```csharp
SKBitmap? videoFrame = null;
if (HasMotionBackground && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    var videoPath = ParentSongItem?.MotionBackgroundVideoPath;
    if (!string.IsNullOrWhiteSpace(videoPath))
    {
        try
        {
            using var avaBmp = WindowsThumbnailProvider.GetThumbnail(
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
```

Replace with:

```csharp
SKBitmap? videoFrame = null;
if (HasMotionBackground)
{
    var videoPath = ParentSongItem?.MotionBackgroundVideoPath;
    if (!string.IsNullOrWhiteSpace(videoPath))
    {
        if (ThumbnailEngineSettings.UseMpvEngine)
        {
            using var avaBmp = MpvThumbnailExtractor.ExtractAsync(videoPath, maxWidth: 1920, maxHeight: 1080)
                .GetAwaiter().GetResult();
            if (avaBmp != null)
                videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var avaBmp = WindowsThumbnailProvider.GetThumbnail(
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
}
```

Add at the top of the file (alongside existing usings):

```csharp
using HandsLiftedApp.Core.Utils;
using LibMpv.Thumbnailing;
```

- [ ] **Step 2: Update SongItemInstance.cs**

Open `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs`.

Find this block (approximately line 443–457):

```csharp
SKBitmap? videoFrame = null;
if (HasMotionBackground && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    try
    {
        using var avaBmp = WindowsThumbnailProvider.GetThumbnail(
            MotionBackgroundVideoPath, 1920, 1080, ThumbnailOptions.None);
        if (avaBmp != null)
            videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "[SongItemInstance] Failed to extract video thumbnail from {Path}", MotionBackgroundVideoPath);
    }
}
```

Replace with:

```csharp
SKBitmap? videoFrame = null;
if (HasMotionBackground)
{
    if (ThumbnailEngineSettings.UseMpvEngine)
    {
        using var avaBmp = MpvThumbnailExtractor.ExtractAsync(MotionBackgroundVideoPath, maxWidth: 1920, maxHeight: 1080)
            .GetAwaiter().GetResult();
        if (avaBmp != null)
            videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        try
        {
            using var avaBmp = WindowsThumbnailProvider.GetThumbnail(
                MotionBackgroundVideoPath, 1920, 1080, ThumbnailOptions.None);
            if (avaBmp != null)
                videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SongItemInstance] Failed to extract video thumbnail from {Path}", MotionBackgroundVideoPath);
        }
    }
}
```

Add at the top (alongside existing usings):

```csharp
using HandsLiftedApp.Core.Utils;
using LibMpv.Thumbnailing;
```

Leave `using ShellThumbs;` and `using System.Runtime.InteropServices;` in place — the Win32 path still uses them.

- [ ] **Step 3: Verify build**

```bash
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -v quiet
```

Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs
git add HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs
git commit -m "feat: add mpv opt-in thumbnail path in slide generation alongside Win32 fallback"
```

---

## Task 5: Verify Coexistence + Final Smoke Test

**Files:** No changes — read-only verification.

- [ ] **Step 1: Run all tests**

```bash
dotnet test Libraries/LibMpv/LibMpv.Thumbnailing.Tests/ -v normal
```

Expected: all 5 tests pass.

- [ ] **Step 2: Verify full solution builds**

```bash
dotnet build HandsLiftedApp.sln -v quiet
```

Expected: no errors.

- [ ] **Step 3: Verify Win32 path still reachable (grep check)**

```bash
grep -rn "WindowsThumbnailProvider\|WindowsThumbnailImageLoader" HandsLiftedApp.Core --include="*.cs"
```

Expected: hits in `ShellThumbs.cs`, `WindowsThumbnailImageLoader.cs`, `SongTitleSlideInstance.cs`, `SongItemInstance.cs`, `LibraryQueryView.axaml.cs`. The Win32 code must still be present in all call sites.

- [ ] **Step 4: Verify opt-in flag is wired**

```bash
grep -rn "ThumbnailEngineSettings\|UseMpvEngine" HandsLiftedApp.Core --include="*.cs"
```

Expected: hits in `ThumbnailEngineSettings.cs`, `LibraryQueryView.axaml.cs`, `SongTitleSlideInstance.cs`, `SongItemInstance.cs`.

- [ ] **Step 5: Document how to enable mpv engine**

> To enable the mpv thumbnail engine at runtime, set `ThumbnailEngineSettings.UseMpvEngine = true` before the app loads any thumbnails (e.g., in app startup / `App.axaml.cs` `OnFrameworkInitializationCompleted`). On non-Windows, the flag defaults to `true` automatically and Win32 is never called.

- [ ] **Step 6: Final commit (if any cleanup needed)**

```bash
git add -A
git status  # confirm no unintended files
git commit -m "chore: verify coexistence of Win32 and mpv thumbnail engines"
```

---

## Self-Review

**Spec coverage:**
- Cross-platform mpv frame extractor: Task 2 ✓
- Opt-in flag (`ThumbnailEngineSettings.UseMpvEngine`): Task 3 ✓
- Win32 as default on Windows: Tasks 3 and 4 (Win32 path kept, flag defaults to `false` on Windows) ✓
- Mpv path used on non-Windows automatically: `UseMpvEngine` defaults to `true` on non-Windows ✓
- Library file browser thumbnails: Task 3 (loader selected by flag) ✓
- Slide generation thumbnails: Task 4 (both paths present, flag-gated) ✓
- Win32 files NOT deleted: confirmed — ShellThumbs.cs and WindowsThumbnailImageLoader.cs kept ✓
- Uses existing LibMPV integration: yes — `MpvContext.StartSoftwareRendering` + `SoftwareRender` ✓
- Does not reuse global MpvContext: each extraction creates and disposes its own context ✓

**Placeholder scan:** All steps contain actual code. No TBD/TODO/similar.

**Type consistency:**
- `ThumbnailEngineSettings.UseMpvEngine` is `bool` — used in `if` guards in Tasks 3 and 4 ✓
- `MpvThumbnailExtractor.ExtractAsync` returns `Task<WriteableBitmap?>` — used as `Bitmap?` (valid: `WriteableBitmap : Bitmap`) in Task 3 ✓
- `BitmapUtils.AvaloniaToSKBitmap(Bitmap)` called with `WriteableBitmap` — valid since `WriteableBitmap : Bitmap` ✓
- `ctx.SoftwareRender(int, int, nint, string)` — signature matches `MpvContext.Rendering.cs:183` ✓
- `ctx.StartSoftwareRendering(UpdateCallback)` where `UpdateCallback = delegate void ()` — matches existing signature ✓
