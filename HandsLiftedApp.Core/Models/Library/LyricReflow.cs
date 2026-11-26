using System;
using System.Collections.Generic;
using System.Linq;

namespace HandsLiftedApp.Core.Models.Library
{
    /// <summary>How <see cref="LyricReflow"/> splits a stanza into slides. LinesPerSlide 0 = Smart.</summary>
    public readonly record struct ReflowMode(int LinesPerSlide)
    {
        public static ReflowMode Smart => new(0);

        public static ReflowMode Fixed(int n)
        {
            if (n < 2 || n > 4) throw new ArgumentOutOfRangeException(nameof(n), "Lines per slide must be 2-4.");
            return new ReflowMode(n);
        }

        public bool IsSmart => LinesPerSlide == 0;
    }

    /// <summary>
    /// Rewrites the blank-line slide breaks inside a stanza's lyrics. Never changes lyric text.
    /// </summary>
    public static class LyricReflow
    {
        public static string Reflow(string? lyrics, ReflowMode mode, ReflowMetrics? metrics = null)
        {
            var lines = NormalisedLines(lyrics);
            if (lines.Count == 0) return "";

            if (mode.IsSmart) return SmartReflow(lines, metrics ?? ReflowMetrics.Default);

            var sizes = FixedSizes(lines.Count, mode.LinesPerSlide);

            var chunks = new List<string>();
            var pos = 0;
            foreach (var size in sizes)
            {
                chunks.Add(string.Join("\n", lines.Skip(pos).Take(size)));
                pos += size;
            }
            return string.Join("\n\n", chunks);
        }

        /// <summary>Trimmed, non-blank lines (handles \r\n).</summary>
        internal static List<string> NormalisedLines(string? lyrics)
        {
            if (string.IsNullOrWhiteSpace(lyrics)) return new List<string>();
            return lyrics.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();
        }

        private static List<int> FixedSizes(int count, int n)
        {
            if (count <= n) return new List<int> { count };

            var sizes = new List<int>();
            for (var left = count; left > 0; left -= n) sizes.Add(Math.Min(n, left));

            // avoid a 1-line orphan: share the last two chunks as evenly as possible, larger first;
            // when that can't give both 2+ lines (n = 2), fold the orphan into the previous chunk
            if (sizes[^1] == 1)
            {
                var total = sizes[^2] + 1;
                if (total / 2 >= 2)
                {
                    sizes[^2] = (total + 1) / 2;
                    sizes[^1] = total / 2;
                }
                else
                {
                    sizes[^2] = total;
                    sizes.RemoveAt(sizes.Count - 1);
                }
            }
            return sizes;
        }

        // Smart-mode weights (lower total cost wins). Starting values, tuned against the song library.
        private const double ChunkCost = 1.5;     // each slide: fewer slides preferred
        private const double FourLineCost = 0.25; // a 4-line slide is slightly busier than a 3-line one
        private const double UnevenCost = 0.5;    // adjacent slides of different size
        private const double RhymeBonus = 0.5;    // a slide break right after a rhyming pair of lines
        private const double RepeatOpenBonus = 1.0; // a slide break right before a line that opens like an earlier line (a new phrase starts)
        private const int RepeatOpenWords = 3;

        /// <summary>
        /// A slide holding more than this many characters (all its lines together) is "heavy": autofit shrinks
        /// the font to the widest line and four long lines read as a wall of small text. Verses and bridges
        /// of 3 short lines sit near 100; four ~45-character chorus lines sit near 180.
        /// </summary>
        internal const int SlideCharCap = 140;
        private const double OverCapCost = 3.0;       // flat cost for any slide over the cap: beats the extra slide it takes to avoid it
        private const double OverCapPerRatio = 4.0;   // plus this per 100% over, so a lighter slide still wins

        private const double ShrinkWeight = 6.0;    // per 100% of font shrink on a slide
        private const double ScaleJumpWeight = 4.0; // per 100% difference in font scale between adjacent slides

