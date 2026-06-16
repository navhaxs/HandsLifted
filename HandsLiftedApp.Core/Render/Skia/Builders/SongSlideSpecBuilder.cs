// HandsLiftedApp.Core/Render/Skia/Builders/SongSlideSpecBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq;
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

    private static DropShadowSpec? GetShadow(BaseSlideTheme theme) =>
        theme.DropShadowEnabled
            ? new DropShadowSpec(
                (float)theme.DropShadowOffsetX,
                (float)theme.DropShadowOffsetY,
                (float)theme.DropShadowBlurRadius,
                ToSkColor(theme.DropShadowColour))
            : null;

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
        var rawLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        using var typeface = GetTypeface(theme);
        using var measureFont = new SKFont(typeface, theme.FontSize);
        using var measurePaint = new SKPaint(measureFont);

        float maxWidth = CanvasWidth - 2 * HorizontalMargin;

        // Word-wrap each input line into display lines that fit within maxWidth.
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

        float lineHeight = theme.LineHeight;
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

            // Create a fresh SKTypeface per element (the measurement typeface is disposed above)
            var elemTypeface = GetTypeface(theme);
            result.Add(new TextLineElement(line, bounds, elemTypeface, theme.FontSize, color, GetShadow(theme)));
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
