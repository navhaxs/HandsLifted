# Theme Background Opacity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-theme "Background opacity" slider that blends the theme's background image or motion video over the theme's background colour.

**Architecture:** New `BaseSlideTheme.BackgroundOpacity` (int percent). One shared `ThemeBackgroundSpecBuilder` turns (theme, hasMotionBackground, videoFrame) into a `BackgroundSpec`; the three theme-driven spec builders delegate to it. Images/video-frame bitmaps carry `BackColor` + `Opacity` and `SlideRenderer` draws colour first, bitmap on top at `alpha*Opacity`. Motion video stays in its own layer; the canvas draws the colour over it at alpha `1 - opacity` (source-over gives `colour*(1-o) + video*o`). No change to `MotionBackgroundLayer` / `MotionBackgroundObserver` / `MotionBackgroundService`.

**Tech Stack:** C# / Avalonia 11 / ReactiveUI / SkiaSharp / MSTest (`HandsLiftedApp.Tests`).

**Spec:** `docs/superpowers/specs/2026-11-17-theme-background-opacity-design.md`

## Global Constraints

- `BackgroundOpacity` is an `int` percent, 0-100, default **100**; setter clamps to 0-100. Missing XML element must load as 100.
- At 100 every existing opaque-image / motion-video theme must render exactly as before (motion theme at 100 still returns `TransparentBackground`).
- Do NOT edit `MotionBackgroundLayer`, `MotionBackgroundObserver`, `MotionBackgroundService` (one-`MotionBackgroundLayer` rule).
- Custom slide backgrounds (`CustomSlideSpecBuilder`, `CustomSlideModel`) are out of scope.
- New fields on `ImageBackground` / `SkiaBitmapBackground` are optional trailing params so `SlideSpecResolver`, `CustomSlideSpecBuilder` and other existing callers compile unchanged.
- Tests touching theme `ReactiveObject`s / undo run inside `DispatcherTestThread.Run(...)`; call `UndoStack.Clear()` after setup edits.
- Never shell out to `find`/`grep` (use Glob/Grep tools). Commit messages end with the session's `Co-Authored-By` trailer.
- Run tests with: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~<ClassName>"`.

## Review Focus

- Motion theme at 0%: video fully hidden, colour only (builder alpha 255). Pinned in Task 3.
- Theme colour that itself has alpha (colour-picker alpha): motion overlay alpha must scale it, not replace it. Pinned in Task 3.
- Hand-edited / corrupt theme XML with out-of-range opacity (250, -3): clamped, no exception. Pinned in Task 1.
- Image file missing or undecodable while `BackColor` is set: colour still drawn, no throw. Pinned in Task 2.
- Slide transition from a transparent (motion at 100) slide into a tinted image slide: colour and image fade in together. Pinned in Task 2.

---

### Task 1: `BackgroundOpacity` on the theme (model, XML, undo)

**Files:**
- Modify: `HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs` (next to `BackgroundGraphicFilePath`, ~line 447)
- Modify: `HandsLiftedApp.Core/Undo/ThemeEditRecorder.cs` (the `"Change Background"` group, ~line 169)
- Create: `HandsLiftedApp.Tests/Models/SlideTheme/BaseSlideThemeBackgroundOpacityTests.cs`
- Modify (tests): `HandsLiftedApp.Tests/Utils/SlideThemeXmlSerializerTests.cs`, `HandsLiftedApp.Tests/Undo/ThemeEditRecorderTests.cs`

**Interfaces:**
- Produces: `BaseSlideTheme.BackgroundOpacity : int` (`[DataMember]`, default 100, clamped 0-100, raises property change). Tasks 3 and 4 rely on this name.

- [ ] **Step 1: Write the failing tests**

In the new `BaseSlideThemeBackgroundOpacityTests` (mirror `BaseSlideThemeBackgroundModeTests` header/namespace):
`BackgroundOpacity_DefaultsTo100`; `BackgroundOpacity_ClampsToZeroAndHundred` (set 150 -> 100, set -5 -> 0); `CopyFrom_CopiesBackgroundOpacity` (source 40, target default -> 40).