        /// <summary>
        /// The fraction of the full font size autofit would leave this slide at: limited by its widest line and by its
        /// height, never below <see cref="ReflowMetrics.MinScale"/>.
        /// </summary>
        internal static double PredictedScale(IReadOnlyList<string> slideLines, ReflowMetrics m)
        {
            if (slideLines.Count == 0) return 1.0;
            var widest = 0.0;
            foreach (var l in slideLines) widest = Math.Max(widest, m.MeasureWidth(l));
            var byWidth = widest <= 0 ? 1.0 : m.MaxWidth / widest;
            var byHeight = m.MaxHeight / (slideLines.Count * m.LineHeight);
            return Math.Max(m.MinScale, Math.Min(1.0, Math.Min(byWidth, byHeight)));
        }

        private const double PeriodBreakBonus = 1.0; // a slide break after a whole number of repeat periods
        private const double PeriodMinScore = 0.45;  // mean similarity of lines one period apart needed to call it a structure
        private const double PeriodMargin = 0.25;    // ...and it must beat the stanza's average pairwise similarity by this much

        /// <summary>
        /// Lower-case letter-only words. A contraction keeps only the part before its apostrophe
        /// (<c>you've</c> -> <c>you</c>), so "If you've been" and "If you walk" open alike.
        /// </summary>
        private static List<string> NormWords(string line)
        {
            var result = new List<string>();
            foreach (var raw in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var w = raw.TrimStart('\'', '’');
                var cut = w.IndexOfAny(new[] { '\'', '’' });
                if (cut > 0) w = w[..cut];
                var letters = new string(w.Where(char.IsLetter).ToArray()).ToLowerInvariant();
                if (letters.Length > 0) result.Add(letters);
            }
            return result;
        }

        /// <summary>Key = first few words (see <see cref="NormWords"/>); null when the line is too short to have one.</summary>
        private static string? OpeningKey(string line)
        {
            var words = NormWords(line).Take(RepeatOpenWords).ToList();
            return words.Count < RepeatOpenWords ? null : string.Join(" ", words);
        }

        /// <summary>
        /// 0..1 "these two lines play the same role": 1 for an identical line, otherwise the best of shared opening words
        /// (4+ = 1), shared closing words (4+ = 1) and a rhyme (0.3).
        /// </summary>
        internal static double Similarity(string a, string b)
        {
            var wa = NormWords(a);
            var wb = NormWords(b);
            if (wa.Count == 0 || wb.Count == 0) return 0;
            if (wa.SequenceEqual(wb)) return 1.0;

            var max = Math.Min(wa.Count, wb.Count);
            var open = 0;
            while (open < max && wa[open] == wb[open]) open++;
            var trail = 0;
            while (trail < max && wa[^(trail + 1)] == wb[^(trail + 1)]) trail++;

            return Math.Max(Math.Max(Math.Min(1.0, open / 4.0), Math.Min(1.0, trail / 4.0)), Rhymes(a, b) ? 0.3 : 0);
        }

        /// <summary>
        /// The repeat period (2 or 3 lines) of a stanza whose lines echo the lines one period earlier (A B A B, or
        /// A B C A B), or 0 when there is no clear structure. Slide breaks then prefer period multiples.
        /// </summary>
        internal static int DetectPeriod(IReadOnlyList<string> lines)
        {
            var n = lines.Count;
            var best = 0;
            var bestScore = 0.0;
            foreach (var p in new[] { 2, 3 })
            {
                if (n - p < 2) continue; // need at least two pairs to call it a pattern
                var sum = 0.0;
                for (var i = 0; i + p < n; i++) sum += Similarity(lines[i], lines[i + p]);
                var score = sum / (n - p);
                if (score > bestScore + 1e-9) { best = p; bestScore = score; } // ties keep the shorter period
            }
            if (best == 0 || bestScore < PeriodMinScore) return 0;

            var pairSum = 0.0;
            var pairs = 0;
            for (var i = 0; i < n; i++)
                for (var j = i + 1; j < n; j++) { pairSum += Similarity(lines[i], lines[j]); pairs++; }
            return bestScore - pairSum / pairs >= PeriodMargin ? best : 0;
        }

        // ---- per-line splitting ----

        private const double SplitBelowScale = 0.85; // a line autofit would shrink below this is a candidate for splitting
        private const double MinSplitGain = 0.1;     // ...and a split must lift the narrower-fitting half's scale by at least this much
        private const double SplitPunctuationBonus = 3.0;
        private const double SplitConnectorBonus = 2.0;
        private const double SplitBalanceWeight = 2.0; // penalty for lopsided halves (0 = equal widths, 1 = all on one side)

