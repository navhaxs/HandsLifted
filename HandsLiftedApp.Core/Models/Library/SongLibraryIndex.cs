using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reactive.Subjects;
using System.Xml.Serialization;
using DebounceThrottle;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Utils;
using Serilog;

namespace HandsLiftedApp.Core.Models.Library
{
    /// <summary>
    /// App-wide, UUID-keyed cache of library SongItems, shared across every configured
    /// SongLibrary. Playlist SongItemInstance facades resolve against this instead of
    /// holding song content locally, so an edit made from one playlist is immediately
    /// visible from any other playlist referencing the same song (write-through).
    /// </summary>
    public class SongLibraryIndex
    {
        private sealed record Entry(SongItem Song, string FilePath, string LibraryDirectory);

        private readonly ConcurrentDictionary<Guid, Entry> _byId = new();
        private readonly ConcurrentDictionary<Guid, DebounceDispatcher> _saveDebouncers = new();
        private readonly Subject<Guid> _songChanged = new();
        private readonly Subject<(Guid Id, Exception Error)> _saveFailed = new();

        public IObservable<Guid> SongChanged => _songChanged;

        /// <summary>
        /// Fires when a debounced write-through save to disk fails. No generic app-wide
        /// error-toast mechanism exists in this codebase today (confirmed: the closest
        /// precedent, commit f5d8e99, is a bespoke Google Slides reauth dialog, not
        /// reusable infra) — this hook exists so UI code can subscribe and surface it
        /// later without SongLibraryIndex itself taking a UI dependency. Until something
        /// subscribes, failures are still visible via Log.Error.
        /// </summary>
        public IObservable<(Guid Id, Exception Error)> SaveFailed => _saveFailed;

        public SongItem? Resolve(Guid id) => _byId.TryGetValue(id, out var entry) ? entry.Song : null;

        /// <summary>
        /// Registers a SongItem already known to have a file at filePath (e.g. from a
        /// SongLibrary scan, or SongItemInstance.AttachToLibrary). Resolves
        /// MotionBackgroundVideoPath to an absolute path (it's stored relative to the
        /// library directory on disk).
        ///
        /// If this UUID already has a live cached object (e.g. attached moments earlier by
        /// the Song Editor, and this call is a subsequent SongLibrary rescan re-parsing the
        /// same file from disk), the existing object identity wins — only its file/directory
        /// bookkeeping is refreshed. This keeps in-flight edits pointed at the one object
        /// actually being edited/rendered, instead of silently swapping them onto a
        /// content-equal but distinct re-parsed copy the next time a library scan runs.
        /// </summary>
        public void Register(SongItem song, string filePath, string libraryDirectory)
        {
            if (_byId.TryGetValue(song.UUID, out var existing))
            {
                _byId[song.UUID] = existing with { FilePath = filePath, LibraryDirectory = libraryDirectory };
                return;
            }

            if (!string.IsNullOrEmpty(song.MotionBackgroundVideoPath))
            {
                song.MotionBackgroundVideoPath =
                    RelativeFilePathResolver.ToAbsolutePath(libraryDirectory, song.MotionBackgroundVideoPath);
            }

            _byId[song.UUID] = new Entry(song, filePath, libraryDirectory);
        }

        /// <summary>
        /// Registers content that has no known library file yet — a brand-new song, or an
        /// old-format playlist's inline song data being migrated — by writing it to a new
        /// file under libraryDirectory and then registering it normally.
        /// </summary>
        public void ImportAndCache(SongItem song, string libraryDirectory)
        {
            var fileName = FilenameUtils.ReplaceInvalidChars(
                string.IsNullOrWhiteSpace(song.Title) ? song.UUID.ToString() : song.Title) + ".xml";
            var filePath = Path.Combine(libraryDirectory, fileName);

            SaveToDisk(song.UUID, song, filePath, libraryDirectory);
            _byId[song.UUID] = new Entry(song, filePath, libraryDirectory);
        }

        /// <summary>
        /// Call after mutating a resolved SongItem's content. Notifies every
        /// SongItemInstance referencing this UUID to re-raise PropertyChanged and
        /// re-render, and debounces a save of the new content to disk.
        /// </summary>
        public void NotifyChanged(Guid id)
        {
            _songChanged.OnNext(id);

            if (!_byId.TryGetValue(id, out var entry))
                return;

            var debouncer = _saveDebouncers.GetOrAdd(id, _ => new DebounceDispatcher(500));
            debouncer.Debounce(() => SaveToDisk(id, entry.Song, entry.FilePath, entry.LibraryDirectory));
        }

        private void SaveToDisk(Guid id, SongItem song, string filePath, string libraryDirectory)
        {
            try
            {
                // MotionBackgroundVideoPath is held in-memory as an absolute path (see
                // Register); store it relative to the library directory so the file stays
                // portable, then restore the absolute path afterward for continued use.
                var absoluteMotionBgPath = song.MotionBackgroundVideoPath;
                if (!string.IsNullOrEmpty(absoluteMotionBgPath) && Path.IsPathFullyQualified(absoluteMotionBgPath))
                {
                    song.MotionBackgroundVideoPath =
                        RelativeFilePathResolver.ToRelativePath(libraryDirectory, absoluteMotionBgPath);
                }

                var serializer = new XmlSerializer(typeof(SongItem));
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    serializer.Serialize(stream, song);
                }

                song.MotionBackgroundVideoPath = absoluteMotionBgPath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SongLibraryIndex: failed to save {FilePath}", filePath);
                _saveFailed.OnNext((id, ex));
            }
        }
    }
}
