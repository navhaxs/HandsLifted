# Song Lyric Slide Autofit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Song lyric slides (`SongSlideInstance`) shrink their font size to keep each raw lyric line on one display line (no mid-line word-wrap) and keep the whole stanza within the canvas, controlled by a per-theme "Autofit" toggle. Song title slides are untouched.

**Architecture:** Two new `[DataMember]` properties on `BaseSlideTheme` (`AutofitEnabled`, `AutofitMinFontSizeRatio`). `SongSlideSpecBuilder.BuildTextElements` computes an effective font size/line-height before its existing word-wrap+layout logic runs, by measuring raw (pre-wrap) lines with `SkiaSharp` at decreasing candidate sizes until they fit both width and a new vertical margin, floored at a ratio of the theme's base size. The existing word-wrap logic is unchanged and acts as the safety net if even the floor doesn't fit. `SlideThemeDesigner.axaml` gets a checkbox + slider for the new fields.

**Tech Stack:** C# / .NET 8, Avalonia 11, SkiaSharp (`SKFont`/`SKPaint.MeasureText`), MSTest (`HandsLiftedApp.Tests`, real Skia text measurement — no mocking, matches existing `SongSlideSpecBuilderTests.cs`).

## Global Constraints

- Song title slides (`SongTitleSlideSpecBuilder.cs`) are explicitly out of scope — do not modify.
- `CustomSlideSpecBuilder.cs` (element-based slides) is out of scope — do not modify.
- Only `HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs` gets new fields — the parallel `HandsLiftedApp.Models/Models/SlideTheme/BaseSlideTheme.cs` copy is not referenced by `HandsLiftedApp.Core` and must not be touched.
- `AutofitEnabled` defaults to `true`; `AutofitMinFontSizeRatio` defaults to `0.5M`.
- New theme properties follow the existing plain `RaiseAndSetIfChanged` pattern (no clamping), matching `FontSize`/`LineHeightEm` in the same file.
- When `AutofitEnabled = false`, output must be byte-for-byte identical to today's fixed-size rendering path.

---

### Task 1: Add Autofit fields to `BaseSlideTheme`

**Files:**
- Modify: `HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs:212-235` (insert new properties next to `FontSize`/`LineHeightEm`)

**Interfaces:**
- Produces: `BaseSlideTheme.AutofitEnabled` (`bool`, default `true`), `BaseSlideTheme.AutofitMinFontSizeRatio` (`decimal`, default `0.5M`) — consumed by Task 2.

This task adds plain data properties with no independent behavior (matching how `FontSize`/`LineHeightEm` themselves have no dedicated unit test in this codebase) — coverage comes from Task 2's behavioral tests on `SongSlideSpecBuilder`, which read these properties through a real `BaseSlideTheme` instance.

- [ ] **Step 1: Add the two properties**

In `HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs`, immediately after the existing `LineHeightEm` property (ends at line 235), insert:

```csharp
        private bool _autofitEnabled = true;

        [DataMember]
        public bool AutofitEnabled
        {
            get => _autofitEnabled;
            set => this.RaiseAndSetIfChanged(ref _autofitEnabled, value);
        }

        private decimal _autofitMinFontSizeRatio = 0.5M;

        [DataMember]
        public decimal AutofitMinFontSizeRatio
        {
            get => _autofitMinFontSizeRatio;
            set => this.RaiseAndSetIfChanged(ref _autofitMinFontSizeRatio, value);
        }
```

- [ ] **Step 2: Build to confirm no compile errors**

