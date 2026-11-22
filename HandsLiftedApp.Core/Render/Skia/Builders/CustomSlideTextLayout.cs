using System;
using System.Collections.Generic;
using Avalonia.Layout;
using Avalonia.Media;
using HandsLiftedApp.Data.Models.SlideElement;
using SkiaSharp;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

/// <summary>One laid-out line of a text element: left edge X, baseline Y and measured width, in slide pixels.</summary>
public sealed record LayoutLine(string Text, float X, float Baseline, float Width);

/// <summary>Shared text layout for custom slide text elements (used by both the renderer and the editor).</summary>
public static class CustomSlideTextLayout
{
    /// <summary>
    /// Creates a font for the element. Each call creates a fresh <see cref="SKTypeface"/>; the caller must
    /// dispose both the returned font and its <c>Typeface</c>.
    /// </summary>
    public static SKFont CreateFont(TextElement el)
    {
        var weight = el.FontWeight == FontWeight.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = el.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        var typeface = SKTypeface.FromFamilyName(el.FontFamilyAsText, weight, SKFontStyleWidth.Normal, slant)
                       ?? SKTypeface.Default;
        return new SKFont(typeface, el.FontSize)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Subpixel = true,
        };
    }

    public static IReadOnlyList<LayoutLine> Layout(TextElement el, SKFont font)
    {
        var result = new List<LayoutLine>();
        if (string.IsNullOrEmpty(el.Text)) return result;

        font.GetFontMetrics(out var metrics);
        float ascent = -metrics.Ascent; // ascent is negative in Skia

        var wrapped = WrapText(el.Text, font, el.Width);
        float lineHeight = el.LineHeight > 0 ? el.LineHeight : el.FontSize * 1.2f;
        float totalTextHeight = wrapped.Count * lineHeight;

        float startY = el.VerticalAlignment switch
        {
            VerticalAlignment.Bottom => el.Y + el.Height - totalTextHeight,
            VerticalAlignment.Center => el.Y + (el.Height - totalTextHeight) / 2f,
            _ => el.Y, // Top or Stretch
        };

        for (int i = 0; i < wrapped.Count; i++)
        {
            string line = wrapped[i];
            float width = font.MeasureText(line);
            float x = el.TextAlignment switch
            {
                TextAlignment.Right => el.X + el.Width - width,
                TextAlignment.Center => el.X + (el.Width - width) / 2f,
                _ => el.X, // Left or Justify
            };
            result.Add(new LayoutLine(line, x, startY + i * lineHeight + ascent, width));
        }
        return result;
    }

    // Word-wraps to maxWidth; explicit newlines first, then words. A word wider than the box sits on its own line.
    private static List<string> WrapText(string text, SKFont font, int maxWidth)
    {
        var result = new List<string>();
        var paragraphs = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var para in paragraphs)
        {
            if (string.IsNullOrEmpty(para))
            {
                result.Add("");
                continue;
            }

            string current = "";
            foreach (var word in para.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string test = current.Length == 0 ? word : current + " " + word;
                if (font.MeasureText(test) <= maxWidth)
                {
                    current = test;
                }
                else
                {
                    if (current.Length > 0) result.Add(current);
                    current = word;
                }
            }
            if (current.Length > 0) result.Add(current);
        }
        return result;
    }
}
