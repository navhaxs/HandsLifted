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

public record TextLineElement(
    string Text,
    SKRect Bounds,
    SKTypeface Typeface,
    float FontSize,
    SKColor Color,
    DropShadowSpec? Shadow
) : RenderElement(Bounds);

public record DropShadowSpec(float OffsetX, float OffsetY, float BlurRadius, SKColor Color);
