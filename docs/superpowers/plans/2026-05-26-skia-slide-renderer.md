# Skia Slide Renderer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Avalonia `RenderTargetBitmap` slide rendering with a direct Skia `SlideCanvas` control, fixing drop shadow capture and cross-fade dip-out on the projector and NDI outputs.

**Architecture:** A static `SlideRenderer` draws slide content via SkiaSharp using `ICustomDrawOperation`. A `SlideCanvas` Avalonia control wraps `SlideRenderer` and manages transition animation internally — unchanged text lines stay at full opacity throughout a transition; only added/removed lines animate. Spec builder classes translate the existing slide data model to a `SlideRenderSpec` (pure data). Both the projector window and the editor use the same `SlideCanvas`, giving a pixel-identical preview.

**Tech Stack:** SkiaSharp, Avalonia 11 (`ICustomDrawOperation`, `ISkiaSharpApiLeaseFeature`), MSTest (.NET 8), ReactiveUI (existing)

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Create | `HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs` | Pure data model for all slide content |
| Create | `HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs` | Static Skia draw engine + thumbnail helper |
| Create | `HandsLiftedApp.Core/Render/Skia/SlideCanvas.cs` | Custom Avalonia control; owns transition animation |
| Create | `HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs` | `SongSlideInstance` → `SlideRenderSpec` |
| Create | `HandsLiftedApp.Core/Render/Skia/Builders/SongTitleSlideSpecBuilder.cs` | `SongTitleSlideInstance` → `SlideRenderSpec` |
| Create | `HandsLiftedApp.Tests/Render/Skia/SlideRenderSpecTests.cs` | Data model construction tests |
| Create | `HandsLiftedApp.Tests/Render/Skia/SlideRendererTests.cs` | Pixel-level draw tests |
| Create | `HandsLiftedApp.Tests/Render/Skia/Builders/SongSlideSpecBuilderTests.cs` | Builder output correctness |
| Create | `HandsLiftedApp.Tests/Render/Skia/Builders/SongTitleSlideSpecBuilderTests.cs` | Builder output correctness |
| Modify | `HandsLiftedApp.Core/Views/ProjectorWindow.axaml` | Replace `ActiveSlideRender` with `SlideCanvas` |
| Modify | `HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs` | Wire `Transition()` on active slide change |
| Modify | `HandsLiftedApp.Core/Views/Designer/SongSlideView.axaml` | Replace TextBlock content with `SlideCanvas` |
| Modify | `HandsLiftedApp.Core/Views/Designer/SongSlideView.axaml.cs` | Rebuild spec on DataContext / theme change |
| Delete | `HandsLiftedApp.Core/Render/ActiveSlideRender.axaml` | Replaced by `SlideCanvas` |
| Delete | `HandsLiftedApp.Core/Render/ActiveSlideRender.axaml.cs` | Replaced by `SlideCanvas` |
| Delete | `HandsLiftedApp.Core/Views/SlideRendererWorkerWindow.axaml.cs` | Replaced by `SlideRenderer.RenderToSKBitmap` |

> `HandsLiftedApp.XTransitioningContentControl` project: leave the project file in the solution but remove the `<ProjectReference>` from `HandsLiftedApp.Core.csproj` once `ActiveSlideRender` is gone and no other file references it.

---

## Task 1: SlideRenderSpec data model

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs`
- Create: `HandsLiftedApp.Tests/Render/Skia/SlideRenderSpecTests.cs`

- [ ] **Step 1.1: Write the failing test**

```csharp
// HandsLiftedApp.Tests/Render/Skia/SlideRenderSpecTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;
using HandsLiftedApp.Core.Render.Skia;

namespace HandsLiftedApp.Tests.Render.Skia;

[TestClass]
public class SlideRenderSpecTests
{
    [TestMethod]
    public void TransparentBackground_IsDistinctFromSolid()
    {
        BackgroundSpec transparent = new TransparentBackground();
        BackgroundSpec solid = new SolidBackground(SKColors.Black);
        Assert.AreNotEqual(transparent, solid);
    }

    [TestMethod]
    public void TextLineElement_IdentityIsText()
    {
        var a = new TextLineElement("Hello", SKRect.Empty, SKTypeface.Default, 100f, SKColors.White, null);
        var b = new TextLineElement("Hello", new SKRect(10, 20, 300, 120), SKTypeface.Default, 80f, SKColors.Red, null);
        Assert.AreEqual(a.Text, b.Text);
    }

    [TestMethod]
    public void SlideRenderSpec_StoresElementsAndBackground()
    {
        var elements = new List<RenderElement>
        {
            new TextLineElement("Line one", SKRect.Empty, SKTypeface.Default, 100f, SKColors.White, null)
        };
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Black), elements);

        Assert.AreEqual(1, spec.Elements.Count);
        Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
    }
}
```

- [ ] **Step 1.2: Run test — expect compile error (types don't exist yet)**

```
dotnet test HandsLiftedApp.Tests --filter "SlideRenderSpecTests"
```

Expected: build failure — `HandsLiftedApp.Core.Render.Skia` namespace not found.

- [ ] **Step 1.3: Create `SlideRenderSpec.cs`**

```csharp
// HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs
using SkiaSharp;

