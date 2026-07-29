using System.Text.RegularExpressions;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public readonly record struct ParsedScriptureReference(
        string BookCode,
        string BookName,
        int StartChapter,
        int? StartVerse,
        int EndChapter,
        int? EndVerse);

    public static class ScriptureReferenceParser
    {
        private const string GrammarHint = "Couldn't understand that. Try \"1 Peter 1:10-12\".";

        private static readonly Regex ReferencePattern = new(
            @"^(?<startChapter>\d+)\s*(?::\s*(?<startVerse>\d+)\s*(?:-\s*(?:(?<endChapter>\d+)\s*:\s*(?<endVerse>\d+)|(?<endVerseOnly>\d+)))?)?$",
            RegexOptions.Compiled);

        private static readonly Regex TrailingReferencePattern = new(
            @"\s+\d+(:\d+(-\d+(:\d+)?)?)?\s*$",
            RegexOptions.Compiled);

        public static bool TryParse(string input, out ParsedScriptureReference result, out string? error)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = GrammarHint;
                return false;
            }

            var trimmed = input.Trim();

            if (!ScriptureBookAliasCatalog.TryMatchBookPrefix(trimmed, out var bookCode, out var bookName, out var matchedLength))
            {
                error = $"Unknown book \"{ExtractLikelyBookToken(trimmed)}\".";
                return false;
            }

            var remainder = trimmed[matchedLength..].Trim();
            var match = ReferencePattern.Match(remainder);
            if (!match.Success)
            {
                error = GrammarHint;
                return false;
            }

            if (!int.TryParse(match.Groups["startChapter"].Value, out var startChapter))
            {
                error = GrammarHint;
                return false;
            }

            int? startVerse = null;
            if (match.Groups["startVerse"].Success)
            {
                if (!int.TryParse(match.Groups["startVerse"].Value, out var parsedStartVerse))
                {
                    error = GrammarHint;
                    return false;
                }

                startVerse = parsedStartVerse;
            }

            int endChapter;
            int? endVerse;

            if (startVerse is null)
            {
                // Whole chapter — caller resolves the actual last verse against real book data.
                endChapter = startChapter;
                endVerse = null;
            }
            else if (match.Groups["endChapter"].Success)
            {
                if (!int.TryParse(match.Groups["endChapter"].Value, out endChapter) ||
                    !int.TryParse(match.Groups["endVerse"].Value, out var parsedEndVerse))
                {
                    error = GrammarHint;
                    return false;
                }

                endVerse = parsedEndVerse;
            }
            else if (match.Groups["endVerseOnly"].Success)
            {
                if (!int.TryParse(match.Groups["endVerseOnly"].Value, out var parsedEndVerseOnly))
                {
                    error = GrammarHint;
                    return false;
                }

                endChapter = startChapter;
                endVerse = parsedEndVerseOnly;
            }
            else
            {
                // No dash at all — single verse, start == end.
                endChapter = startChapter;
                endVerse = startVerse;
            }

            result = new ParsedScriptureReference(bookCode, bookName, startChapter, startVerse, endChapter, endVerse);
            error = null;
            return true;
        }

        private static string ExtractLikelyBookToken(string trimmed)
        {
            var match = TrailingReferencePattern.Match(trimmed);
            return match.Success ? trimmed[..match.Index].Trim() : trimmed;
        }
    }
}