In `SlideThemeXmlSerializerTests` (use its existing serialize/deserialize helpers around `SlideThemeXmlSerializer.TrySerializeToBytes` / `TryDeserialize(Stream, out BaseSlideTheme?)`):
- `BackgroundOpacity_RoundTripsThroughXml`: theme with 40 -> bytes -> deserialize -> 40.
- `ThemeXmlWithoutBackgroundOpacity_LoadsAs100`: serialize a default theme, remove the `<BackgroundOpacity>...</BackgroundOpacity>` element from the UTF-8 text, deserialize -> 100.
- `ThemeXmlWithOutOfRangeBackgroundOpacity_IsClamped`: replace the element's value with `250` -> 100; with `-3` -> 0.

In `ThemeEditRecorderTests` (same `DispatcherTestThread.Run` + `Open()` style as `LabelsFollowWhatChanged`):
`ChangingBackgroundOpacity_IsUndoable_AndLabelledChangeBackground`: `themes[0].BackgroundOpacity = 40;` assert `stack.UndoMenuText == "Undo Change Background"`; `stack.Undo()` -> 100; `stack.Redo()` -> 40.

- [ ] **Step 2: Run to verify failure** — `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~BackgroundOpacity"`. Expected: build error (`BackgroundOpacity` not defined).

- [ ] **Step 3: Implement `public int BackgroundOpacity { get; set; }` in `BaseSlideTheme.cs`.** Backing field `_backgroundOpacity = 100`; `[DataMember]`; setter `this.RaiseAndSetIfChanged(ref _backgroundOpacity, Math.Clamp(value, 0, 100))`. Add `nameof(BaseSlideTheme.BackgroundOpacity)` to the `"Change Background"` group in `ThemeEditRecorder.Groups`. (`ThemeSnapshot` and `CopyFrom` are reflection-based; nothing else to do.)

- [ ] **Step 4: Run** the same filter. Expected: all PASS. Also run `--filter "FullyQualifiedName~ThemeEditRecorderTests"` and `~SlideThemeXmlSerializerTests` to confirm no regressions.

- [ ] **Step 5: Commit** — `feat: add BackgroundOpacity to BaseSlideTheme`

---

### Task 2: Renderer draws back colour + bitmap at opacity

**Files:**
- Modify: `HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs:15-16`
- Modify: `HandsLiftedApp.Core/Render/Skia/SlideRenderer.cs` (`DrawBackground`, lines ~256-303)
- Test: `HandsLiftedApp.Tests/Render/Skia/SlideRendererTests.cs`

**Interfaces:**
- Produces (Task 3 relies on these exact shapes):
  - `public record ImageBackground(string FilePath, SKColor? BackColor = null, float Opacity = 1f) : BackgroundSpec;`
  - `public record SkiaBitmapBackground(SKBitmap Bitmap, SKColor? BackColor = null, float Opacity = 1f) : BackgroundSpec;`

- [ ] **Step 1: Write the failing tests** in `SlideRendererTests` (render with `SlideRenderer.RenderToSKBitmap(spec, 100, 100)`, read `GetPixel(50, 50)`; add a private helper `SolidBitmap(SKColor)` returning a 2x2 `SKBitmap` filled with the colour):
  - `SkiaBitmapBackground_HalfOpacityOverBlue_BlendsEvenly`: red bitmap, `BackColor` blue, `Opacity` 0.5 -> Red 128 +-2, Blue 128 +-2, Alpha 255.
  - `SkiaBitmapBackground_FullOpacity_ShowsOnlyBitmap`: red over blue at 1 -> (255,0,0).
  - `SkiaBitmapBackground_ZeroOpacity_ShowsOnlyBackColor`: -> (0,0,255).
  - `SkiaBitmapBackground_NoBackColor_DrawsBitmapUnchanged`: `new SkiaBitmapBackground(red)` -> (255,0,0) (existing behaviour).
  - `ImageBackground_HalfOpacityOverBlue_BlendsEvenly`: write a red 2x2 PNG to `Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png")` (encode via `SKImage.FromBitmap(...).Encode()`), delete it in `finally`; same assertions as the first test.
  - `ImageBackground_MissingFile_WithBackColor_DrawsBackColorAndDoesNotThrow` (Review Focus): nonexistent path, `BackColor` red, opacity 0.5 -> pixel (255,0,0).
  - `Transition_FromTransparent_FadesBackColorAndBitmapTogether` (Review Focus): previous `TransparentBackground`, current `SkiaBitmapBackground(red, blue, 0.5f)`, `RenderToSKBitmap(curr, 100, 100, prev, 0.5f)` -> Red 102 +-3, Blue 153 +-3, Alpha 159 +-3.