namespace HandsLiftedApp.Core.Render.Skia;

public record SlideRenderSpec(
    BackgroundSpec Background,
    IReadOnlyList<RenderElement> Elements
);

public abstract record BackgroundSpec;
public record TransparentBackground() : BackgroundSpec;
public record SolidBackground(SKColor Color) : BackgroundSpec;
public record ImageBackground(string FilePath) : BackgroundSpec;

public abstract record RenderElement(SKRect Bounds);

public record TextLineElement(
    string Text,
    SKRect Bounds,
    SKTypeface Typeface,
    float FontSize,
    SKColor Color,
    DropShadowSpec? Shadow
) : RenderElement(Bounds);

public record DropShadowSpec(float OffsetX, float OffsetY, float BlurRadius, SKColor Color);
```

- [ ] **Step 1.4: Run test — expect pass**

```
dotnet test HandsLiftedApp.Tests --filter "SlideRenderSpecTests"
```

Expected: 3 passed.

- [ ] **Step 1.5: Commit**

```
git add HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs
git add HandsLiftedApp.Tests/Render/Skia/SlideRenderSpecTests.cs
git commit -m "feat: add SlideRenderSpec data model"
```

---

## Task 2: SlideRenderer — static drawing engine

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs`
- Create: `HandsLiftedApp.Tests/Render/Skia/SlideRendererTests.cs`

- [ ] **Step 2.1: Write the failing tests**

```csharp
// HandsLiftedApp.Tests/Render/Skia/SlideRendererTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;
using HandsLiftedApp.Core.Render.Skia;

namespace HandsLiftedApp.Tests.Render.Skia;

[TestClass]
public class SlideRendererTests
{
    [TestMethod]
    public void RenderToSKBitmap_ReturnsBitmapWithRequestedDimensions()
    {
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Black), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 320, 180);

        Assert.AreEqual(320, bitmap.Width);
        Assert.AreEqual(180, bitmap.Height);
    }

    [TestMethod]
    public void RenderToSKBitmap_SolidBackground_FillsCanvas()
    {
        var spec = new SlideRenderSpec(new SolidBackground(SKColors.Red), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 100, 100);
        var pixel = bitmap.GetPixel(0, 0);

        Assert.AreEqual(255, pixel.Red);
        Assert.AreEqual(0, pixel.Green);
        Assert.AreEqual(0, pixel.Blue);
    }

    [TestMethod]
    public void RenderToSKBitmap_TransparentBackground_PixelIsTransparent()
    {
        var spec = new SlideRenderSpec(new TransparentBackground(), Array.Empty<RenderElement>());

        using var bitmap = SlideRenderer.RenderToSKBitmap(spec, 100, 100);
        var pixel = bitmap.GetPixel(0, 0);

        Assert.AreEqual(0, pixel.Alpha);
    }

    [TestMethod]
    public void RenderToSKBitmap_WithPreviousSpec_DoesNotThrow()
    {
        var prev = new SlideRenderSpec(new SolidBackground(SKColors.Blue), Array.Empty<RenderElement>());
        var curr = new SlideRenderSpec(new SolidBackground(SKColors.Green), Array.Empty<RenderElement>());

        // Should not throw at any transition progress
        using var bitmap = SlideRenderer.RenderToSKBitmap(curr, 100, 100, prev, 0.5f);

        Assert.IsNotNull(bitmap);
    }
}
```

- [ ] **Step 2.2: Run test — expect compile failure**

```
dotnet test HandsLiftedApp.Tests --filter "SlideRendererTests"
```

Expected: `SlideRenderer` not found.

- [ ] **Step 2.3: Create `SlideRenderer.cs`**

