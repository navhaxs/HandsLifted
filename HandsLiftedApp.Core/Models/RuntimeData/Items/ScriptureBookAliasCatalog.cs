using System;
using System.Collections.Generic;
using System.Linq;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public static class ScriptureBookAliasCatalog
    {
        // One common abbreviation per book, positionally parallel to ScriptureBookCatalog.AllBooks
        // (i.e. to ScriptureBookCatalog's own internal Names array, same 66-book canonical order).
        private static readonly string[] Abbreviations =
        {
            "Gen", "Exo", "Lev", "Num", "Deut", "Josh", "Judg", "Ruth", "1 Sam", "2 Sam",
            "1 Kgs", "2 Kgs", "1 Chr", "2 Chr", "Ezra", "Neh", "Esth", "Job", "Ps", "Prov",
            "Eccl", "Song", "Isa", "Jer", "Lam", "Ezek", "Dan", "Hos", "Joel", "Amos",
            "Obad", "Jonah", "Mic", "Nah", "Hab", "Zeph", "Hag", "Zech", "Mal",
            "Matt", "Mark", "Luke", "John", "Acts", "Rom", "1 Cor", "2 Cor", "Gal", "Eph",
            "Phil", "Col", "1 Thess", "2 Thess", "1 Tim", "2 Tim", "Titus", "Phlm", "Heb", "Jas",
            "1 Pet", "2 Pet", "1 Jn", "2 Jn", "3 Jn", "Jude", "Rev"
        };

        private static readonly IReadOnlyList<(string Alias, string Code)> AllAliases = BuildAliases();

        private static List<(string Alias, string Code)> BuildAliases()
        {
            var books = ScriptureBookCatalog.AllBooks;
            var result = new List<(string, string)>();

            for (var i = 0; i < books.Count; i++)
            {
                foreach (var variant in ExpandNumericVariants(books[i].Name))
                {
                    result.Add((variant, books[i].Code));
                }

                foreach (var variant in ExpandNumericVariants(Abbreviations[i]))
                {
                    result.Add((variant, books[i].Code));
                }
            }

            // Longest alias first: TryMatchBookPrefix takes the first match it finds, so a
            // longer, more specific alias (e.g. "1 John") must be tried before a shorter one
            // that could otherwise match a truncated prefix of it (e.g. "John").
            return result.OrderByDescending(a => a.Item1.Length).ToList();
        }

        private static IEnumerable<string> ExpandNumericVariants(string name)
        {
            yield return name;

            if (name.StartsWith("1 ", StringComparison.Ordinal))
            {
                yield return "I " + name[2..];
                yield return "First " + name[2..];
            }
            else if (name.StartsWith("2 ", StringComparison.Ordinal))
            {
                yield return "II " + name[2..];
                yield return "Second " + name[2..];
            }
            else if (name.StartsWith("3 ", StringComparison.Ordinal))
            {
                yield return "III " + name[2..];
                yield return "Third " + name[2..];
            }
        }

        public static bool TryMatchBookPrefix(string input, out string bookCode, out string bookName, out int matchedLength)
        {
            foreach (var (alias, code) in AllAliases)
            {
                if (input.Length < alias.Length) continue;
                if (!input.AsSpan(0, alias.Length).Equals(alias, StringComparison.OrdinalIgnoreCase)) continue;
                if (input.Length > alias.Length && char.IsLetter(input[alias.Length])) continue;

                bookCode = code;
                bookName = ScriptureBookCatalog.AllBooks.Single(b => b.Code == code).Name;
                matchedLength = alias.Length;
                return true;
            }

            bookCode = "";
            bookName = "";
            matchedLength = 0;
            return false;
        }
    }
}
