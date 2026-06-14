using System.Collections.Generic;

namespace HandsLiftedApp.Core.Models.Library
{
    public record SongLibraryEntry(string Id, string Title);

    public interface ISongLibrarySource
    {
        IEnumerable<SongLibraryEntry> GetEntries();
    }

    internal record SongIndexEntry(
        string FilePath,
        string Title,
        string Copyright,
        string LyricText
    );
}