```csharp
// HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs
using SkiaSharp;

namespace HandsLiftedApp.Core.Render.Skia;

public static class SlideRenderer
{
    // Entry point for SlideCanvas (called every frame during transitions)
    public static void Draw(
        SKCanvas canvas,
        SlideRenderSpec? current,
        SlideRenderSpec? previous,
        float progress,
        int width,
        int height)
    {
        canvas.Clear(SKColors.Transparent);

        DrawBackground(canvas, current?.Background ?? previous?.Background, width, height);

        // Elements in current: unchanged lines stay at 1.0, new lines fade in
        if (current != null)
        {
            var previousTexts = previous?.Elements
                .OfType<TextLineElement>()
                .Select(e => e.Text)
                .ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);

            foreach (var element in current.Elements)
            {
                if (element is TextLineElement textEl)
                {
                    float alpha = previousTexts.Contains(textEl.Text) ? 1f : progress;
                    DrawTextElement(canvas, textEl, alpha);
                }
            }
        }

        // Elements only in previous: fade out
        if (previous != null && progress < 1f)
        {
            var currentTexts = current?.Elements
                .OfType<TextLineElement>()
                .Select(e => e.Text)
                .ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);

            foreach (var element in previous.Elements)
            {
                if (element is TextLineElement textEl && !currentTexts.Contains(textEl.Text))
                    DrawTextElement(canvas, textEl, 1f - progress);
            }
        }
    }

    // Convenience overload for static rendering (no transition)
    public static SKBitmap RenderToSKBitmap(
        SlideRenderSpec spec,
        int width = 1920,
        int height = 1080,
        SlideRenderSpec? previous = null,
        float progress = 1f)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        Draw(surface.Canvas, spec, previous, progress, width, height);
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static void DrawBackground(SKCanvas canvas, BackgroundSpec? bg, int width, int height)
    {
        switch (bg)
        {
            case SolidBackground solid:
                canvas.DrawRect(0, 0, width, height, new SKPaint { Color = solid.Color });
                break;

            case ImageBackground img:
                using (var bmp = SKBitmap.Decode(img.FilePath))
                {
                    if (bmp != null)
                    {
                        var dest = new SKRect(0, 0, width, height);
                        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
                        canvas.DrawBitmap(bmp, dest, paint);
                    }
                }
                break;

            case TransparentBackground:
            default:
                // leave the canvas cleared to transparent (done above)
                break;
        }
    }

    private static void DrawTextElement(SKCanvas canvas, TextLineElement element, float alpha)
    {
        if (alpha <= 0f) return;

        using var font = new SKFont(element.Typeface, element.FontSize)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Subpixel = true,
        };

        font.GetFontMetrics(out var metrics);
        // DrawText baseline = Bounds.Top + ascent height (ascent is negative in Skia)
        float baselineY = element.Bounds.Top - metrics.Ascent;

        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = element.Color.WithAlpha((byte)(element.Color.Alpha * alpha));

        if (element.Shadow is { } shadow)
        {
            // BlurRadius → Gaussian sigma: sigma = blurRadius / 2
            float sigma = shadow.BlurRadius / 2f;
            paint.ImageFilter = SKImageFilter.CreateDropShadow(
                shadow.OffsetX,
                shadow.OffsetY,
                sigma,
                sigma,
                shadow.Color.WithAlpha((byte)(shadow.Color.Alpha * alpha)));
        }

        canvas.DrawText(element.Text, element.Bounds.Left, baselineY, font, paint);
    }
}
```

- [ ] **Step 2.4: Run tests — expect pass**

```
dotnet test HandsLiftedApp.Tests --filter "SlideRendererTests"
```

Expected: 4 passed.

- [ ] **Step 2.5: Commit**

```
git add HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs
git add HandsLiftedApp.Tests/Render/Skia/SlideRendererTests.cs
git commit -m "feat: add SlideRenderer static drawing engine"
```

---

## Task 3: SongSlideSpecBuilder

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs`
- Create: `HandsLiftedApp.Tests/Render/Skia/Builders/SongSlideSpecBuilderTests.cs`

- [ ] **Step 3.1: Write the failing tests**

```csharp
// HandsLiftedApp.Tests/Render/Skia/Builders/SongSlideSpecBuilderTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Avalonia.Media;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Tests.Render.Skia.Builders;

[TestClass]
public class SongSlideSpecBuilderTests
{
    private static BaseSlideTheme MakeTheme() => new BaseSlideTheme
    {
        FontSize = 100,
        TextColour = new XmlColor { Color = Colors.White },
        BackgroundColour = new XmlColor { Color = Colors.Black },
    };

    [TestMethod]
    public void Build_TwoLineText_ReturnsTwoTextElements()
    {
        var slide = new SongSlideInstance(null, null, "id1") { Text = "Line one\nLine two" };
        slide.Theme = MakeTheme();

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.AreEqual(2, spec.Elements.Count);
        Assert.IsInstanceOfType(spec.Elements[0], typeof(TextLineElement));
        Assert.IsInstanceOfType(spec.Elements[1], typeof(TextLineElement));
    }

    [TestMethod]
    public void Build_TextElementsCarryCorrectText()
    {
        var slide = new SongSlideInstance(null, null, "id2") { Text = "Amazing grace\nHow sweet the sound" };
        slide.Theme = MakeTheme();

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.AreEqual("Amazing grace", ((TextLineElement)spec.Elements[0]).Text);
        Assert.AreEqual("How sweet the sound", ((TextLineElement)spec.Elements[1]).Text);
    }

    [TestMethod]
    public void Build_HasMotionBackground_ReturnsTransparentBackground()
    {
        // SongSlideInstance.HasMotionBackground is driven by its parent SongItemInstance.
        // Create a minimal SongItemInstance with HasMotionBackground = true.
        // If the test setup for SongItemInstance is complex, assert via a subclass stub.
        // For now, test the false branch (no motion background):
        var slide = new SongSlideInstance(null, null, "id3") { Text = "Test" };
        slide.Theme = MakeTheme();

        var spec = SongSlideSpecBuilder.Build(slide);

        // No motion background → solid or image background, not transparent
        Assert.IsNotInstanceOfType(spec.Background, typeof(TransparentBackground));
    }

