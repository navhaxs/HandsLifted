// HandsLiftedApp.Core/Render/Skia/Builders/SongTitleSlideSpecBuilder.cs
using System;
using System.Collections.Generic;
using Avalonia.Media;
using VerticalAlignment = Avalonia.Layout.VerticalAlignment;
using SkiaSharp;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

public static class SongTitleSlideSpecBuilder
{
    private const int CanvasWidth = 1920;
    private const int CanvasHeight = 1080;
    private const float HorizontalMargin = 80f;
    private const float TitleVerticalMargin = 80f;
    private const float CopyrightBottomMargin = 60f;
    private const float TitleCopyrightGap = 20f;

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

        float copyrightTop = !string.IsNullOrWhiteSpace(slide.Copyright)
            ? ComputeCopyrightTop(slide.Copyright, slide.Theme)
            : CanvasHeight;

        if (!string.IsNullOrWhiteSpace(slide.Title))
            elements.AddRange(BuildTitleElements(slide.Title, slide.Theme, copyrightTop));

        if (!string.IsNullOrWhiteSpace(slide.Copyright))
            elements.AddRange(BuildCopyrightElements(slide.Copyright, slide.Theme));

        return new SlideRenderSpec(bg, elements);
    }

    private static BackgroundSpec BuildBackground(SongTitleSlideInstance slide, SKBitmap? videoFrame = null) =>
        ThemeBackgroundSpecBuilder.Build(slide.Theme, slide.HasMotionBackground, videoFrame);

    private static List<RenderElement> BuildTitleElements(string title, BaseSlideTheme theme, float copyrightTop)
    {
        using var typeface = GetTypeface(theme);
        using var measureFont = new SKFont(typeface, theme.TitleFontSize);
        using var measurePaint = new SKPaint(measureFont);

        var lines = WrapLines(title, measurePaint, CanvasWidth - 2 * HorizontalMargin);

        float titleLineHeight = theme.TitleFontSize * (float)theme.LineHeightEm;
        float blockHeight = lines.Count * titleLineHeight;
        // Bottom alignment must never dip into the copyright block; Center always centers on the whole slide.
        float availableBottom = Math.Min(CanvasHeight, copyrightTop - TitleCopyrightGap);
        float top = theme.TitleVerticalAlignment switch
        {
            VerticalAlignment.Top    => TitleVerticalMargin,
            VerticalAlignment.Bottom => Math.Min(CanvasHeight - TitleVerticalMargin, availableBottom) - blockHeight,
            _                        => (CanvasHeight - blockHeight) / 2f, // Center / Stretch
        };

        var result = new List<RenderElement>(lines.Count);
        var color = ToSkColor(theme.TextAvaloniaColour);
        for (int i = 0; i < lines.Count; i++)
        {
            float textWidth = measurePaint.MeasureText(lines[i]);
            float x = theme.TitleTextAlignment switch
            {
                TextAlignment.Right => CanvasWidth - textWidth - HorizontalMargin,
                TextAlignment.Left  => HorizontalMargin,
                _                   => (CanvasWidth - textWidth) / 2f, // Center / Justify
            };
            float y = top + i * titleLineHeight;
            var bounds = new SKRect(x, y, x + textWidth, y + titleLineHeight);
            // Fresh typeface per element (the measurement typeface is disposed above).
            result.Add(new TextLineElement(lines[i], bounds, GetTypeface(theme), theme.TitleFontSize,
                color, GetShadow(theme)));
        }
        return result;
    }

    /// <summary>Word-wraps text (honouring explicit newlines) to lines no wider than maxWidth.</summary>
    private static List<string> WrapLines(string text, SKPaint paint, float maxWidth)
    {
        var result = new List<string>();
        foreach (var raw in text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string current = string.Empty;
            foreach (var word in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.Length == 0) { current = word; continue; }
                string candidate = current + " " + word;
                if (paint.MeasureText(candidate) > maxWidth)
                {
                    result.Add(current);
                    current = word;
                }
                else current = candidate;
            }
            if (current.Length > 0) result.Add(current);
        }
        if (result.Count == 0) result.Add(text);
        return result;
    }

    private static float ComputeCopyrightTop(string copyright, BaseSlideTheme theme)
    {
        var displayLines = WrapCopyrightLines(copyright, theme);
        float lineHeight = theme.CopyrightFontSize * 1.2f;
        return CanvasHeight - CopyrightBottomMargin - displayLines.Count * lineHeight;
    }

    private static List<string> WrapCopyrightLines(string copyright, BaseSlideTheme theme)
    {
        var rawLines = copyright.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        float maxWidth = CanvasWidth - 2 * HorizontalMargin;

        using var typeface = GetTypeface(theme);
        using var measureFont = new SKFont(typeface, theme.CopyrightFontSize);
        using var measurePaint = new SKPaint(measureFont);

        // Word-wrap each input line into display lines that fit within maxWidth.
        var displayLines = new List<string>();
        foreach (var raw in rawLines)
        {
            if (string.IsNullOrEmpty(raw))
            {
                displayLines.Add(string.Empty);
                continue;
            }

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

        return displayLines;
    }

    private static IEnumerable<RenderElement> BuildCopyrightElements(string copyright, BaseSlideTheme theme)
    {
        float copyrightSize = theme.CopyrightFontSize;
        float lineHeight = copyrightSize * 1.2f;

        using var typeface = GetTypeface(theme);
        using var measureFont = new SKFont(typeface, copyrightSize);
        using var measurePaint = new SKPaint(measureFont);

        var displayLines = WrapCopyrightLines(copyright, theme);

        int n = displayLines.Count;
        var result = new List<RenderElement>(n);
        var color = ToSkColor(theme.TextAvaloniaColour);

        for (int i = 0; i < n; i++)
        {
            string line = displayLines[i];
            if (string.IsNullOrEmpty(line)) continue; // empty line = spacing gap only

            // Stack lines above the bottom margin; empty lines still consume vertical space.
            float lineTop = CanvasHeight - CopyrightBottomMargin - (n - i) * lineHeight;
            float textWidth = measurePaint.MeasureText(line);
            float x = theme.CopyrightTextAlignment switch
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
        var weight = (SKFontStyleWeight)(int)theme.FontWeight;
        var slant  = theme.CalculatedTextFontItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return SKTypeface.FromFamilyName(theme.FontFamilyAsText, weight, SKFontStyleWidth.Normal, slant)
               ?? SKTypeface.Default;
    }

    private static SKColor ToSkColor(Color color) =>
        new SKColor(color.R, color.G, color.B, color.A);
}
