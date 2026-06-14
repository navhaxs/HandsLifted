using System;

namespace HandsLiftedApp.Core.Models.Library
{
    internal static class SongFuzzyMatcher
    {
        // Returns 0 if any token is unmatched → exclude item. Otherwise sum of scores.
        internal static int Score(string[] tokens, SongIndexEntry entry)
        {
            int total = 0;
            foreach (var token in tokens)
            {
                int best = 0;
                best = Math.Max(best, ScoreToken(token, entry.Title)     * 3);
                best = Math.Max(best, ScoreToken(token, entry.Copyright) * 3 / 2);
                best = Math.Max(best, ScoreToken(token, entry.LyricText));
                if (best == 0) return 0;
                total += best;
            }
            return total;
        }

        private static int ScoreToken(string token, string field)
        {
            if (string.IsNullOrEmpty(field)) return 0;
            if (field.Contains(token, StringComparison.OrdinalIgnoreCase)) return 100;
            // word-prefix: any word in field starts with token
            bool newWord = true;
            for (int i = 0; i < field.Length; i++)
            {
                if (newWord && field.Length - i >= token.Length &&
                    field.AsSpan(i, token.Length).Equals(token.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    return 70;
                newWord = char.IsWhiteSpace(field[i]);
            }
            return IsSubsequence(token, field) ? 40 : 0;
        }

        private static bool IsSubsequence(string needle, string haystack)
        {
            int ni = 0;
            for (int hi = 0; hi < haystack.Length && ni < needle.Length; hi++)
                if (char.ToLowerInvariant(haystack[hi]) == char.ToLowerInvariant(needle[ni]))
                    ni++;
            return ni == needle.Length;
        }
    }
}
