using System.Collections.Generic;

namespace HandsLiftedApp.Importer.Scripture.Models;

public sealed class ScriptureBook
{
    public ScriptureBook(string code, string title, IReadOnlyList<ScriptureParagraph> paragraphs)
    {
        Code = code;
        Title = title;
        Paragraphs = paragraphs;
    }

    public string Code { get; }

    public string Title { get; }

    public IReadOnlyList<ScriptureParagraph> Paragraphs { get; }
}
