using System.Collections.Generic;

namespace HandsLiftedApp.Importer.Scripture.Models;

public sealed record ScriptureParagraph(int StartChapter, bool IsVerseContinuation, bool IsPoetry, int PoetryIndentLevel, IReadOnlyList<ScriptureVerseSegment> Verses);