        private static readonly HashSet<string> SplitConnectors = new(StringComparer.OrdinalIgnoreCase)
        {
            "and", "but", "or", "for", "so", "who", "whom", "that", "which", "when", "where", "while", "as",
            "to", "in", "with", "from", "by",
            "if", "because", "though", "until", "till", "yet", "than", "then",
        };

        /// <summary>
        /// Splits a line autofit would shrink hard into two lines at the most natural point (after a comma, before a
        /// connector, near the middle), capitalising the first letter of the second. Returns the line unchanged when it
        /// fits, has fewer than 4 words, or no split helps enough.
        /// </summary>
        internal static IReadOnlyList<string> SplitLongLine(string line, ReflowMetrics m)
        {
            var whole = new[] { line };
            var unsplitScale = PredictedScale(whole, m);
            if (unsplitScale >= SplitBelowScale) return whole;

            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 4) return whole;

            (string Left, string Right)? best = null;
            var bestScore = double.NegativeInfinity;
            for (var k = 2; k <= words.Length - 2; k++)
            {
                var left = string.Join(" ", words.Take(k));
                var right = string.Join(" ", words.Skip(k));

                var worseHalf = Math.Min(PredictedScale(new[] { left }, m), PredictedScale(new[] { right }, m));
                if (worseHalf - unsplitScale < MinSplitGain) continue;

                var lw = m.MeasureWidth(left);
                var rw = m.MeasureWidth(right);
                var score = (EndsClause(left) ? SplitPunctuationBonus : 0)
                            + (SplitConnectors.Contains(LeadingLetters(right)) ? SplitConnectorBonus : 0)
                            - (lw + rw > 0 ? Math.Abs(lw - rw) / (lw + rw) * SplitBalanceWeight : 0);

                if (score > bestScore + 1e-9) { best = (left, right); bestScore = score; } // ties keep the earlier split
            }

            return best == null ? whole : new[] { best.Value.Left, CapitaliseFirstLetter(best.Value.Right) };
        }

        private static bool EndsClause(string s) => s.Length > 0 && ",;:.!?–—-".Contains(s[^1]);

        private static string LeadingLetters(string s)
        {
            var w = s.Split(' ', 2)[0];
            return new string(w.Where(char.IsLetter).ToArray());
        }

        /// <summary>Upper-cases the first letter, skipping leading punctuation such as "(" or a quote; leaves everything else alone.</summary>
        private static string CapitaliseFirstLetter(string s)
        {
            for (var i = 0; i < s.Length; i++)
            {
                if (!char.IsLetterOrDigit(s[i])) continue;
                return char.IsLetter(s[i]) ? s[..i] + char.ToUpperInvariant(s[i]) + s[(i + 1)..] : s;
            }
            return s;
        }

        /// <summary>Smart mode: split over-wide lines, then group whole (logical) lines into slides.</summary>
        private static string SmartReflow(List<string> lines, ReflowMetrics metrics)
        {
            var phys = lines.Select(l => SplitLongLine(l, metrics)).ToList();
            var groups = SmartGroups(lines, phys, metrics);

            var chunks = new List<string>();
            var pos = 0;
            foreach (var size in groups)
            {
                chunks.Add(string.Join("\n", phys.Skip(pos).Take(size).SelectMany(p => p)));
                pos += size;
            }
            return string.Join("\n\n", chunks);
        }

