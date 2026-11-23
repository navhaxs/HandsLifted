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

        private static List<int> SmartSizes(List<string> lines) =>
            throw new NotImplementedException();
    }
}