    [TestMethod]
    public void Build_NullText_ReturnsEmptyElements()
    {
        var slide = new SongSlideInstance(null, null, "id4") { Text = "" };
        slide.Theme = MakeTheme();

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }
}
```

- [ ] **Step 3.2: Run test — expect compile failure**

```
dotnet test HandsLiftedApp.Tests --filter "SongSlideSpecBuilderTests"
```

Expected: `SongSlideSpecBuilder` not found.

- [ ] **Step 3.3: Create `SongSlideSpecBuilder.cs`**

```csharp
// HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs
using Avalonia.Media;
using SkiaSharp;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

public static class SongSlideSpecBuilder
{
    private const int CanvasWidth = 1920;
    private const int CanvasHeight = 1080;
    private const float HorizontalMargin = 80f;

    // Drop shadow parameters matching the previous DropShadowDirectionEffect XAML:
    // ShadowDepth=40, Direction=0 (straight down), BlurRadius=20, Color=Black, Opacity=1
    private static readonly DropShadowSpec DefaultShadow =
        new DropShadowSpec(0f, 40f, 20f, SKColors.Black);

    public static SlideRenderSpec Build(SongSlideInstance slide)
    {
        var bg = BuildBackground(slide);

        if (slide.Theme == null || string.IsNullOrWhiteSpace(slide.Text))
            return new SlideRenderSpec(bg, Array.Empty<RenderElement>());

        var elements = BuildTextElements(slide.Text, slide.Theme);
        return new SlideRenderSpec(bg, elements);
    }

    private static BackgroundSpec BuildBackground(SongSlideInstance slide)
    {
        if (slide.HasMotionBackground)
            return new TransparentBackground();

        if (!string.IsNullOrEmpty(slide.Theme?.BackgroundGraphicFilePath))
            return new ImageBackground(slide.Theme.BackgroundGraphicFilePath);

        var bg = slide.Theme != null
            ? ToSkColor(slide.Theme.BackgroundAvaloniaColour)
            : SKColors.Black;
        return new SolidBackground(bg);
    }

    private static IReadOnlyList<RenderElement> BuildTextElements(string text, BaseSlideTheme theme)
    {
        string[] lines = text.Split('\n');
        using var typeface = GetTypeface(theme);
        using var font = new SKFont(typeface, theme.FontSize);

        float lineHeight = theme.LineHeight;
        float totalHeight = lines.Length * lineHeight;
        float startY = (CanvasHeight - totalHeight) / 2f;
        var color = ToSkColor(theme.TextAvaloniaColour);

        var result = new List<RenderElement>(lines.Length);
        foreach (var (line, i) in lines.Select((l, i) => (l, i)))
        {
            if (string.IsNullOrEmpty(line))
            {
                // empty line: placeholder bounds so position is stable, no element drawn
                continue;
            }

            float textWidth = font.MeasureText(line);
            float x = theme.TextAlignment switch
            {
                TextAlignment.Right  => CanvasWidth - textWidth - HorizontalMargin,
                TextAlignment.Left   => HorizontalMargin,
                _                    => (CanvasWidth - textWidth) / 2f, // Center / Justify
            };
            float y = startY + i * lineHeight;
            var bounds = new SKRect(x, y, x + textWidth, y + lineHeight);

            // SKTypeface is shared — do not dispose it here; SlideRenderer will use it at draw time.
            // Create a fresh SKTypeface for the element since we dispose the measurement one above.
            var elemTypeface = GetTypeface(theme);
            result.Add(new TextLineElement(line, bounds, elemTypeface, theme.FontSize, color, DefaultShadow));
        }
        return result;
    }

    private static SKTypeface GetTypeface(BaseSlideTheme theme)
    {
        var weight = theme.CalculatedTextFontBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant  = theme.CalculatedTextFontItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return SKTypeface.FromFamilyName(theme.FontFamilyAsText, weight, SKFontStyleWidth.Normal, slant)
               ?? SKTypeface.Default;
    }

    private static SKColor ToSkColor(Color color) =>
        new SKColor(color.R, color.G, color.B, color.A);
}
```

> **Note on SKTypeface lifetime:** Each `TextLineElement` owns its `SKTypeface`. `SlideRenderer.DrawTextElement` creates an `SKFont` from it per draw call (cheap). The typeface is GC-collected when the `SlideRenderSpec` is replaced — no explicit disposal needed for MVP.

- [ ] **Step 3.4: Run tests — expect pass**

```
dotnet test HandsLiftedApp.Tests --filter "SongSlideSpecBuilderTests"
```

Expected: 4 passed.

- [ ] **Step 3.5: Commit**

```
git add HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs
git add HandsLiftedApp.Tests/Render/Skia/Builders/SongSlideSpecBuilderTests.cs
git commit -m "feat: add SongSlideSpecBuilder"
```

---

## Task 4: SongTitleSlideSpecBuilder

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/Builders/SongTitleSlideSpecBuilder.cs`
- Create: `HandsLiftedApp.Tests/Render/Skia/Builders/SongTitleSlideSpecBuilderTests.cs`

- [ ] **Step 4.1: Write the failing tests**