- [ ] **Step 2: Run** `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SlideRendererTests"`. Expected: build error (records lack the new params).

- [ ] **Step 3: Implement.** Add the two record shapes above. In `SlideRenderer`, add `private static void DrawBitmapLayer(SKCanvas canvas, SKBitmap bitmap, SKColor? backColor, float opacity, float alpha, int width, int height)` and call it from both the `ImageBackground` (after `LoadCachedBitmap` returns non-null **or null**) and `SkiaBitmapBackground` cases. Behaviour: if `backColor` is set, fill the canvas with it, colour alpha multiplied by `alpha` (same formula as `SolidBackground`); then, if the bitmap is non-null, draw it with paint alpha `alpha * Math.Clamp(opacity, 0, 1)` (only set `paint.Color` when that product < 1, as today). The missing-file/decode-failure case must still draw the colour.

- [ ] **Step 4: Run** the same filter. Expected: all PASS (including the pre-existing tests).

- [ ] **Step 5: Commit** — `feat: draw back colour under background images at opacity`

---

### Task 3: `ThemeBackgroundSpecBuilder` + builders delegate

**Files:**
- Create: `HandsLiftedApp.Core/Render/Skia/Builders/ThemeBackgroundSpecBuilder.cs`
- Modify: `SongSlideSpecBuilder.cs:39-51`, `SongTitleSlideSpecBuilder.cs:50-65`, `ScriptureParagraphSpecBuilder.cs:40-49` (all in `HandsLiftedApp.Core/Render/Skia/Builders/`) — replace each `BuildBackground` body with a call to the new helper
- Create (tests): `HandsLiftedApp.Tests/Render/Skia/Builders/ThemeBackgroundSpecBuilderTests.cs`
- Modify (tests): `SongSlideSpecBuilderTests.cs`, `SongTitleSlideSpecBuilderTests.cs`, `ScriptureParagraphSpecBuilderTests.cs`

**Interfaces:**
- Consumes: Task 1 `BaseSlideTheme.BackgroundOpacity`; Task 2 `ImageBackground`/`SkiaBitmapBackground` shapes.
- Produces: `public static BackgroundSpec ThemeBackgroundSpecBuilder.Build(BaseSlideTheme? theme, bool hasMotionBackground, SKBitmap? videoFrame = null)`.

Rules for `Build` (let `o = theme.BackgroundOpacity / 100f`, `colour` = theme `BackgroundAvaloniaColour` converted to `SKColor` with a private helper, black when theme is null; precedence is first match):
1. `videoFrame != null` -> `new SkiaBitmapBackground(videoFrame, colour, o)` (null theme: `BackColor` null, `o` 1).
2. `hasMotionBackground` -> `o >= 1` ? `TransparentBackground` : `SolidBackground(colour with Alpha = round(colour.Alpha * (1 - o)))`.
3. non-empty `theme.BackgroundGraphicFilePath` -> `new ImageBackground(path, colour, o)`.
4. else `SolidBackground(colour)` (opacity ignored when there is no media).

