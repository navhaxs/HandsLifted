using System;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using SkiaSharp;

namespace HandsLiftedApp.Core.Models.Library
{
    /// <summary>
    /// What <see cref="LyricReflow"/> needs to predict how far autofit will shrink a slide: a line-width measurer
    /// (pixels at the theme's full font size) plus the canvas limits. Mirrors <c>SongSlideSpecBuilder.ComputeAutofitSize</c>.
    /// </summary>
    public sealed record ReflowMetrics(
        Func<string, double> MeasureWidth,
        double MaxWidth,
        double MaxHeight,
        double LineHeight,
        double MinScale)
    {
        // Theme defaults (FontSize 100, LineHeightEm 1.2) with an average glyph 0.5em wide.
        private const double DefaultCharWidth = 50.0;

        /// <summary>No theme available: estimate from the default theme.</summary>
        public static ReflowMetrics Default { get; } =
            new(l => l.Length * DefaultCharWidth, 1760, 920, 120, 0.5);

        /// <summary>Lines never count as too wide. Isolates phrasing from splitting/shrink in tests.</summary>
        public static ReflowMetrics Unconstrained { get; } =
            new(l => l.Length * DefaultCharWidth, double.MaxValue, 920, 120, 0.5);

        /// <summary>Real measurements from the theme's font. A theme without autofit never shrinks, so falls back to <see cref="Default"/>.</summary>
        public static ReflowMetrics FromTheme(BaseSlideTheme theme)
        {
            if (!theme.AutofitEnabled) return Default;

            return new ReflowMetrics(
                line =>
                {
                    using var typeface = SongSlideSpecBuilder.GetTypeface(theme);
                    using var font = new SKFont(typeface, theme.FontSize);
                    return font.MeasureText(line);
                },
                SongSlideSpecBuilder.MaxTextWidth,
                SongSlideSpecBuilder.MaxTextHeight,
                theme.FontSize * (double)theme.LineHeightEm,
                (double)theme.AutofitMinFontSizeRatio);
        }
    }
}
