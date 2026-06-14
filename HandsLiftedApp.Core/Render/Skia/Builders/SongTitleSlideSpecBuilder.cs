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
    private const float HorizontalMargin = 80f;
    private const float CopyrightSizeRatio = 0.45f;
    private const float CopyrightBottomMargin = 60f;

    private static DropShadowSpec? GetShadow(BaseSlideTheme theme) =>
        theme.DropShadowEnabled
            ? new DropShadowSpec(
                (float)theme.DropShadowOffsetX,
                (float)theme.DropShadowOffsetY,
                (float)theme.DropShadowBlurRadius,
                ToSkColor(theme.DropShadowColour))
            : null;

    public static SlideRenderSpec Build(SongTitleSlideInstance slide, SKBitmap? videoFrame = null)
    {
        var bg = BuildBackground(slide, videoFrame);
        if (slide.Theme == null) return new SlideRenderSpec(bg, Array.Empty<RenderElement>());

        var elements = new List<RenderElement>();

        if (!string.IsNullOrWhiteSpace(slide.Title))
            elements.Add(BuildTitleElement(slide.Title, slide.Theme));

        if (!string.IsNullOrWhiteSpace(slide.Copyright))
            elements.AddRange(BuildCopyrightElements(slide.Copyright, slide.Theme));

        return new SlideRenderSpec(bg, elements);
    }

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

    private static TextLineElement BuildTitleElement(string title, BaseSlideTheme theme)
    {
        using var typeface = GetTypeface(theme);
        using var measureFont = new SKFont(typeface, theme.FontSize);
        using var measurePaint = new SKPaint(measureFont);
        float textWidth = measurePaint.MeasureText(title);
        float x = theme.TextAlignment switch
        {
            TextAlignment.Right => CanvasWidth - textWidth - HorizontalMargin,
            TextAlignment.Left  => HorizontalMargin,
            _                   => (CanvasWidth - textWidth) / 2f, // Center / Justify
        };
        float y = (CanvasHeight - theme.LineHeight) / 2f;
        var bounds = new SKRect(x, y, x + textWidth, y + theme.LineHeight);
        var elemTypeface = GetTypeface(theme);
        return new TextLineElement(title, bounds, elemTypeface, theme.FontSize,
            ToSkColor(theme.TextAvaloniaColour), GetShadow(theme));
    }

    private static IEnumerable<RenderElement> BuildCopyrightElements(string copyright, BaseSlideTheme theme)
    {
        var lines = copyright.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        float copyrightSize = theme.FontSize * CopyrightSizeRatio;
        float lineHeight = copyrightSize * 1.2f;
        int n = lines.Length;

        var result = new List<RenderElement>(n);
        using var typeface = GetTypeface(theme);
        using var measureFont = new SKFont(typeface, copyrightSize);
        using var measurePaint = new SKPaint(measureFont);
        var color = ToSkColor(theme.TextAvaloniaColour);

        for (int i = 0; i < n; i++)
        {
            string line = lines[i];
            if (string.IsNullOrEmpty(line)) continue; // empty line = spacing gap only

            // Stack lines above the bottom margin; empty lines still consume vertical space.
            float lineTop = CanvasHeight - CopyrightBottomMargin - (n - i) * lineHeight;
            float textWidth = measurePaint.MeasureText(line);
            float x = theme.TextAlignment switch
            {
                TextAlignment.Right => CanvasWidth - textWidth - HorizontalMargin,
                TextAlignment.Left  => HorizontalMargin,
                _                   => (CanvasWidth - textWidth) / 2f, // Center / Justify
            };
            var bounds = new SKRect(x, lineTop, x + textWidth, lineTop + lineHeight);
            result.Add(new TextLineElement(line, bounds, GetTypeface(theme), copyrightSize, color, GetShadow(theme)));
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