- [ ] **Step 1: Write the failing tests** in `ThemeBackgroundSpecBuilderTests` (themes built like `SongSlideSpecBuilderTests.MakeTheme()`; use `Colors.Blue`, converted expectation `new SKColor(0,0,255,255)`):
  - `NullTheme_NoMotion_IsBlackSolid`
  - `Plain_NoMedia_IsSolidThemeColour_OpacityIgnored` (opacity 40 -> `SolidBackground` with the exact theme colour)
  - `Image_CarriesColourAndOpacity` -> `ImageBackground("a.png", blue, 0.4f)` (opacity 40)
  - `Motion_Opacity100_IsTransparent`
  - `Motion_Opacity40_IsSolidColourWithAlpha153`
  - `Motion_Opacity0_IsFullyOpaqueColour` -> alpha 255 (Review Focus)
  - `Motion_ColourWithOwnAlpha_IsScaledNotReplaced`: colour alpha 200, opacity 50 -> alpha 100 (Review Focus)
  - `VideoFrame_WinsOverMotion_CarriesColourAndOpacity`: `AreSame` bitmap, `BackColor` blue, `Opacity` 0.4f.

  Per-builder tests: `SongSlideSpecBuilderTests.Build_ImageThemeWithOpacity_CarriesOpacity` (follow the existing image test pattern: `BackgroundGraphicFilePath = "some/image.png"`, `BackgroundOpacity = 40` -> `ImageBackground.Opacity == 0.4f`); the same for `SongTitleSlideSpecBuilderTests` (pattern at its existing image test) and `ScriptureParagraphSpecBuilderTests` (mirror that file's existing slide setup).

- [ ] **Step 2: Run** `--filter "FullyQualifiedName~SpecBuilderTests"`. Expected: build error (`ThemeBackgroundSpecBuilder` not defined).

- [ ] **Step 3: Implement `ThemeBackgroundSpecBuilder.Build`** per the rules above. Then in each of the three builders make `BuildBackground` a one-line delegate: `ThemeBackgroundSpecBuilder.Build(slide.Theme, slide.HasMotionBackground)` (song), `.Build(slide.Theme, slide.HasMotionBackground, videoFrame)` (title), `.Build(slide.Theme, false)` (scripture, which has no motion background). Leave each builder's own text `ToSkColor` untouched.

- [ ] **Step 4: Run** `--filter "FullyQualifiedName~SpecBuilderTests"` then the whole `HandsLiftedApp.Tests` project. Expected: all PASS.

- [ ] **Step 5: Commit** — `feat: blend theme background colour and opacity in spec builders`

---

### Task 4: Designer slider + docs

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml` (directly after the "Graphic" `DockPanel`, ~line 511, before the `Border` divider)
- Modify: `CLAUDE.md` (Motion background section: one short note)

**Interfaces:**
- Consumes: Task 1 `BackgroundOpacity`. The preview views (`themePreviewSlideView` etc.) already rebuild on any theme property change, so no code-behind change.

- [ ] **Step 1: Add the slider.** A `TextBlock Text="Background Opacity (%)"` followed by `controls:NumericSliderBox Minimum="0" Maximum="100" TickFrequency="1" TextBoxMinWidth="40" Text="{Binding BackgroundOpacity, FallbackValue=100}"`. Enable it only when an image is set or the mode is motion: `IsEnabled` as a `MultiBinding` with `Converter="{x:Static BoolConverters.Or}"` over `BackgroundGraphicFilePath` (`Converter="{x:Static StringConverters.IsNotNullOrEmpty}"`) and `BackgroundMode` (`Converter="{StaticResource IsMotionBackgroundModeConverter}"`, already declared in this file). If the compiled-binding setup rejects that, drop the `IsEnabled` gate and leave the slider always enabled (acceptable; say so in the commit message).

- [ ] **Step 2: Build** — `dotnet build HandsLiftedApp.Core`. Expected: succeeds with no new errors.

- [ ] **Step 3: Docs.** Add 2-3 lines to `CLAUDE.md` under "Motion background (libmpv)": a theme's background opacity over a motion video is done by the canvas drawing the theme colour at alpha `1 - opacity` on top of the video layer (`ThemeBackgroundSpecBuilder`), not by changing the video layer's opacity; at 100 it returns `TransparentBackground`.

- [ ] **Step 4: Full test run** — `dotnet test HandsLiftedApp.Tests`. Expected: all PASS.

- [ ] **Step 5: Manual click-through (needs a human with a display; the Avalonia devtools MCP is unavailable here).** In the theme designer: image theme at 100 looks unchanged, at 50 dims toward the colour, slider undo/redo works from Edit menu; motion theme at 50 over a real video shows lyrics on a dimmed/tinted video in LivePane, ProjectorWindow and StageDisplay; at 0 only the colour shows; at 100 identical to before. Report anything off rather than committing.

- [ ] **Step 6: Commit** — `feat: background opacity slider in theme designer`
