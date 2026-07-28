using System;

namespace HandsLiftedApp.Importer.Scripture;

public sealed class ScriptureBookNotFoundException : Exception
{
    public string BookCode { get; }

    public string ExpectedPath { get; }

    public ScriptureBookNotFoundException(string bookCode, string expectedPath)
        : base($"Scripture book '{bookCode}' not found at '{expectedPath}'.")
    {
        BookCode = bookCode;
        ExpectedPath = expectedPath;
    }
}