        /// <summary>
        /// Groups the logical lines into slides; returns how many logical lines each slide takes. A split line's halves
        /// (<paramref name="phys"/>) always stay on one slide. A slide holds 2-4 physical lines.
        /// </summary>
        private static List<int> SmartGroups(List<string> lines, List<IReadOnlyList<string>> phys, ReflowMetrics metrics)
        {
            var n = lines.Count;

            // pp[i] = physical lines in items [0..i), cc[i] = their characters: a slide's weight is one subtraction
            var pp = new int[n + 1];
            var cc = new int[n + 1];
            for (var i = 0; i < n; i++)
            {
                pp[i + 1] = pp[i] + phys[i].Count;
                cc[i + 1] = cc[i] + phys[i].Sum(l => l.Length);
            }

            // up to 3 physical lines can't split without a 1-line orphan; 4 only split (2+2) when too dense to read as one slide
            if (pp[n] <= 3 || (pp[n] == 4 && cc[n] <= SlideCharCap)) return new List<int> { n };

            List<string> PhysLines(int from, int to) => phys.Skip(from).Take(to - from).SelectMany(p => p).ToList();

            // opensRepeat[i] = line i opens (first words) the same as some earlier line, so a break before it reads naturally
            var opensRepeat = new bool[n];
            var seen = new HashSet<string>();
            for (var i = 0; i < n; i++)
            {
                var key = OpeningKey(lines[i]);
                if (key == null) continue;
                opensRepeat[i] = !seen.Add(key);
            }

            // when the stanza has a clear repeat period, breaks on period multiples replace the break-before-repeat bonus
            // (that bonus pulls a repeated line away from its couplet partner)
            var period = DetectPeriod(lines);

            // memo[pos, last] = best (cost, sizes) for items [pos..) given the previous slide took `last` items
            // (sizes are item counts; the previous slide is items [pos-last..pos), so (pos, last) fixes its scale and size)
            var memo = new Dictionary<(int, int), (double Cost, List<int> Sizes)?>();

            (double Cost, List<int> Sizes)? Solve(int pos, int last)
            {
                if (pos == n) return (0, new List<int>());
                if (memo.TryGetValue((pos, last), out var cached)) return cached;

                (double Cost, List<int> Sizes)? best = null;
                for (var size = 4; size >= 1; size--)
                {
                    var end = pos + size;
                    if (end > n) continue;

                    var physLines = pp[end] - pp[pos];
                    if (physLines < 2 || physLines > 4) continue;

                    var rest = Solve(end, size);
                    if (rest == null) continue;

                    var chars = cc[end] - cc[pos];
                    var overCap = chars > SlideCharCap
                        ? OverCapCost + OverCapPerRatio * (chars - SlideCharCap) / SlideCharCap
                        : 0;

                    var scale = PredictedScale(PhysLines(pos, end), metrics);
                    var prevScale = last == 0 ? scale : PredictedScale(PhysLines(pos - last, pos), metrics);
                    var prevPhys = last == 0 ? 0 : pp[pos] - pp[pos - last];

                    var cost = ChunkCost + (physLines == 4 ? FourLineCost : 0) + overCap
                               + ShrinkWeight * (1 - scale) + ScaleJumpWeight * Math.Abs(scale - prevScale)
                               + (last != 0 && prevPhys != physLines ? UnevenCost : 0)
                               - (end < n && size >= 2 && Rhymes(lines[end - 2], lines[end - 1]) ? RhymeBonus : 0)
                               - (end < n && period == 0 && opensRepeat[end] ? RepeatOpenBonus : 0)
                               - (end < n && period > 0 && end % period == 0 ? PeriodBreakBonus : 0)
                               + rest.Value.Cost;

                    // strictly cheaper wins; sizes are tried largest first so ties keep the larger earlier slide
                    if (best == null || cost < best.Value.Cost - 1e-9)
                    {
                        var sizes = new List<int> { size };
                        sizes.AddRange(rest.Value.Sizes);
                        best = (cost, sizes);
                    }
                }

                memo[(pos, last)] = best;
                return best;
            }

            // no legal partition (not reachable for 1-2 physical lines per item): keep the stanza on one slide
            return Solve(0, 0)?.Sizes ?? new List<int> { n };
        }

        /// <summary>Crude rhyme check: last words (letters only) share their final 2 letters.</summary>
        internal static bool Rhymes(string lineA, string lineB)
        {
            var a = LastWord(lineA);
            var b = LastWord(lineB);
            if (a.Length == 0 || b.Length == 0) return false;
            var k = Math.Min(2, Math.Min(a.Length, b.Length));
            return a[^k..] == b[^k..];
        }

        private static string LastWord(string line)
        {
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = words.Length - 1; i >= 0; i--)
            {
                var letters = new string(words[i].Where(char.IsLetter).ToArray()).ToLowerInvariant();
                if (letters.Length > 0) return letters;
            }
            return "";
        }
    }
}
