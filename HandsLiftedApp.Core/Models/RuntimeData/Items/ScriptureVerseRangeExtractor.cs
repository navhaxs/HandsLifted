using System.Collections.Generic;
using HandsLiftedApp.Importer.Scripture.Models;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public readonly record struct ScriptureVerseRef(int Chapter, int Verse, string Text);

    public static class ScriptureVerseRangeExtractor
    {
        public static List<ScriptureVerseRef> Extract(
            ScriptureBook book, int startChapter, int startVerse, int endChapter, int endVerse)
        {
            var result = new List<ScriptureVerseRef>();

            foreach (var paragraph in book.Paragraphs)
            {
                for (var i = 0; i < paragraph.Verses.Count; i++)
                {
                    var segment = paragraph.Verses[i];

                    if (!IsWithinRange(paragraph.StartChapter, segment.VerseNumber, startChapter, startVerse, endChapter, endVerse))
                    {
                        continue;
                    }

                    var isContinuationOfPrevious = i == 0
                        && paragraph.IsVerseContinuation
                        && result.Count > 0
                        && result[^1].Chapter == paragraph.StartChapter
                        && result[^1].Verse == segment.VerseNumber;

                    if (isContinuationOfPrevious)
                    {
                        var previous = result[^1];
                        result[^1] = previous with { Text = previous.Text + " " + segment.Text };
                    }
                    else
                    {
                        result.Add(new ScriptureVerseRef(paragraph.StartChapter, segment.VerseNumber, segment.Text));
                    }
                }
            }

            return result;
        }

        private static bool IsWithinRange(
            int chapter, int verse, int startChapter, int startVerse, int endChapter, int endVerse)
        {
            if (chapter < startChapter || chapter > endChapter)
            {
                return false;
            }

            if (chapter == startChapter && verse < startVerse)
            {
                return false;
            }

            if (chapter == endChapter && verse > endVerse)
            {
                return false;
            }

            return true;
        }
    }
}