Run: `dotnet build HandsLiftedApp.Data/HandsLiftedApp.Data.csproj`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs
git commit -m "feat: add AutofitEnabled/AutofitMinFontSizeRatio to BaseSlideTheme"
```

---

### Task 2: Autofit sizing in `SongSlideSpecBuilder`

**Files:**
- Modify: `HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs:12-124`
- Test: `HandsLiftedApp.Tests/Render/Skia/Builders/SongSlideSpecBuilderTests.cs`

**Interfaces:**
- Consumes: `BaseSlideTheme.AutofitEnabled` (`bool`), `BaseSlideTheme.AutofitMinFontSizeRatio` (`decimal`), `BaseSlideTheme.FontSize` (`int`), `BaseSlideTheme.LineHeightEm` (`decimal`) — all from Task 1.
- Produces: `TextLineElement.FontSize` now reflects the computed effective size (existing public field on the existing `TextLineElement` record — no signature change). No new public API.

Existing `SongSlideSpecBuilderTests.cs` tests (`Build_TwoLineText_ReturnsTwoTextElements`, etc.) use short lines and must keep passing unchanged — autofit is on by default but short lines already fit at the theme's base size, so the computed size equals `theme.FontSize` and behavior is identical.

- [ ] **Step 1: Write failing tests for autofit behavior**

Add to `HandsLiftedApp.Tests/Render/Skia/Builders/SongSlideSpecBuilderTests.cs`, inside the `SongSlideSpecBuilderTests` class:

```csharp
    // ~43 chars: measured to exceed the 1760px width at FontSize=100 (forcing a
    // wrap without autofit) but comfortably fit at the 0.5-ratio floor of 50
    // (so autofit is guaranteed to land on a single, non-floor, shrunk size).
    private const string LongSingleLine = "Consider Christ the source of our salvation";

    [TestMethod]
    public void Build_AutofitEnabled_LongLineShrinksAndStaysOnOneLine()
    {
        var slide = new SongSlideInstance(null, null, "id-autofit-1") { Text = LongSingleLine };
        slide.Theme = MakeTheme(); // AutofitEnabled defaults to true

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.AreEqual(1, spec.Elements.Count, "autofit should keep the raw line on one display line");
        var element = (TextLineElement)spec.Elements[0];
        Assert.IsTrue(element.FontSize < 100, "font should have shrunk below the theme size");
        Assert.IsTrue(element.FontSize >= 50, "font should not shrink below the 0.5 ratio floor");
    }

    [TestMethod]
    public void Build_AutofitDisabled_LongLineWrapsAtFixedSize()
    {
        var slide = new SongSlideInstance(null, null, "id-autofit-2") { Text = LongSingleLine };
        var theme = MakeTheme();
        theme.AutofitEnabled = false;
        slide.Theme = theme;

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.IsTrue(spec.Elements.Count > 1, "without autofit the long line should word-wrap into multiple lines");
        foreach (var el in spec.Elements)
        {
            Assert.AreEqual(100f, ((TextLineElement)el).FontSize, "font size must stay fixed when autofit is disabled");
        }
    }

    [TestMethod]
    public void Build_AutofitEnabled_ShortTextKeepsThemeFontSize()
    {
        var slide = new SongSlideInstance(null, null, "id-autofit-3") { Text = "Line one\nLine two" };
        slide.Theme = MakeTheme();

        var spec = SongSlideSpecBuilder.Build(slide);

        foreach (var el in spec.Elements)
        {
            Assert.AreEqual(100f, ((TextLineElement)el).FontSize, "short text should not be shrunk");
        }
    }

    [TestMethod]
    public void Build_AutofitEnabled_UnfittableLineFallsBackToFloorSize()
    {
        var oneGiantWord = new string('a', 300); // no spaces: word-wrap cannot split it further
        var slide = new SongSlideInstance(null, null, "id-autofit-4") { Text = oneGiantWord };
        slide.Theme = MakeTheme(); // AutofitMinFontSizeRatio defaults to 0.5

        var spec = SongSlideSpecBuilder.Build(slide);

        Assert.AreEqual(1, spec.Elements.Count);
        var element = (TextLineElement)spec.Elements[0];
        Assert.AreEqual(50f, element.FontSize, "line can never fit even shrunk, so size must land exactly on the floor");
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SongSlideSpecBuilderTests"`
Expected: the 4 new tests FAIL (compile error or assertion failure — `AutofitEnabled`/shrink behavior doesn't exist yet), existing tests in the file still PASS.

- [ ] **Step 3: Implement `ComputeAutofitSize` and wire it into `BuildTextElements`**

In `HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs`:

Add a vertical margin constant next to `HorizontalMargin` (line 16):

```csharp
    private const float HorizontalMargin = 80f;
    private const float VerticalMargin = 80f;
```

Replace the measurement setup at the top of `BuildTextElements` (current lines 56-60):

```csharp
        using var typeface = GetTypeface(theme);
        using var measureFont = new SKFont(typeface, theme.FontSize);
        using var measurePaint = new SKPaint(measureFont);

        float maxWidth = CanvasWidth - 2 * HorizontalMargin;
```

with:

```csharp
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
```

Replace the two remaining uses of `theme.FontSize`/`theme.LineHeight` further down in the same method:

```csharp
        float lineHeight = theme.LineHeight;
```
→
```csharp
        float lineHeight = effectiveLineHeight;
```

and:

```csharp
            result.Add(new TextLineElement(line, bounds, elemTypeface, theme.FontSize, color, GetShadow(theme)));
```
→
```csharp
            result.Add(new TextLineElement(line, bounds, elemTypeface, effectiveFontSize, color, GetShadow(theme)));
```

Add the new private method (place after `BuildTextElements`, before `GetTypeface`):

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SongSlideSpecBuilderTests"`
Expected: all tests in the file PASS (the 4 new ones plus the 5 pre-existing ones).

- [ ] **Step 5: Run the full test suite to check for regressions**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj`
Expected: `Passed!` — no failures in `SlideRendererTests`, `SlideRenderSpecTests`, or elsewhere (these don't touch `SongSlideSpecBuilder`'s internals, but confirms nothing else regressed).

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs HandsLiftedApp.Tests/Render/Skia/Builders/SongSlideSpecBuilderTests.cs
git commit -m "feat: shrink song lyric slide font to fit stanzas without wrapping"
```

---

### Task 3: Designer UI toggle

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml:300-311` (insert new controls after the existing "Line Height" block, before the `Border` divider at line 315)

**Interfaces:**
- Consumes: `BaseSlideTheme.AutofitEnabled`, `BaseSlideTheme.AutofitMinFontSizeRatio` (from Task 1) via direct `{Binding}` on the designer's `DataContext` (the selected theme), matching the existing `LineHeightEm` control's binding style in the same file — no element-name path binding, so the `CLAUDE.md` stale-binding gotcha doesn't apply here.

No automated test — this file has no existing AXAML/UI tests (`SlideThemeDesigner.axaml.cs` isn't covered by `HandsLiftedApp.Tests`); verification is manual, per Step 3 below.

- [ ] **Step 1: Add Autofit controls to the AXAML**

In `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml`, immediately after the closing `</DockPanel>` of the "Line Height" block (line 311) and before `<Border Height="1" Margin="0 10" Background="#808080" />` (line 315), insert:

```xml
                    <TextBlock Text="Autofit" Margin="0 10 0 0" />
                    <CheckBox Name="AutofitEnabledCheckBox"
                              Content="Shrink font to fit stanza"
                              IsChecked="{Binding AutofitEnabled}" />
                    <DockPanel IsEnabled="{Binding #AutofitEnabledCheckBox.IsChecked}">
                        <TextBox Name="AutofitMinRatioTextBox"
                                 MinWidth="30"
                                 Text="{Binding AutofitMinFontSizeRatio, FallbackValue=0.5}" />
                        <Slider Name="AutofitMinRatioSlider" Minimum="0.1" Maximum="1.0"
                                IsSnapToTickEnabled="True"
                                TickFrequency="0.01"
                                Value="{Binding #AutofitMinRatioTextBox.Text, Converter={StaticResource RoundedDecimalConverter}}" />
                    </DockPanel>
```

- [ ] **Step 2: Build to confirm the AXAML compiles**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: `Build succeeded.` (no XAML binding/resource errors — `RoundedDecimalConverter` is already a resource in this file, reused from the `LineHeightEm` slider).

- [ ] **Step 3: Manual verification in the running app**

Run the app (see project's own run instructions), open the Slide Theme Designer for any theme:
1. Confirm "Shrink font to fit stanza" checkbox appears under a new "Autofit" label, checked by default for existing themes.
2. Uncheck it, confirm the ratio slider becomes disabled (greyed out).
3. Re-check it, drag the ratio slider, confirm the adjacent text box updates in sync (same behavior as the existing Line Height slider/textbox pair).
4. Open a song with a long stanza (4+ lines, per the original screenshot), confirm slides render smaller text without mid-line wraps.
5. Toggle Autofit off for that theme, confirm the same stanza now wraps/overflows as it did before this change.
6. Open a song title slide using the same theme, confirm its font size is unaffected by the Autofit toggle.

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml
git commit -m "feat: add Autofit controls to Slide Theme Designer"
```
