# Scripture Rendering + Projection Wiring (Phase 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `ScriptureSlideInstance` actually render — turning its `Text`/`Theme` into a Skia bitmap the same way `SongSlideInstance` does — and wire that into both live-preview (`LivePane`) and actual projector output (`ProjectorWindow`), so a generated scripture slide is visible on screen for the first time.

**Architecture:** Adds a `Theme` property to `ScriptureSlideInstance` (Task 1), a `ScriptureSlideSpecBuilder` mirroring `SongSlideSpecBuilder` almost verbatim — same word-wrap/autofit/drop-shadow logic, since none of that is Song-specific (Task 2), then wires `ScriptureSlideInstance` into the existing reactive self-render pattern (`IRenderable`, `Render()`, debounced `RequestRender()`) that `SongSlideInstance` already uses (Task 3), then adds the two slide-type switch arms and one thumbnail `DataTemplate` that make a `ScriptureSlideInstance` actually reach the screen (Task 4).

**Tech Stack:** .NET 8, MSTest, SkiaSharp (via the existing `SlideRenderer`/`SlideRenderSpec` — untouched, no changes to that engine), ReactiveUI, Avalonia XAML.

## Global Constraints

- net8.0, MSTest, matches Phase 1/2.
- No changes to `SlideRenderer`, `SlideRenderSpec`, or any Song-type file — this phase only adds new Scripture-side files plus 3 small, additive edits to shared switch statements/templates that already have a slot for each slide type.
- `ScriptureItem` has no `Design`/theme-selection property (confirmed absent, Phase 2). This phase does NOT add one — `ScriptureSlideInstance.Theme` always resolves to `Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme()` (the same fallback `SongSlideInstance.ResolveTheme` uses when a song has no per-item design override, i.e. `Guid.Empty`). Adding a real per-item theme picker is a future enhancement, not required for scripture slides to render correctly with the app's default theme.
- **Test-coverage precedent, deliberate:** the existing `SongSlideInstance.Render()` (the reactive, `Dispatcher.UIThread`-posting method that actually populates `Cached`/`Thumbnail`) has **no unit test anywhere in this codebase** — it can't be tested without a running Avalonia dispatcher, which a plain MSTest host doesn't provide. `ScriptureSlideInstance.Render()` follows the identical pattern and is left equally untested at that layer, matching established precedent; only the pure `ScriptureSlideSpecBuilder` (Task 2) and shallow construction checks (Task 1, Task 3) get automated tests.
- **Task 4 (XAML/UI wiring) cannot be automatically verified in this environment** — there is no existing automated test for the analogous Song switch-statement arms or `DataTemplate` entries anywhere in this codebase, and a desktop Avalonia app can't be visually inspected by a subagent here. Task 4's edits are small, mechanical, and mirror an existing pattern exactly (same shape as the `SongSlideInstance`/`SongTitleSlideInstance` entries already present); verification is a correctness review of the diff, not a runtime check. Actually seeing a scripture slide render on screen requires the user to run the app themselves after this phase lands.

---

