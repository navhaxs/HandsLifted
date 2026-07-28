using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using SkiaSharp;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

public static class ScriptureParagraphSpecBuilder
{
    private const int CanvasWidth = ScriptureParagraphLayoutEngine.CanvasWidth;
    private const int CanvasHeight = ScriptureParagraphLayoutEngine.CanvasHeight;
    private const float HorizontalMargin = ScriptureParagraphLayoutEngine.HorizontalMargin;

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

        if (slide.Theme == null || slide.Lines.Count == 0)
            return new SlideRenderSpec(bg, Array.Empty<RenderElement>());

        var elements = BuildTextElements(slide.Lines, slide.Theme);
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

    private static IReadOnlyList<RenderElement> BuildTextElements(
        IReadOnlyList<ScriptureParagraphLine> lines, BaseSlideTheme theme)
    {
        using var typeface = GetTypeface(theme);
        float bodyFontSize = theme.FontSize;
        float headerFontSize = bodyFontSize * ScriptureParagraphLayoutEngine.HeaderFontSizeRatio;
        float superscriptFontSize = bodyFontSize * ScriptureParagraphLayoutEngine.SuperscriptFontSizeRatio;
        float superscriptBaselineOffset = -(bodyFontSize * ScriptureParagraphLayoutEngine.SuperscriptBaselineOffsetRatio);
        float lineHeight = bodyFontSize * (float)theme.LineHeightEm;
        float headerLineHeight = headerFontSize * (float)theme.LineHeightEm;
        var color = ToSkColor(theme.TextAvaloniaColour);
        var shadow = GetShadow(theme);

        using var bodyFont = new SKFont(typeface, bodyFontSize);
        using var bodyPaint = new SKPaint(bodyFont);
        using var superscriptFont = new SKFont(typeface, superscriptFontSize);
        using var superscriptPaint = new SKPaint(superscriptFont);
        using var headerTypeface = GetBoldTypeface(theme);
        using var headerFont = new SKFont(headerTypeface, headerFontSize);
        using var headerPaint = new SKPaint(headerFont);

        bool hasHeader = lines.Any(l => l.IsHeader);
        float totalHeight = lines.Sum(l => l.IsHeader ? headerLineHeight : lineHeight);
        if (hasHeader && lines.Any(l => !l.IsHeader))
            totalHeight += ScriptureParagraphLayoutEngine.HeaderSpacingBelow;

        float startY = (CanvasHeight - totalHeight) / 2f;
        var result = new List<RenderElement>(lines.Count);
        float y = startY;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            float thisLineHeight = line.IsHeader ? headerLineHeight : lineHeight;
            float lineFontSize = line.IsHeader ? headerFontSize : bodyFontSize;
            var linePaint = line.IsHeader ? headerPaint : bodyPaint;

            float lineWidth = 0f;
            var runs = new List<TextRun>(line.Runs.Count);
            foreach (var run in line.Runs)
            {
                bool isSuperscript = !line.IsHeader && run.IsSuperscript;
                var runPaint = isSuperscript ? superscriptPaint : linePaint;
                float runFontSize = isSuperscript ? superscriptFontSize : lineFontSize;
                float runOffset = isSuperscript ? superscriptBaselineOffset : 0f;

                lineWidth += runPaint.MeasureText(run.Text);
                runs.Add(new TextRun(run.Text, runFontSize, runOffset));
            }

            float x = theme.TextAlignment switch
            {
                TextAlignment.Right => CanvasWidth - lineWidth - HorizontalMargin,
                TextAlignment.Left => HorizontalMargin,
                _ => (CanvasWidth - lineWidth) / 2f, // Center / Justify
            };

            var bounds = new SKRect(x, y, x + lineWidth, y + thisLineHeight);

            // Create a fresh SKTypeface per element (the measurement typeface is disposed above).
            // Header lines render bold per the design spec; body lines use the theme's plain weight.
            var elemTypeface = line.IsHeader ? GetBoldTypeface(theme) : GetTypeface(theme);
            result.Add(new MultiRunTextLineElement(runs, bounds, elemTypeface, color, shadow));

            y += thisLineHeight;
            bool isLastHeaderLineBeforeBody = line.IsHeader && (i + 1 >= lines.Count || !lines[i + 1].IsHeader);
            if (isLastHeaderLineBeforeBody)
                y += ScriptureParagraphLayoutEngine.HeaderSpacingBelow;
        }

        return result;
    }

    private static SKTypeface GetTypeface(BaseSlideTheme theme)
    {
        var weight = (SKFontStyleWeight)(int)theme.FontWeight;
        var slant = theme.CalculatedTextFontItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return SKTypeface.FromFamilyName(theme.FontFamilyAsText, weight, SKFontStyleWidth.Normal, slant)
               ?? SKTypeface.Default;
    }

    private static SKTypeface GetBoldTypeface(BaseSlideTheme theme)
    {
        var slant = theme.CalculatedTextFontItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return SKTypeface.FromFamilyName(theme.FontFamilyAsText, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, slant)
               ?? SKTypeface.Default;
    }

    private static SKColor ToSkColor(Color color) =>
        new SKColor(color.R, color.G, color.B, color.A);
}
