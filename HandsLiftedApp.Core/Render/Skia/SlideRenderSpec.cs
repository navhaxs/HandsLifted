// HandsLiftedApp.Core/Render/Skia/SlideRenderSpec.cs
using System.Collections.Generic;
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

/// <remarks>
/// Structural equality is intentionally incomplete: <see cref="SKTypeface"/> uses reference equality,
/// so two elements with the same content but different typeface instances will not compare equal.
/// Transition diffing uses <see cref="TextLineElement.Text"/> directly — do not rely on record equality for semantic comparison.
/// </remarks>
public record TextLineElement(
    string Text,
    SKRect Bounds,
    SKTypeface Typeface,
    float FontSize,
    SKColor Color,
    DropShadowSpec? Shadow
) : RenderElement(Bounds);

public record DropShadowSpec(float OffsetX, float OffsetY, float BlurRadius, SKColor Color);