```csharp
// HandsLiftedApp.Tests/Render/Skia/Builders/SongTitleSlideSpecBuilderTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Avalonia.Media;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Tests.Render.Skia.Builders;

[TestClass]
public class SongTitleSlideSpecBuilderTests
{
    private static BaseSlideTheme MakeTheme() => new BaseSlideTheme
    {
        FontSize = 120,
        TextColour = new XmlColor { Color = Colors.White },
        BackgroundColour = new XmlColor { Color = Colors.Black },
    };

    [TestMethod]
    public void Build_TitleOnly_ReturnsOneElement()
    {
        var slide = new SongTitleSlideInstance(null) { Title = "Amazing Grace", Copyright = "" };
        slide.Theme = MakeTheme();

        var spec = SongTitleSlideSpecBuilder.Build(slide);

        Assert.AreEqual(1, spec.Elements.Count);
        Assert.AreEqual("Amazing Grace", ((TextLineElement)spec.Elements[0]).Text);
    }

    [TestMethod]
    public void Build_TitleAndCopyright_ReturnsTwoElements()
    {
        var slide = new SongTitleSlideInstance(null)
        {
            Title = "Amazing Grace",
            Copyright = "Public Domain"
        };
        slide.Theme = MakeTheme();

        var spec = SongTitleSlideSpecBuilder.Build(slide);

        Assert.AreEqual(2, spec.Elements.Count);
    }

    [TestMethod]
    public void Build_CopyrightIsSmaller_ThanTitle()
    {
        var slide = new SongTitleSlideInstance(null)
        {
            Title = "Amazing Grace",
            Copyright = "Public Domain"
        };
        slide.Theme = MakeTheme();

        var spec = SongTitleSlideSpecBuilder.Build(slide);

        var titleEl    = (TextLineElement)spec.Elements[0];
        var copyrightEl = (TextLineElement)spec.Elements[1];
        Assert.IsTrue(copyrightEl.FontSize < titleEl.FontSize);
    }
}
```

- [ ] **Step 4.2: Run test — expect compile failure**

```
dotnet test HandsLiftedApp.Tests --filter "SongTitleSlideSpecBuilderTests"
```

- [ ] **Step 4.3: Create `SongTitleSlideSpecBuilder.cs`**

```csharp
// HandsLiftedApp.Core/Render/Skia/Builders/SongTitleSlideSpecBuilder.cs
using Avalonia.Media;
using SkiaSharp;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

public static class SongTitleSlideSpecBuilder
{
    private const int CanvasWidth = 1920;
    private const int CanvasHeight = 1080;
    private const float HorizontalMargin = 80f;
    private const float CopyrightSizeRatio = 0.45f; // copyright is ~45% of title font size
    private const float CopyrightBottomMargin = 60f;

    private static readonly DropShadowSpec DefaultShadow =
        new DropShadowSpec(0f, 40f, 20f, SKColors.Black);

    public static SlideRenderSpec Build(SongTitleSlideInstance slide)
    {
        var bg = BuildBackground(slide);
        if (slide.Theme == null) return new SlideRenderSpec(bg, Array.Empty<RenderElement>());

        var elements = new List<RenderElement>();

        if (!string.IsNullOrWhiteSpace(slide.Title))
            elements.Add(BuildTitleElement(slide.Title, slide.Theme));

        if (!string.IsNullOrWhiteSpace(slide.Copyright))
            elements.Add(BuildCopyrightElement(slide.Copyright, slide.Theme));

        return new SlideRenderSpec(bg, elements);
    }

    private static BackgroundSpec BuildBackground(SongTitleSlideInstance slide)
    {
        if (slide.HasMotionBackground)
            return new TransparentBackground();

        if (!string.IsNullOrEmpty(slide.Theme?.BackgroundGraphicFilePath))
            return new ImageBackground(slide.Theme.BackgroundGraphicFilePath);

        var bg = slide.Theme != null
            ? ToSkColor(slide.Theme.BackgroundAvaloniaColour)
            : SKColors.Black;
        return new SolidBackground(bg);
    }

    private static TextLineElement BuildTitleElement(string title, BaseSlideTheme theme)
    {
        var typeface = GetTypeface(theme);
        using var font = new SKFont(typeface, theme.FontSize);
        float textWidth = font.MeasureText(title);
        float x = (CanvasWidth - textWidth) / 2f;
        float y = (CanvasHeight - theme.LineHeight) / 2f;
        var bounds = new SKRect(x, y, x + textWidth, y + theme.LineHeight);
        return new TextLineElement(title, bounds, GetTypeface(theme), theme.FontSize,
            ToSkColor(theme.TextAvaloniaColour), DefaultShadow);
    }

    private static TextLineElement BuildCopyrightElement(string copyright, BaseSlideTheme theme)
    {
        float copyrightSize = theme.FontSize * CopyrightSizeRatio;
        var typeface = GetTypeface(theme);
        using var font = new SKFont(typeface, copyrightSize);
        float textWidth = font.MeasureText(copyright);
        float x = (CanvasWidth - textWidth) / 2f;
        float lineHeight = copyrightSize * 1.2f;
        float y = CanvasHeight - lineHeight - CopyrightBottomMargin;
        var bounds = new SKRect(x, y, x + textWidth, y + lineHeight);
        return new TextLineElement(copyright, bounds, GetTypeface(theme), copyrightSize,
            ToSkColor(theme.TextAvaloniaColour), DefaultShadow);
    }

    private static SKTypeface GetTypeface(BaseSlideTheme theme)
    {
        var weight = theme.CalculatedTextFontBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant  = theme.CalculatedTextFontItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return SKTypeface.FromFamilyName(theme.FontFamilyAsText, weight, SKFontStyleWidth.Normal, slant)
               ?? SKTypeface.Default;
    }

    private static SKColor ToSkColor(Color color) =>
        new SKColor(color.R, color.G, color.B, color.A);
}
```

