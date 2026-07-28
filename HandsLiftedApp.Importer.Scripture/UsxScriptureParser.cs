using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HandsLiftedApp.Importer.Scripture.Models;

namespace HandsLiftedApp.Importer.Scripture;

public static class UsxScriptureParser
{
    public static ScriptureBook Parse(XDocument document)
    {
        var root = document.Root ?? throw new InvalidOperationException("USX document does not have a root element.");
        var bookElement = root.Elements().FirstOrDefault(x => x.Name.LocalName == "book");
        var mt1Element = root.Elements().FirstOrDefault(x =>
            x.Name.LocalName == "para" && x.Attribute("style")?.Value == "mt1");

        var code = bookElement?.Attribute("code")?.Value ?? "UNK";

        var titleText = CollapseWhitespace(mt1Element?.Value) is { Length: > 0 } mt1Title
            ? mt1Title
            : CollapseWhitespace(bookElement?.Value);
        var title = string.IsNullOrEmpty(titleText) ? code : titleText;

        var paragraphs = new List<ScriptureParagraph>();
        var currentChapter = 1;
        var accumulator = new ParagraphAccumulator();

        foreach (var element in root.Elements())
        {
            if (element.Name.LocalName == "chapter" && element.Attribute("number") is not null)
            {
                if (int.TryParse(element.Attribute("number")?.Value, out var parsedChapter))
                {
                    currentChapter = parsedChapter;
                }

                continue;
            }

            if (element.Name.LocalName != "para")
            {
                continue;
            }

            var style = element.Attribute("style")?.Value;
            if (IsNonVerseStyle(style))
            {
                continue;
            }

            foreach (var node in element.Nodes())
            {
                AppendNode(node, accumulator);
            }

            accumulator.Flush();

            if (accumulator.Verses.Count > 0)
            {
                paragraphs.Add(new ScriptureParagraph(
                    currentChapter,
                    IsPoetryStyle(style),
                    GetPoetryIndentLevel(style),
                    accumulator.Verses.ToList()));
            }

            accumulator.Verses.Clear();
        }

        return new ScriptureBook(code, title, paragraphs);
    }

    private static bool IsNonVerseStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return false;
        }

        // Front matter, navigation metadata, and section headings are not scripture text.
        return style.Equals("h", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("toc", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("mt", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("imt", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("s", StringComparison.OrdinalIgnoreCase)
            || style is "d" or "mr" or "ms" or "r" or "usfm";
    }

    private static bool IsPoetryStyle(string? style) => style is "q1" or "q2" or "q3" or "qa" or "qr" or "qc";

    private static int GetPoetryIndentLevel(string? style) => style switch
    {
        "q1" or "qa" or "qc" => 1,
        "q2" or "qr" => 2,
        "q3" => 3,
        _ => 0
    };

    private static void AppendNode(XNode node, ParagraphAccumulator accumulator)
    {
        if (node is XText textNode)
        {
            accumulator.AppendText(CollapseWhitespace(textNode.Value));
            return;
        }

        if (node is not XElement element)
        {
            return;
        }

        if (element.Name.LocalName == "verse")
        {
            if (element.Attribute("eid") is not null)
            {
                return;
            }

            var verseNumber = element.Attribute("number")?.Value;
            if (string.IsNullOrWhiteSpace(verseNumber) || !int.TryParse(verseNumber, out var parsedVerse))
            {
                return;
            }

            accumulator.StartVerse(parsedVerse);
            return;
        }

        if (element.Name.LocalName == "note")
        {
            var footnoteText = ExtractFootnoteText(element);
            if (!string.IsNullOrWhiteSpace(footnoteText))
            {
                accumulator.AddFootnote(footnoteText);
            }

            return;
        }

        foreach (var child in element.Nodes())
        {
            AppendNode(child, accumulator);
        }
    }

    private static string ExtractFootnoteText(XElement noteElement)
    {
        var ftChunks = noteElement
            .Descendants()
            .Where(x => x.Name.LocalName == "char" && x.Attribute("style")?.Value == "ft")
            .Select(x => CollapseWhitespace(x.Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return ftChunks.Count > 0 ? string.Join(' ', ftChunks) : CollapseWhitespace(noteElement.Value);
    }

    private static string CollapseWhitespace(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return string.Join(' ', input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsClosingPunctuation(char value) =>
        value is '.' or ',' or ';' or ':' or '!' or '?' or ')' or ']' or '}' or '"' or '\'';

    // Carries verse-number state across paragraph boundaries (a verse's text can span
    // multiple <para> elements — see Parse_BsbShapedUsx_SplitsVerseFiveTextAcrossTwoParagraphs)
    // while collecting only the current paragraph's segments in Verses.
    private sealed class ParagraphAccumulator
    {
        private readonly StringBuilder _text = new();
        private List<ScriptureFootnote> _footnotes = new();
        private int _currentVerse;

        public List<ScriptureVerseSegment> Verses { get; } = new();

        public void AppendText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (_text.Length > 0 && !char.IsWhiteSpace(_text[^1]) && !IsClosingPunctuation(text[0]))
            {
                _text.Append(' ');
            }

            _text.Append(text);
        }

        public void StartVerse(int verseNumber)
        {
            Flush();
            _currentVerse = verseNumber;
        }

        public void AddFootnote(string text)
        {
            _footnotes.Add(new ScriptureFootnote((_footnotes.Count + 1).ToString(), text));
        }

        public void Flush()
        {
            if (_currentVerse > 0 && _text.Length > 0)
            {
                Verses.Add(new ScriptureVerseSegment(_currentVerse, _text.ToString().Trim(), _footnotes));
                _footnotes = new List<ScriptureFootnote>();
            }

            _text.Clear();
        }
    }
}