### Task 1: Add `Theme` to `ScriptureSlideInstance`

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs`
- Modify: `HandsLiftedApp.Tests/Models/RuntimeData/Slides/ScriptureSlideInstanceTests.cs`

**Interfaces:**
- Produces: `ScriptureSlideInstance.Theme` (settable `BaseSlideTheme?`, defaults to `Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme()` at construction). Task 2 (`ScriptureSlideSpecBuilder`) reads this directly.

**Why split from Task 3 (Render/IRenderable):** `ScriptureSlideSpecBuilder.Build` (Task 2) needs to compile against `ScriptureSlideInstance.Theme`, and `ScriptureSlideInstance.Render()` (Task 3) needs `ScriptureSlideSpecBuilder` to already exist to call it — a genuine circular dependency between "the slide needs a theme to render" and "the spec builder needs the slide's theme property to exist." Splitting `Theme` out into its own task first breaks the cycle cleanly.

- [ ] **Step 1: Write the failing test**

Add to `HandsLiftedApp.Tests/Models/RuntimeData/Slides/ScriptureSlideInstanceTests.cs` (existing file from Phase 2 — add this test alongside the existing 4):

```csharp
    [TestMethod]
    public void Theme_DefaultsToNonNullAfterConstruction()
    {
        var slide = new ScriptureSlideInstance(null, "1:1");

        Assert.IsNotNull(slide.Theme);
    }

    [TestMethod]
    public void Theme_IsSettable()
    {
        var slide = new ScriptureSlideInstance(null, "1:1");
        var customTheme = new HandsLiftedApp.Data.SlideTheme.BaseSlideTheme { FontSize = 42 };

        slide.Theme = customTheme;

        Assert.AreSame(customTheme, slide.Theme);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureSlideInstanceTests"`
Expected: FAIL — compile error, `Theme` doesn't exist on `ScriptureSlideInstance` yet.

- [ ] **Step 3: Add the `Theme` property**

Modify `HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs` — the current file (from Phase 2) is:

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

Replace it with:

```csharp
using Avalonia.Media.Imaging;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    // IRenderable / self-rendering added in Phase 3 Task 3, once ScriptureSlideSpecBuilder
    // (Task 2) exists for Render() to call.
    public class ScriptureSlideInstance : ScriptureSlide, ISlideInstance
    {
        public ScriptureSlideInstance(ScriptureItem? parentScriptureItem, string id, string? text = null, string? label = null)
            : base(parentScriptureItem, id)
        {
            if (text != null) Text = text;
            if (label != null) Label = label;

            // No per-item Design/theme-selection concept exists yet for scripture items
            // (unlike SongItem.Design) — every scripture slide uses the app's default theme.
            Theme = Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
        }

        private BaseSlideTheme? _theme;
        public BaseSlideTheme? Theme
        {
            get => _theme;
            set => this.RaiseAndSetIfChanged(ref _theme, value);
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
Expected: PASS (6 tests — 4 from Phase 2 + 2 new).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 111 + 2 = 113, no regressions.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs HandsLiftedApp.Tests/Models/RuntimeData/Slides/ScriptureSlideInstanceTests.cs
git commit -m "feat: add Theme property to ScriptureSlideInstance, defaulting to app default theme"
```

---

### Task 2: `ScriptureSlideSpecBuilder`

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/Builders/ScriptureSlideSpecBuilder.cs`
- Test: `HandsLiftedApp.Tests/Render/Skia/Builders/ScriptureSlideSpecBuilderTests.cs`

**Interfaces:**
- Consumes: `ScriptureSlideInstance.Text`/`.Theme` (Task 1); `SlideRenderSpec`/`RenderElement`/`TextLineElement`/`BackgroundSpec`/`SolidBackground`/`ImageBackground`/`DropShadowSpec` (existing, `HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs` — untouched); `BaseSlideTheme` (existing, `HandsLiftedApp.Data/SlideTheme/`).
- Produces: `public static class ScriptureSlideSpecBuilder { public static SlideRenderSpec Build(ScriptureSlideInstance slide) }` — Task 3's `ScriptureSlideInstance.Render()` calls this directly; Task 4's `LivePane`/`ProjectorWindow` switch arms call it too.

**Design note:** this is a near-verbatim copy of `SongSlideSpecBuilder.cs` (`HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs`) — word-wrap, autofit, drop-shadow, and typeface logic all operate on plain `string text, BaseSlideTheme theme`, with nothing Song-specific in them. The only real difference is `BuildBackground`: `SongSlideSpecBuilder` checks `slide.HasMotionBackground` first (a Song-only concept — motion background video), which `ScriptureSlideInstance` has no equivalent of, so that check is simply omitted here. No shared base class is introduced — `SongTitleSlideSpecBuilder` already duplicates this same logic independently rather than sharing a helper, so this follows the codebase's existing (if imperfect) convention rather than introducing a new abstraction pattern unprompted.

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/Render/Skia/Builders/ScriptureSlideSpecBuilderTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Avalonia.Media;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Tests.Render.Skia.Builders;

[TestClass]
public class ScriptureSlideSpecBuilderTests
{
    private static BaseSlideTheme MakeTheme() => new BaseSlideTheme
    {
        FontSize = 100,
        TextColour = Colors.White,
        BackgroundColour = Colors.Black,
    };

    [TestMethod]
    public void Build_TwoLineText_ReturnsTwoTextElements()
    {
        var slide = new ScriptureSlideInstance(null, "id1") { Text = "Line one\nLine two" };
        slide.Theme = MakeTheme();

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual(2, spec.Elements.Count);
        Assert.IsInstanceOfType(spec.Elements[0], typeof(TextLineElement));
        Assert.IsInstanceOfType(spec.Elements[1], typeof(TextLineElement));
    }

    [TestMethod]
    public void Build_TextElementsCarryCorrectText()
    {
        var slide = new ScriptureSlideInstance(null, "id2") { Text = "For God so loved the world\nthat He gave His one and only Son" };
        slide.Theme = MakeTheme();

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual("For God so loved the world", ((TextLineElement)spec.Elements[0]).Text);
        Assert.AreEqual("that He gave His one and only Son", ((TextLineElement)spec.Elements[1]).Text);
    }

    [TestMethod]
    public void Build_WithTheme_ReturnsSolidBackground()
    {
        var slide = new ScriptureSlideInstance(null, "id3") { Text = "Test" };
        slide.Theme = MakeTheme();

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
    }

    [TestMethod]
    public void Build_NoTheme_ReturnsEmptyElements()
    {
        var slide = new ScriptureSlideInstance(null, "id-no-theme") { Text = "Test" };
        slide.Theme = null;

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }

    [TestMethod]
    public void Build_EmptyText_ReturnsEmptyElements()
    {
        var slide = new ScriptureSlideInstance(null, "id4") { Text = "" };
        slide.Theme = MakeTheme();

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }

    [TestMethod]
    public void Build_WhitespaceOnlyText_ReturnsEmptyElements()
    {
        var slide = new ScriptureSlideInstance(null, "id5") { Text = "   " };
        slide.Theme = MakeTheme();

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual(0, spec.Elements.Count);
    }

    // ~55 chars: measured to exceed the 1760px width at FontSize=100 (forcing a
    // wrap without autofit) but comfortably fit at the 0.5-ratio floor of 50.
    private const string LongVerseLine = "For God so loved the world that He gave His only Son";

    [TestMethod]
    public void Build_AutofitEnabled_LongLineShrinksAndStaysOnOneLine()
    {
        var slide = new ScriptureSlideInstance(null, "id-autofit-1") { Text = LongVerseLine };
        slide.Theme = MakeTheme(); // AutofitEnabled defaults to true

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.AreEqual(1, spec.Elements.Count, "autofit should keep the raw line on one display line");
        var element = (TextLineElement)spec.Elements[0];
        Assert.IsTrue(element.FontSize < 100, "font should have shrunk below the theme size");
        Assert.IsTrue(element.FontSize >= 50, "font should not shrink below the 0.5 ratio floor");
    }

    [TestMethod]
    public void Build_AutofitDisabled_LongLineWrapsAtFixedSize()
    {
        var slide = new ScriptureSlideInstance(null, "id-autofit-2") { Text = LongVerseLine };
        var theme = MakeTheme();
        theme.AutofitEnabled = false;
        slide.Theme = theme;

        var spec = ScriptureSlideSpecBuilder.Build(slide);

        Assert.IsTrue(spec.Elements.Count > 1, "without autofit the long line should word-wrap into multiple lines");
        foreach (var el in spec.Elements)
        {
            Assert.AreEqual(100f, ((TextLineElement)el).FontSize, "font size must stay fixed when autofit is disabled");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureSlideSpecBuilderTests"`
Expected: FAIL — compile error, `ScriptureSlideSpecBuilder` doesn't exist yet.

- [ ] **Step 3: Implement the spec builder**

`HandsLiftedApp.Core/Render/Skia/Builders/ScriptureSlideSpecBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using Avalonia.Media;
using SkiaSharp;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

public static class ScriptureSlideSpecBuilder
{
    private const int CanvasWidth = 1920;
    private const int CanvasHeight = 1080;
    private const float HorizontalMargin = 80f;
    private const float VerticalMargin = 80f;

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

        if (slide.Theme == null || string.IsNullOrWhiteSpace(slide.Text))
            return new SlideRenderSpec(bg, Array.Empty<RenderElement>());

        var elements = BuildTextElements(slide.Text, slide.Theme);
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

    private static IReadOnlyList<RenderElement> BuildTextElements(string text, BaseSlideTheme theme)
    {
        var rawLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        using var typeface = GetTypeface(theme);
        float maxWidth = CanvasWidth - 2 * HorizontalMargin;

        float effectiveFontSize = theme.FontSize;
        float effectiveLineHeight = theme.LineHeight;
        if (theme.AutofitEnabled)
        {
            float maxHeight = CanvasHeight - 2 * VerticalMargin;
            (effectiveFontSize, effectiveLineHeight) =
                ComputeAutofitSize(rawLines, theme, typeface, maxWidth, maxHeight);
        }

        using var measureFont = new SKFont(typeface, effectiveFontSize);
        using var measurePaint = new SKPaint(measureFont);

        var displayLines = new List<string>(rawLines.Length);
        foreach (var raw in rawLines)
        {
            if (measurePaint.MeasureText(raw) <= maxWidth)
            {
                displayLines.Add(raw);
                continue;
            }

            var words = raw.Split(' ');
            var current = new System.Text.StringBuilder();
            foreach (var word in words)
            {
                if (current.Length == 0)
                {
                    current.Append(word);
                }
                else
                {
                    string candidate = current + " " + word;
                    if (measurePaint.MeasureText(candidate) > maxWidth)
                    {
                        displayLines.Add(current.ToString());
                        current.Clear();
                        current.Append(word);
                    }
                    else
                    {
                        current.Clear();
                        current.Append(candidate);
                    }
                }
            }
            if (current.Length > 0)
                displayLines.Add(current.ToString());
        }

        float lineHeight = effectiveLineHeight;
        float totalHeight = displayLines.Count * lineHeight;
        float startY = (CanvasHeight - totalHeight) / 2f;
        var color = ToSkColor(theme.TextAvaloniaColour);

        var result = new List<RenderElement>(displayLines.Count);
        for (int i = 0; i < displayLines.Count; i++)
        {
            string line = displayLines[i];
            float textWidth = measurePaint.MeasureText(line);
            float x = theme.TextAlignment switch
            {
                TextAlignment.Right  => CanvasWidth - textWidth - HorizontalMargin,
                TextAlignment.Left   => HorizontalMargin,
                _                    => (CanvasWidth - textWidth) / 2f, // Center / Justify
            };
            float y = startY + i * lineHeight;
            var bounds = new SKRect(x, y, x + textWidth, y + lineHeight);

            var elemTypeface = GetTypeface(theme);
            result.Add(new TextLineElement(line, bounds, elemTypeface, effectiveFontSize, color, GetShadow(theme)));
        }
        return result;
    }

    private static (float FontSize, float LineHeight) ComputeAutofitSize(
        string[] rawLines, BaseSlideTheme theme, SKTypeface typeface, float maxWidth, float maxHeight)
    {
        float floor = theme.FontSize * (float)theme.AutofitMinFontSizeRatio;
        const float step = 4f;

        for (float candidate = theme.FontSize; candidate > floor; candidate -= step)
        {
            if (FitsAt(candidate))
                return (candidate, candidate * (float)theme.LineHeightEm);
        }

        return (floor, floor * (float)theme.LineHeightEm);

        bool FitsAt(float size)
        {
            float candidateLineHeight = size * (float)theme.LineHeightEm;
            if (rawLines.Length * candidateLineHeight > maxHeight)
                return false;

            using var font = new SKFont(typeface, size);
            using var paint = new SKPaint(font);
            foreach (var line in rawLines)
            {
                if (paint.MeasureText(line) > maxWidth)
                    return false;
            }
            return true;
        }
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

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureSlideSpecBuilderTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 113 + 8 = 121, no regressions.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Render/Skia/Builders/ScriptureSlideSpecBuilder.cs HandsLiftedApp.Tests/Render/Skia/Builders/ScriptureSlideSpecBuilderTests.cs
git commit -m "feat: add ScriptureSlideSpecBuilder (word-wrap, autofit, drop-shadow for verse text)"
```

---

### Task 3: Self-rendering wiring on `ScriptureSlideInstance`

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs`
- Modify: `HandsLiftedApp.Tests/Models/RuntimeData/Slides/ScriptureSlideInstanceTests.cs`

**Interfaces:**
- Consumes: `ScriptureSlideSpecBuilder.Build` (Task 2), `SlideRenderer.RenderToSKBitmap` (existing), `BitmapUtils.SKBitmapToAvalonia`/`.CreateThumbnail` (existing), `Globals.Instance.SlideRenderQueue` (existing), `IRenderable` (existing, `HandsLiftedApp.Core/Services/SlideRenderQueue.cs:12-15`).
- Produces: `ScriptureSlideInstance` now implements `IRenderable`; text/theme changes trigger a debounced re-render populating `Cached`/`Thumbnail`. Task 4's `LivePane`/`ProjectorWindow` wiring doesn't call anything new here directly (they call `ScriptureSlideSpecBuilder.Build` themselves for the *live preview transition*, same as Song does) — this task's payoff is the *thumbnail strip* updating automatically whenever verse text or theme changes.

**Design note — untestable-by-design, matches precedent:** see Global Constraints. This task adds `Render()`/`RequestRender()` mirroring `SongSlideInstance.cs:83-95`'s exact pattern (`Globals.Instance.SlideRenderQueue.Enqueue(this)` → threadpool → `Dispatcher.UIThread.Post` to set `Cached`/`Thumbnail`). No test exercises `Render()` itself — only that the class compiles as `IRenderable` and that constructing it doesn't throw.

- [ ] **Step 1: Write the failing test**

Add to `HandsLiftedApp.Tests/Models/RuntimeData/Slides/ScriptureSlideInstanceTests.cs`:

```csharp
    [TestMethod]
    public void ScriptureSlideInstance_ImplementsIRenderable()
    {
        var slide = new ScriptureSlideInstance(null, "1:1");

        Assert.IsInstanceOfType(slide, typeof(HandsLiftedApp.Core.Services.IRenderable));
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureSlideInstanceTests"`
Expected: FAIL — `ScriptureSlideInstance` does not implement `IRenderable` yet.

- [ ] **Step 3: Add `IRenderable`/`Render()`/reactive wiring**

Replace `HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs` (current state is Task 1's output) with:

```csharp
using Avalonia.Media.Imaging;
using DebounceThrottle;
using DynamicData.Binding;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace HandsLiftedApp.Data.Slides
{
    public class ScriptureSlideInstance : ScriptureSlide, ISlideInstance, IRenderable
    {
        private readonly DebounceDispatcher debounceDispatcher = new(200);

        public ScriptureSlideInstance(ScriptureItem? parentScriptureItem, string id, string? text = null, string? label = null)
            : base(parentScriptureItem, id)
        {
            if (text != null) Text = text;
            if (label != null) Label = label;

            // No per-item Design/theme-selection concept exists yet for scripture items
            // (unlike SongItem.Design) — every scripture slide uses the app's default theme.
            Theme = Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();

            this.WhenAnyValue(x => x.Theme)
                .Select(t => t?.WhenAnyPropertyChanged() ?? Observable.Never<BaseSlideTheme?>())
                .Switch()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => RequestRender());

            this.WhenAnyValue(x => x.Text)
                .Skip(1)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => RequestRender());
        }

        private void RequestRender()
            => debounceDispatcher.Debounce(() => Globals.Instance.SlideRenderQueue.Enqueue(this));

        public void Render()
        {
            var spec = ScriptureSlideSpecBuilder.Build(this);
            using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
            var cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
            var thumb = BitmapUtils.CreateThumbnail(cached);
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () => { Cached = cached; Thumbnail = thumb; },
                Avalonia.Threading.DispatcherPriority.Background);
        }

        private BaseSlideTheme? _theme;
        public BaseSlideTheme? Theme
        {
            get => _theme;
            set => this.RaiseAndSetIfChanged(ref _theme, value);
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

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ScriptureSlideInstanceTests"`
Expected: PASS (7 tests — 6 from Task 1 + this one).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 121 + 1 = 122, no regressions.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Slides/ScriptureSlideInstance.cs HandsLiftedApp.Tests/Models/RuntimeData/Slides/ScriptureSlideInstanceTests.cs
git commit -m "feat: wire ScriptureSlideInstance into IRenderable self-render pattern"
```

---

### Task 4: Wire into `LivePane`, `ProjectorWindow`, and the thumbnail strip

**Files:**
- Modify: `HandsLiftedApp.Core/Views/LivePane.axaml.cs`
- Modify: `HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs`
- Modify: `HandsLiftedApp.Core/Views/ItemSlidesView.axaml`

**Interfaces:**
- Consumes: `ScriptureSlideSpecBuilder.Build` (Task 2), `ScriptureSlideInstance` (Tasks 1/3).
- Produces: nothing new for later tasks — this is the last piece of Phase 3. A `ScriptureItemInstance`'s generated `ScriptureSlideInstance`s can now actually be previewed and projected once Phase 4's editor UI (a separate, later phase) constructs and displays one.

**No automated test for this task** — see Global Constraints. Verification is a manual diff review confirming the new arms/template are byte-for-byte structurally identical in shape to the existing `SongSlideInstance` entries, placed in the same position.

- [ ] **Step 1: Add the switch arm in `LivePane.axaml.cs`**

In `HandsLiftedApp.Core/Views/LivePane.axaml.cs`, find the `OnActiveSlideChanged` method's switch expression (currently, per line 76-88):

```csharp
            SlideRenderSpec? spec = slide switch
            {
                SongSlideInstance s      => SongSlideSpecBuilder.Build(s),
                SongTitleSlideInstance t => SongTitleSlideSpecBuilder.Build(t),
                ImageSlideInstance img   => IsValidMediaPath(img.SourceMediaFilePath)
                    ? new SlideRenderSpec(new ImageBackground(img.SourceMediaFilePath), Array.Empty<RenderElement>())
                    : null,
                LogoSlide                => IsValidMediaPath(logoPath)
                    ? new SlideRenderSpec(new ImageBackground(logoPath), Array.Empty<RenderElement>())
                    : null,
                HandsLiftedApp.Data.Data.Models.Slides.CustomSlide cs => CustomSlideSpecBuilder.Build(cs),
                _                        => null,
            };
```

Add a `ScriptureSlideInstance` arm right after the `SongTitleSlideInstance` arm:

```csharp
            SlideRenderSpec? spec = slide switch
            {
                SongSlideInstance s      => SongSlideSpecBuilder.Build(s),
                SongTitleSlideInstance t => SongTitleSlideSpecBuilder.Build(t),
                ScriptureSlideInstance sc => ScriptureSlideSpecBuilder.Build(sc),
                ImageSlideInstance img   => IsValidMediaPath(img.SourceMediaFilePath)
                    ? new SlideRenderSpec(new ImageBackground(img.SourceMediaFilePath), Array.Empty<RenderElement>())
                    : null,
                LogoSlide                => IsValidMediaPath(logoPath)
                    ? new SlideRenderSpec(new ImageBackground(logoPath), Array.Empty<RenderElement>())
                    : null,
                HandsLiftedApp.Data.Data.Models.Slides.CustomSlide cs => CustomSlideSpecBuilder.Build(cs),
                _                        => null,
            };
```

Add `using HandsLiftedApp.Core.Render.Skia.Builders;` to the file's usings if not already present (it already is, since `SongSlideSpecBuilder` is used in the same file — verify, don't duplicate).

- [ ] **Step 2: Add the identical switch arm in `ProjectorWindow.axaml.cs`**

In `HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs`, find the identical switch expression (currently, per line 129-139) and make the identical one-line addition (`ScriptureSlideInstance sc => ScriptureSlideSpecBuilder.Build(sc),` right after the `SongTitleSlideInstance` arm), matching Step 1 exactly.

- [ ] **Step 3: Add the thumbnail-strip `DataTemplate`**

In `HandsLiftedApp.Core/Views/ItemSlidesView.axaml`, find the `<common:MyTemplateSelector>` block (currently, per line 369-434) containing:

```xml
                                        <common:MyTemplateSelector>
                                            <DataTemplate x:DataType="slides:SongTitleSlideInstance" x:Key="SongTitleSlideInstance">
                                                <Image Source="{Binding Thumbnail}" Stretch="Uniform" />
                                            </DataTemplate>
                                            <DataTemplate x:DataType="slides:SongSlideInstance" x:Key="SongSlideInstance">
                                                <Image Source="{Binding Thumbnail}" Stretch="Uniform" />
                                            </DataTemplate>
```

Add a `ScriptureSlideInstance` entry right after the `SongSlideInstance` one:

```xml
                                        <common:MyTemplateSelector>
                                            <DataTemplate x:DataType="slides:SongTitleSlideInstance" x:Key="SongTitleSlideInstance">
                                                <Image Source="{Binding Thumbnail}" Stretch="Uniform" />
                                            </DataTemplate>
                                            <DataTemplate x:DataType="slides:SongSlideInstance" x:Key="SongSlideInstance">
                                                <Image Source="{Binding Thumbnail}" Stretch="Uniform" />
                                            </DataTemplate>
                                            <DataTemplate x:DataType="slides:ScriptureSlideInstance" x:Key="ScriptureSlideInstance">
                                                <Image Source="{Binding Thumbnail}" Stretch="Uniform" />
                                            </DataTemplate>
```

`slides:` already resolves to `clr-namespace:HandsLiftedApp.Data.Slides` in this file's existing xmlns declarations (`ScriptureSlideInstance` lives in that same namespace, confirmed in Tasks 1/3) — no new xmlns needed.

- [ ] **Step 4: Build the whole solution to confirm the XAML and C# changes compile**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj --nologo`
Expected: Build succeeded, 0 errors (warnings pre-existing in this project are fine, see Phase 1's baseline note — do not attempt to fix unrelated warnings).

- [ ] **Step 5: Run the full test suite once more**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS, count = 122 (unchanged — this task adds no new automated tests, per the Global Constraints note).

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Views/LivePane.axaml.cs HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs HandsLiftedApp.Core/Views/ItemSlidesView.axaml
git commit -m "feat: wire ScriptureSlideInstance into LivePane/ProjectorWindow rendering and thumbnail strip"
```

---

## What This Phase Does Not Cover

- No `ScriptureLibrary`/`LibraryType.Scripture`/persistence — Phase 4.
- No editor UI, no "Add Scripture" library entry point, no way for a user to actually create a `ScriptureItemInstance` in the running app yet — Phase 4. This phase makes rendering *work*, not *reachable* from the UI.
- No per-item theme/Design picker for scripture items — deferred indefinitely until requested; every scripture slide uses the app's default theme.
- No automated verification that a scripture slide visually renders correctly on screen — requires the user to run the app after Phase 4 wires up a way to create one.