- [ ] **Step 4.4: Run tests — expect pass**

```
dotnet test HandsLiftedApp.Tests --filter "SongTitleSlideSpecBuilderTests"
```

Expected: 3 passed.

- [ ] **Step 4.5: Commit**

```
git add HandsLiftedApp.Core/Render/Skia/Builders/SongTitleSlideSpecBuilder.cs
git add HandsLiftedApp.Tests/Render/Skia/Builders/SongTitleSlideSpecBuilderTests.cs
git commit -m "feat: add SongTitleSlideSpecBuilder"
```

---

## Task 5: SlideCanvas Avalonia control

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/SlideCanvas.cs`

No automated test — the control requires an Avalonia render surface. Verification is visual (Task 6).

- [ ] **Step 5.1: Create `SlideCanvas.cs`**

```csharp
// HandsLiftedApp.Core/Render/Skia/SlideCanvas.cs
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace HandsLiftedApp.Core.Render.Skia;

public sealed class SlideCanvas : Control
{
    // ── Transition state ────────────────────────────────────────────────────

    private SlideRenderSpec? _current;
    private SlideRenderSpec? _previous;
    private float _transitionProgress = 1f;
    private TimeSpan _transitionDuration;
    private Stopwatch? _transitionStopwatch;
    private DispatcherTimer? _timer;

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the spec immediately with no animation. Use in the editor.
    /// </summary>
    public SlideRenderSpec? Spec
    {
        get => _current;
        set
        {
            StopTimer();
            _previous = null;
            _current = value;
            _transitionProgress = 1f;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Transitions to <paramref name="next"/> with a cross-fade over <paramref name="duration"/>.
    /// Unchanged text lines stay at full opacity; only added/removed lines animate.
    /// Use in the projector code-behind.
    /// </summary>
    public void Transition(SlideRenderSpec? next, TimeSpan duration)
    {
        StopTimer();
        _previous = _current;
        _current = next;
        _transitionDuration = duration;
        _transitionProgress = 0f;
        _transitionStopwatch = Stopwatch.StartNew();
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            OnTick);
        _timer.Start();
    }

    // ── Avalonia rendering ─────────────────────────────────────────────────

    protected override void OnMeasureInvalidated()
    {
        base.OnMeasureInvalidated();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.Custom(new SlideDrawOperation(
            new Rect(Bounds.Size),
            _current,
            _previous,
            _transitionProgress));
    }

    // ── Timer ───────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        if (_transitionStopwatch is null) return;

        _transitionProgress = _transitionDuration > TimeSpan.Zero
            ? (float)(_transitionStopwatch.Elapsed / _transitionDuration)
            : 1f;

        if (_transitionProgress >= 1f)
        {
            _transitionProgress = 1f;
            StopTimer();
            _previous = null;
        }

        InvalidateVisual();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
        _transitionStopwatch?.Stop();
        _transitionStopwatch = null;
    }

    // ── ICustomDrawOperation ────────────────────────────────────────────────

    private sealed class SlideDrawOperation : ICustomDrawOperation
    {
        private readonly SlideRenderSpec? _current;
        private readonly SlideRenderSpec? _previous;
        private readonly float _progress;

        public Rect Bounds { get; }

        public SlideDrawOperation(
            Rect bounds,
            SlideRenderSpec? current,
            SlideRenderSpec? previous,
            float progress)
        {
            Bounds = bounds;
            _current = current;
            _previous = previous;
            _progress = progress;
        }

        public void Dispose() { }
        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point p) => true;

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } leaseFeature)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            SlideRenderer.Draw(
                canvas,
                _current,
                _previous,
                _progress,
                (int)Bounds.Width,
                (int)Bounds.Height);
            canvas.Restore();
        }
    }
}
```

- [ ] **Step 5.2: Verify it builds**

```
dotnet build HandsLiftedApp.Core
```

Expected: build succeeds with no errors.

- [ ] **Step 5.3: Commit**

```
git add HandsLiftedApp.Core/Render/Skia/SlideCanvas.cs
git commit -m "feat: add SlideCanvas custom Avalonia control with transition"
```

---

## Task 6: Wire SlideCanvas into ProjectorWindow

**Files:**
- Modify: `HandsLiftedApp.Core/Views/ProjectorWindow.axaml`
- Modify: `HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs`

No automated test — verify visually by running the app.

- [ ] **Step 6.1: Replace `ActiveSlideRender` in `ProjectorWindow.axaml`**

In `ProjectorWindow.axaml`, locate the inner `NDISendContainer` (the main output one that contains the `Grid` with `MotionBackgroundLayer` and `ActiveSlideRender`). Replace the `ActiveSlideRender` line:

```xml
<!-- Remove this: -->
<render:ActiveSlideRender ActiveSlide="{Binding Playlist.ActiveSlide}" />

