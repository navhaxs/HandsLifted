using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HandsLiftedApp.Comparer;

namespace HandsLiftedApp.Core.Models.Library
{
    public class FileSystemSongLibrarySource : ISongLibrarySource
    {
        private readonly string _directory;
        public FileSystemSongLibrarySource(string directory) => _directory = directory;

        public IEnumerable<SongLibraryEntry> GetEntries()
        {
            if (!Directory.Exists(_directory))
                return Enumerable.Empty<SongLibraryEntry>();

            return new DirectoryInfo(_directory)
                .GetFiles("*.*", SearchOption.TopDirectoryOnly)
                .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                .Where(f => { var ext = f.Extension.ToLower(); return ext == ".txt" || ext == ".xml"; })
                .OrderBy(f => f.FullName, new NaturalSortStringComparer(StringComparison.Ordinal))
                .Select(f => new SongLibraryEntry(Id: f.FullName, Title: f.Name));
        }
    }
}
