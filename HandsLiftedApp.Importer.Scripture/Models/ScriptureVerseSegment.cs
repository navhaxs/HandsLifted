using System.Collections.Generic;

namespace HandsLiftedApp.Importer.Scripture.Models;

public sealed record ScriptureVerseSegment(int VerseNumber, string Text, IReadOnlyList<ScriptureFootnote> Footnotes);