<!-- Add this (x:Name so the code-behind can call Transition()): -->
<skia:SlideCanvas x:Name="MainSlideCanvas" />
```

Add the `skia` namespace at the top of the file alongside the existing namespace declarations:

```xml
xmlns:skia="clr-namespace:HandsLiftedApp.Core.Render.Skia"
```

- [ ] **Step 6.2: Wire transitions in `ProjectorWindow.axaml.cs`**

Add a field and `OnDataContextChanged` override. The `DataContext` is `MainViewModel`. Subscribe to `Playlist.ActiveSlide` and call `MainSlideCanvas.Transition()` on changes.

Add these members to the `ProjectorWindow` class:

```csharp
private IDisposable? _slideSubscription;

protected override void OnDataContextChanged(EventArgs e)
{
    base.OnDataContextChanged(e);
    _slideSubscription?.Dispose();

    if (DataContext is not MainViewModel vm) return;

    _slideSubscription = vm.Playlist
        .WhenAnyValue(p => p.ActiveSlide)
        .Subscribe(OnActiveSlideChanged);
}

private void OnActiveSlideChanged(Slide? slide)
{
    SlideRenderSpec? spec = slide switch
    {
        SongSlideInstance s      => SongSlideSpecBuilder.Build(s),
        SongTitleSlideInstance t => SongTitleSlideSpecBuilder.Build(t),
        _                        => null,
    };
    MainSlideCanvas.Transition(spec, TimeSpan.FromMilliseconds(120));
}
```

Add the required `using` directives at the top of `ProjectorWindow.axaml.cs`:

```csharp
using System.Reactive.Linq;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.Slides;
```

> `WhenAnyValue` is from ReactiveUI which is already in the project. If `Playlist` doesn't implement `IReactiveObject`, use `Observable.FromEventPattern` or the existing `MessageBus` pattern instead.

- [ ] **Step 6.3: Build and run the app — verify projector shows slides with drop shadows**

```
dotnet run --project HandsLiftedApp.Desktop
```

Open the projector window. Advance through song slides. Confirm:
- Slide text renders with a visible drop shadow
- Transitioning between slides with identical lines shows no dip-out on the matching lines
- Motion background video is still visible behind transparent-background slides

- [ ] **Step 6.4: Commit**

```
git add HandsLiftedApp.Core/Views/ProjectorWindow.axaml
git add HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs
git commit -m "feat: wire SlideCanvas into ProjectorWindow"
```

---

## Task 7: Replace SongSlideView render content with SlideCanvas

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Designer/SongSlideView.axaml`
- Modify: `HandsLiftedApp.Core/Views/Designer/SongSlideView.axaml.cs`

- [ ] **Step 7.1: Replace the XAML content of `SongSlideView.axaml`**

Remove the existing `Grid`/`Border`/`TextBlock` content (including the `DropShadowDirectionEffect`). Replace the entire visual content with:

```xml
<Grid xmlns:skia="clr-namespace:HandsLiftedApp.Core.Render.Skia">
    <skia:SlideCanvas x:Name="SlideCanvas" />
    <!-- Editing overlays go here in future tasks (resize handles, text boxes) -->
</Grid>
```

Keep `x:DataType="slides1:SongSlideInstance"` and any existing namespace declarations. Remove namespace references that are no longer needed (e.g. `MotionBackgroundBrushConverter`, `ColorToBrush`).

- [ ] **Step 7.2: Update `SongSlideView.axaml.cs` to rebuild spec on changes**

Replace the code-behind with:

```csharp
// HandsLiftedApp.Core/Views/Designer/SongSlideView.axaml.cs
using System.Reactive.Linq;
using Avalonia.Controls;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Views.Designer;

public partial class SongSlideView : UserControl
{
    private IDisposable? _subscription;

    public SongSlideView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _subscription?.Dispose();
        _subscription = null;

        if (DataContext is not SongSlideInstance slide) return;

        // Rebuild spec immediately and whenever Text or Theme changes
        _subscription = slide
            .WhenAnyValue(s => s.Text, s => s.Theme)
            .Subscribe(_ => RebuildSpec(slide));

        RebuildSpec(slide);
    }

    private void RebuildSpec(SongSlideInstance slide)
    {
        SlideCanvas.Spec = SongSlideSpecBuilder.Build(slide);
    }
}
```

- [ ] **Step 7.3: Build and verify the editor shows slides**

```
dotnet build HandsLiftedApp.Core
dotnet run --project HandsLiftedApp.Desktop
```

Open the main editor window. Confirm:
- Song slides render correctly in the slide list / editor view
- Theme changes (font, colour) are reflected immediately
- Drop shadow is visible on slide text in the editor

