// HandsLiftedApp.Core/Render/Skia/Builders/SongTitleSlideSpecBuilder.cs
using System;
using System.Collections.Generic;
using Avalonia.Media;
using SkiaSharp;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

public static class SongTitleSlideSpecBuilder
{
    private const int CanvasWidth = 1920;
    private const int CanvasHeight = 1080;
    private const float CopyrightSizeRatio = 0.45f;
    private const float CopyrightBottomMargin = 60f;

    private static readonly DropShadowSpec DefaultShadow =
        new DropShadowSpec(0f, 15f, 20f, SKColors.Black);

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
        using var typeface = GetTypeface(theme);
        using var measureFont = new SKFont(typeface, theme.FontSize);
        using var measurePaint = new SKPaint(measureFont);
        float textWidth = measurePaint.MeasureText(title);
        float x = (CanvasWidth - textWidth) / 2f;
        float y = (CanvasHeight - theme.LineHeight) / 2f;
        var bounds = new SKRect(x, y, x + textWidth, y + theme.LineHeight);
        var elemTypeface = GetTypeface(theme);
        return new TextLineElement(title, bounds, elemTypeface, theme.FontSize,
            ToSkColor(theme.TextAvaloniaColour), DefaultShadow);
    }

    private static TextLineElement BuildCopyrightElement(string copyright, BaseSlideTheme theme)
    {
        float copyrightSize = theme.FontSize * CopyrightSizeRatio;
        using var typeface = GetTypeface(theme);
        using var measureFont = new SKFont(typeface, copyrightSize);
        using var measurePaint = new SKPaint(measureFont);
        float textWidth = measurePaint.MeasureText(copyright);
        float x = (CanvasWidth - textWidth) / 2f;
        float lineHeight = copyrightSize * 1.2f;
        float y = CanvasHeight - lineHeight - CopyrightBottomMargin;
        var bounds = new SKRect(x, y, x + textWidth, y + lineHeight);
        var elemTypeface = GetTypeface(theme);
        return new TextLineElement(copyright, bounds, elemTypeface, copyrightSize,
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
