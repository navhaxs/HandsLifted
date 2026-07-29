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

            var startChapter = int.Parse(match.Groups["startChapter"].Value);
            int? startVerse = match.Groups["startVerse"].Success ? int.Parse(match.Groups["startVerse"].Value) : null;

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
                endChapter = int.Parse(match.Groups["endChapter"].Value);
                endVerse = int.Parse(match.Groups["endVerse"].Value);
            }
            else if (match.Groups["endVerseOnly"].Success)
            {
                endChapter = startChapter;
                endVerse = int.Parse(match.Groups["endVerseOnly"].Value);
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
