using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public readonly record struct ScriptureParagraphRun(string Text, bool IsSuperscript);

    public readonly record struct ScriptureParagraphLine(IReadOnlyList<ScriptureParagraphRun> Runs, bool IsHeader);

    public readonly record struct ScriptureParagraphPage(IReadOnlyList<ScriptureParagraphLine> Lines);

    public static class ScriptureParagraphLayoutEngine
    {
        public const int CanvasWidth = 1920;
        public const int CanvasHeight = 1080;
        public const float HorizontalMargin = 80f;
        public const float VerticalMargin = 80f;
        public const float HeaderFontSizeRatio = 1.3f;
        public const float SuperscriptFontSizeRatio = 0.6f;
        public const float SuperscriptBaselineOffsetRatio = 0.35f;
        public const float HeaderSpacingBelow = 20f;

        public static List<ScriptureParagraphPage> Paginate(
            IReadOnlyList<ScriptureVerseRef> verses, string headerText, BaseSlideTheme theme)
        {
            float maxWidth = CanvasWidth - 2 * HorizontalMargin;
            float maxHeight = CanvasHeight - 2 * VerticalMargin;
            float bodyFontSize = theme.FontSize;
            float headerFontSize = bodyFontSize * HeaderFontSizeRatio;
            float lineHeight = bodyFontSize * (float)theme.LineHeightEm;
            float headerLineHeight = headerFontSize * (float)theme.LineHeightEm;

            using var typeface = GetTypeface(theme);
            using var bodyFont = new SKFont(typeface, bodyFontSize);
            using var bodyPaint = new SKPaint(bodyFont);
            using var superscriptFont = new SKFont(typeface, bodyFontSize * SuperscriptFontSizeRatio);
            using var superscriptPaint = new SKPaint(superscriptFont);
            using var headerTypeface = GetBoldTypeface(theme);
            using var headerFont = new SKFont(headerTypeface, headerFontSize);
            using var headerPaint = new SKPaint(headerFont);

            var headerUnits = TokenizeHeader(headerText);
            var headerLines = WrapUnits(headerUnits, headerPaint, headerPaint, maxWidth, isHeader: true);

            var bodyUnits = TokenizeVerses(verses);
            var bodyLines = WrapUnits(bodyUnits, bodyPaint, superscriptPaint, maxWidth, isHeader: false);

            var pages = new List<ScriptureParagraphPage>();
            var currentPageLines = new List<ScriptureParagraphLine>();
            float heightUsed = 0f;

            foreach (var line in headerLines)
            {
                currentPageLines.Add(line);
                heightUsed += headerLineHeight;
            }
            if (headerLines.Count > 0)
                heightUsed += HeaderSpacingBelow;

            foreach (var line in bodyLines)
            {
                if (currentPageLines.Count > 0 && heightUsed + lineHeight > maxHeight)
                {
                    pages.Add(new ScriptureParagraphPage(currentPageLines));
                    currentPageLines = new List<ScriptureParagraphLine>();
                    heightUsed = 0f;
                }
                currentPageLines.Add(line);
                heightUsed += lineHeight;
            }

            pages.Add(new ScriptureParagraphPage(currentPageLines));
            return pages;
        }

        // A "unit" is one or two runs that must never be split across a line wrap:
        // a plain word is a 1-run unit; a verse's superscript marker + its first word
        // is a 2-run unit, so the marker can never end up orphaned alone at the end of
        // a line with its word pushed to the next line.
        private readonly record struct WrapUnit(IReadOnlyList<ScriptureParagraphRun> Runs);

        private static List<WrapUnit> TokenizeHeader(string headerText)
        {
            if (string.IsNullOrWhiteSpace(headerText))
                return new List<WrapUnit>();

            return headerText
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => new WrapUnit(new[] { new ScriptureParagraphRun(word, IsSuperscript: false) }))
                .ToList();
        }

        private static List<WrapUnit> TokenizeVerses(IReadOnlyList<ScriptureVerseRef> verses)
        {
            var units = new List<WrapUnit>();
            int? previousChapter = null;

            foreach (var v in verses)
            {
                string marker = previousChapter.HasValue && previousChapter.Value != v.Chapter
                    ? $"{v.Chapter}:{v.Verse}"
                    : $"{v.Verse}";
                previousChapter = v.Chapter;

                var words = v.Text.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    units.Add(new WrapUnit(new[] { new ScriptureParagraphRun(marker, IsSuperscript: true) }));
                    continue;
                }

                units.Add(new WrapUnit(new[]
                {
                    new ScriptureParagraphRun(marker, IsSuperscript: true),
                    new ScriptureParagraphRun(words[0], IsSuperscript: false)
                }));

                for (int w = 1; w < words.Length; w++)
                    units.Add(new WrapUnit(new[] { new ScriptureParagraphRun(words[w], IsSuperscript: false) }));
            }

            return units;
        }

        private static List<ScriptureParagraphLine> WrapUnits(
            List<WrapUnit> units, SKPaint bodyPaint, SKPaint superscriptPaint, float maxWidth, bool isHeader)
        {
            var lines = new List<ScriptureParagraphLine>();
            var currentRuns = new List<ScriptureParagraphRun>();
            float currentWidth = 0f;

            foreach (var unit in units)
            {
                bool isFirstOnLine = currentRuns.Count == 0;
                var runsToAdd = isFirstOnLine ? unit.Runs : PrependSpace(unit.Runs);
                float unitWidth = MeasureRuns(runsToAdd, bodyPaint, superscriptPaint);

                if (!isFirstOnLine && currentWidth + unitWidth > maxWidth)
                {
                    lines.Add(new ScriptureParagraphLine(currentRuns, isHeader));
                    currentRuns = new List<ScriptureParagraphRun>();
                    currentWidth = 0f;
                    runsToAdd = unit.Runs;
                    unitWidth = MeasureRuns(runsToAdd, bodyPaint, superscriptPaint);
                }

                currentRuns.AddRange(runsToAdd);
                currentWidth += unitWidth;
            }

            if (currentRuns.Count > 0)
                lines.Add(new ScriptureParagraphLine(currentRuns, isHeader));

            return lines;
        }

        // Inter-unit spacing is its own standalone (non-superscript) run rather than being
        // fused onto whichever run happens to be first in the unit. Fusing it in would corrupt
        // a verse marker's Text (e.g. "2:1" becoming " 2:1") whenever the marker+word unit isn't
        // the very first unit on a wrapped line -- the normal, common case. Keeping the space as
        // its own run means every marker/word run's Text always matches its token exactly, while
        // the space still travels atomically with the rest of the unit (same AddRange batch), so
        // it can never itself be split from its unit or end up as an orphaned trailing run.
        private static IReadOnlyList<ScriptureParagraphRun> PrependSpace(IReadOnlyList<ScriptureParagraphRun> runs)
        {
            var copy = new List<ScriptureParagraphRun>(runs.Count + 1)
            {
                new ScriptureParagraphRun(" ", IsSuperscript: false)
            };
            copy.AddRange(runs);
            return copy;
        }

        private static float MeasureRuns(IReadOnlyList<ScriptureParagraphRun> runs, SKPaint bodyPaint, SKPaint superscriptPaint)
        {
            float total = 0f;
            foreach (var run in runs)
                total += (run.IsSuperscript ? superscriptPaint : bodyPaint).MeasureText(run.Text);
            return total;
        }

        private static SKTypeface GetTypeface(BaseSlideTheme theme)
        {
            var weight = theme.CalculatedTextFontBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
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
    }
}
