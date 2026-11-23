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
        public static string Reflow(string? lyrics, ReflowMode mode)
        {
            var lines = NormalisedLines(lyrics);
            if (lines.Count == 0) return "";

            var sizes = mode.IsSmart ? SmartSizes(lines) : FixedSizes(lines.Count, mode.LinesPerSlide);

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

        /// <summary>Key = first few words (letters only, lowercase); null when the line is too short to have one.</summary>
        private static string? OpeningKey(string line)
        {
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => new string(w.Where(char.IsLetter).ToArray()).ToLowerInvariant())
                .Where(w => w.Length > 0)
                .Take(RepeatOpenWords)
                .ToList();
            return words.Count < RepeatOpenWords ? null : string.Join(" ", words);
        }

        private static List<int> SmartSizes(List<string> lines)
        {
            var n = lines.Count;
            if (n <= 4) return new List<int> { n };

            // opensRepeat[i] = line i opens (first words) the same as some earlier line, so a break before it reads naturally
            var opensRepeat = new bool[n];
            var seen = new HashSet<string>();
            for (var i = 0; i < n; i++)
            {
                var key = OpeningKey(lines[i]);
                if (key == null) continue;
                opensRepeat[i] = !seen.Add(key);
            }

            // memo[pos, last] = best (cost, sizes) for lines[pos..] given the previous slide had `last` lines
            var memo = new Dictionary<(int, int), (double Cost, List<int> Sizes)?>();

            (double Cost, List<int> Sizes)? Solve(int pos, int last)
            {
                if (pos == n) return (0, new List<int>());
                if (memo.TryGetValue((pos, last), out var cached)) return cached;

                (double Cost, List<int> Sizes)? best = null;
                for (var size = 4; size >= 2; size--)
                {
                    var end = pos + size;
                    if (end > n) continue;

                    var rest = Solve(end, size);
                    if (rest == null) continue;

                    var cost = ChunkCost + (size == 4 ? FourLineCost : 0)
                               + (last != 0 && last != size ? UnevenCost : 0)
                               - (end < n && Rhymes(lines[end - 2], lines[end - 1]) ? RhymeBonus : 0)
                               - (end < n && opensRepeat[end] ? RepeatOpenBonus : 0)
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

            return Solve(0, 0)!.Value.Sizes;
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
