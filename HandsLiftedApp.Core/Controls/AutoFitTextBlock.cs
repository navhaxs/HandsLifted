using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace HandsLiftedApp.Controls
{
    /// <summary>
    /// Wrapped text that is never taller than the space it is given. Used by the song "text mode" slide boxes.
    /// <para>
    /// With <see cref="AutoFit"/> on, the font size is reduced from <see cref="MaxFontSize"/> (never increased) to
    /// the largest size at which the text, <b>re-wrapped to the available width at that size</b>, fits the available
    /// height. (Scaling an already-wrapped block down instead - e.g. a <see cref="Viewbox"/> - leaves the text wrapped
    /// for the unscaled width, so it ends up narrow with a blank gap on the right.)
    /// </para>
    /// <para>
    /// Whatever still doesn't fit - at <see cref="MinFontSize"/>, or at <see cref="MaxFontSize"/> with
    /// <see cref="AutoFit"/> off - is cut after the last whole line that fits, ending in an ellipsis.
    /// </para>
    /// </summary>
    public class AutoFitTextBlock : Control
    {
        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<AutoFitTextBlock, string?>(nameof(Text));

        public static readonly StyledProperty<double> MaxFontSizeProperty =
            AvaloniaProperty.Register<AutoFitTextBlock, double>(nameof(MaxFontSize), 16);

        public static readonly StyledProperty<double> MinFontSizeProperty =
            AvaloniaProperty.Register<AutoFitTextBlock, double>(nameof(MinFontSize), 8);

        public static readonly StyledProperty<bool> AutoFitProperty =
            AvaloniaProperty.Register<AutoFitTextBlock, bool>(nameof(AutoFit), true);

        public static readonly StyledProperty<IBrush?> ForegroundProperty =
            TextElement.ForegroundProperty.AddOwner<AutoFitTextBlock>();

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            TextElement.FontFamilyProperty.AddOwner<AutoFitTextBlock>();

        public static readonly StyledProperty<FontWeight> FontWeightProperty =
            TextElement.FontWeightProperty.AddOwner<AutoFitTextBlock>();

        public static readonly StyledProperty<FontStyle> FontStyleProperty =
            TextElement.FontStyleProperty.AddOwner<AutoFitTextBlock>();

        static AutoFitTextBlock()
        {
            AffectsMeasure<AutoFitTextBlock>(TextProperty, MaxFontSizeProperty, MinFontSizeProperty, AutoFitProperty,
                FontFamilyProperty, FontWeightProperty, FontStyleProperty);
            AffectsRender<AutoFitTextBlock>(ForegroundProperty);
        }

        public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

        /// <summary>The font size used when the text fits; the upper bound when auto-fitting.</summary>
        public double MaxFontSize { get => GetValue(MaxFontSizeProperty); set => SetValue(MaxFontSizeProperty, value); }

        /// <summary>The smallest font size auto-fit will shrink to.</summary>
        public double MinFontSize { get => GetValue(MinFontSizeProperty); set => SetValue(MinFontSizeProperty, value); }

        /// <summary>Shrink the font size so the text fits, instead of just cutting it off with an ellipsis.</summary>
        public bool AutoFit { get => GetValue(AutoFitProperty); set => SetValue(AutoFitProperty, value); }

        public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
        public FontStyle FontStyle { get => GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }

        private TextLayout? _layout;

        /// <summary>The font size the text was last laid out at (after any shrinking).</summary>
        public double EffectiveFontSize { get; private set; }

        /// <summary>True if the last layout had to cut text off (and so ends in an ellipsis).</summary>
        public bool IsTrimmed { get; private set; }

        private TextLayout CreateLayout(string text, double fontSize, double maxWidth)
            => new(text,
                new Typeface(FontFamily, FontStyle, FontWeight),
                fontSize,
                Foreground,
                textWrapping: TextWrapping.Wrap,
                maxWidth: maxWidth);

        /// <summary>The text that was actually laid out: <see cref="Text"/>, or its cut-down version ending in an ellipsis.</summary>
        public string DisplayedText { get; private set; } = string.Empty;

        // Cuts the text after the last whole line that fits (maxLines), ending in an ellipsis. Avalonia's own
        // TextTrimming doesn't add one when the cut falls at the end of a line, so it's done here.
        private TextLayout CreateTrimmedLayout(string text, double fontSize, double maxWidth, int maxLines, out string shown)
        {
            var full = CreateLayout(text, fontSize, maxWidth);
            var lines = full.TextLines;
            var last = lines[Math.Min(maxLines, lines.Count) - 1];
            var kept = text.Substring(0, Math.Min(text.Length, last.FirstTextSourceIndex + last.Length)).TrimEnd();

            while (true)
            {
                shown = kept + "…";
                var candidate = CreateLayout(shown, fontSize, maxWidth);
                // the ellipsis can push a full last line onto a new one; drop characters until it doesn't
                if (candidate.TextLines.Count <= maxLines || kept.Length == 0)
                    return candidate;
                kept = kept.Substring(0, kept.Length - 1).TrimEnd();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var maxWidth = availableSize.Width;
            var maxHeight = availableSize.Height;
            var maxSize = Math.Max(1, MaxFontSize);
            var minSize = Math.Min(maxSize, Math.Max(1, MinFontSize));

            var size = maxSize;
            var text = Text ?? string.Empty;
            var shown = text;
            var layout = CreateLayout(text, size, maxWidth);
            IsTrimmed = false;

            if (layout.Height > maxHeight)
            {
                if (AutoFit)
                {
                    // largest font size (to 0.5 unit) whose re-wrapped text fits; text height only ever grows with size
                    double lo = minSize, hi = maxSize;
                    var fitted = false;
                    var fittedLayout = layout;
                    var fittedSize = minSize;
                    while (hi - lo > 0.5)
                    {
                        var mid = (lo + hi) / 2;
                        var candidate = CreateLayout(text, mid, maxWidth);
                        if (candidate.Height <= maxHeight)
                        {
                            lo = mid;
                            fitted = true;
                            fittedLayout = candidate;
                            fittedSize = mid;
                        }
                        else hi = mid;
                    }

                    if (!fitted)
                    {
                        // not even close to fitting at the smallest size, or only fits at (about) the smallest size
                        var atMin = CreateLayout(text, minSize, maxWidth);
                        if (atMin.Height <= maxHeight)
                        {
                            fitted = true;
                            fittedLayout = atMin;
                            fittedSize = minSize;
                        }
                        else
                        {
                            size = minSize;
                            layout = atMin;
                        }
                    }

                    if (fitted)
                    {
                        size = fittedSize;
                        layout = fittedLayout;
                    }
                }

                if (layout.Height > maxHeight)
                {
                    // cut after the last whole line that fits, with a trailing ellipsis
                    var lineHeight = layout.TextLines.Count > 0 ? layout.TextLines[0].Height : size;
                    var maxLines = Math.Max(1, (int)Math.Floor(maxHeight / Math.Max(1, lineHeight)));
                    layout = CreateTrimmedLayout(text, size, maxWidth, maxLines, out shown);
                    IsTrimmed = true;
                }
            }

            _layout?.Dispose();
            _layout = layout;
            DisplayedText = shown;
            EffectiveFontSize = size;

            return new Size(
                double.IsFinite(maxWidth) ? Math.Min(maxWidth, Math.Ceiling(layout.WidthIncludingTrailingWhitespace)) : Math.Ceiling(layout.WidthIncludingTrailingWhitespace),
                Math.Ceiling(layout.Height));
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            _layout?.Draw(context, new Point(0, 0));
        }
    }
}