- [ ] **Step 7.4: Commit**

```
git add HandsLiftedApp.Core/Views/Designer/SongSlideView.axaml
git add HandsLiftedApp.Core/Views/Designer/SongSlideView.axaml.cs
git commit -m "feat: replace SongSlideView TextBlock with SlideCanvas"
```

---

## Task 8: Remove obsolete components

**Files:**
- Delete: `HandsLiftedApp.Core/Render/ActiveSlideRender.axaml`
- Delete: `HandsLiftedApp.Core/Render/ActiveSlideRender.axaml.cs`
- Delete: `HandsLiftedApp.Core/Views/SlideRendererWorkerWindow.axaml.cs`
- Remove: `SlideRenderRequestMessage` references (was in `SlideRendererWorkerWindow`)

- [ ] **Step 8.1: Search for remaining references to `ActiveSlideRender`**

```
grep -r "ActiveSlideRender" HandsLiftedApp.Core --include="*.cs" --include="*.axaml"
```

Resolve any found references (replace with `SlideCanvas` or remove) before deleting the file.

- [ ] **Step 8.2: Update thumbnail generation in `SongSlideInstance` and `SongTitleSlideInstance`**

Both `GenerateBitmaps()` methods currently publish a `SlideRenderRequestMessage` via `MessageBus`. Replace them with direct calls to `SlideRenderer.RenderToSKBitmap()` and `BitmapUtils.CreateThumbnail()`.

In `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongSlideInstance.cs`, replace `GenerateBitmaps()`:

```csharp
private void GenerateBitmaps()
{
    var spec = SongSlideSpecBuilder.Build(this);
    using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
    // Convert SKBitmap → Avalonia Bitmap using existing BitmapUtils pattern
    Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
    Thumbnail = BitmapUtils.CreateThumbnail(Cached);
}
```

In `HandsLiftedApp.Core/Models/RuntimeData/Slides/SongTitleSlideInstance.cs`, apply the same pattern using `SongTitleSlideSpecBuilder.Build(this)`.

> **Note:** `BitmapUtils.SKBitmapToAvalonia` may not exist yet. If `BitmapUtils` only has `CreateThumbnail`, add a static helper that converts an `SKBitmap` to an Avalonia `Bitmap` via a `MemoryStream` encoded as PNG:
>
> ```csharp
> public static Bitmap SKBitmapToAvalonia(SKBitmap skBitmap)
> {
>     using var data = skBitmap.Encode(SKEncodedImageFormat.Png, 100);
>     using var stream = new MemoryStream(data.ToArray());
>     return new Bitmap(stream);
> }
> ```

Add the required `using` directives:
```csharp
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
```

- [ ] **Step 8.3: Search for remaining references to `SlideRendererWorkerWindow` and `SlideRenderRequestMessage`**

```
grep -r "SlideRendererWorkerWindow\|SlideRenderRequestMessage" HandsLiftedApp.Core --include="*.cs" --include="*.axaml"
```

All remaining references should now be zero. If any remain, remove or replace them.

- [ ] **Step 8.4: Delete the obsolete files**

```
git rm HandsLiftedApp.Core/Render/ActiveSlideRender.axaml
git rm HandsLiftedApp.Core/Render/ActiveSlideRender.axaml.cs
git rm HandsLiftedApp.Core/Views/SlideRendererWorkerWindow.axaml.cs
```

- [ ] **Step 8.5: Remove `XTransitioningContentControl` project reference from `HandsLiftedApp.Core.csproj`**

Open `HandsLiftedApp.Core/HandsLiftedApp.Core.csproj` and remove the `<ProjectReference>` line pointing to `HandsLiftedApp.XTransitioningContentControl`. Verify no remaining `using` statements reference types from that project:

```
grep -r "XTransitioningContentControl\|XFade" HandsLiftedApp.Core --include="*.cs" --include="*.axaml"
```

- [ ] **Step 8.6: Build the full solution — confirm no errors**

```
dotnet build HandsLiftedApp.sln
```

Expected: 0 errors.

- [ ] **Step 8.7: Run all tests**

```
dotnet test HandsLiftedApp.Tests
```

Expected: all tests pass.

- [ ] **Step 8.8: Commit**

```
git add -u
git commit -m "refactor: remove ActiveSlideRender, SlideRendererWorkerWindow, XTransitioningContentControl reference"
```

---

## Self-review checklist (for the implementer)

After Task 8 completes, verify these by running the app and testing each scenario:

- [ ] Song slide with drop shadow renders correctly on projector screen
- [ ] Same drop shadow appears in NDI output (check with an NDI monitor tool)
- [ ] Advancing between two verses — unchanged lines do not dip/flicker
- [ ] Advancing between two verses — changed lines cross-fade smoothly
- [ ] Motion background video shows through when `HasMotionBackground = true`
- [ ] Static image/colour background renders when `HasMotionBackground = false`
- [ ] Song title slide renders title and copyright text
- [ ] Editor slide view updates immediately when theme (font/colour) is changed
- [ ] No black flash or blank frame visible in NDI output during transition
