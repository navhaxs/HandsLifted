
using System;
using System.IO;

namespace HandsLiftedApp.Core.Utils
{
    public static class RelativeFilePathResolver
    {
        // avares:// URIs are Avalonia resource identifiers, not filesystem paths — relativizing
        // or re-rooting one against a playlist directory produces garbage (e.g.
        // "C:\Playlist Data\avares:\Assembly\Assets\logo.png"). Path.IsPathFullyQualified returns
        // false for them, so without this guard they fall through to the filesystem-path logic
        // below on both the save (ToRelativePath) and load (ToAbsolutePath) side.
        private static bool IsAvaresUri(string? path) =>
            path != null && path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase);

        public static string? ToAbsolutePath(string? relativeTo, string? path)
        {
            if (IsAvaresUri(path))
            {
                return path;
            }

            if (relativeTo == null || path == null || Path.IsPathFullyQualified(path))
            {
                return path;
            }

            // If the base directory is not absolute, we can't reliably resolve —
            // return the path as-is to avoid producing garbage paths
            if (!Path.IsPathFullyQualified(relativeTo))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(relativeTo, path));
        }

        public static string? ToRelativePath(string? relativeTo, string? path)
        {
            if (IsAvaresUri(path))
            {
                return path;
            }

            if (relativeTo == null || path == null)
            {
                return null;
            }

            // If the base directory is not absolute, we can't compute a meaningful
            // relative path — store the path as-is (absolute)
            if (!Path.IsPathFullyQualified(relativeTo))
            {
                return path;
            }

            return Path.GetRelativePath(relativeTo, path);
        }

        /// <summary>
        /// Resolves a media path saved by a playlist. New-format playlists store media paths
        /// relative to the configured Media Library folder; legacy playlists (saved before the
        /// Media Library feature existed) store them relative to the playlist's own folder. There
        /// is no format/version flag, so we disambiguate by trying the library first and falling
        /// back to the playlist folder when the file isn't found there.
        /// </summary>
        public static string? ToAbsoluteMediaPath(string? mediaLibraryPath, string? playlistDirectoryPath, string? path)
        {
            if (IsAvaresUri(path) || path == null || Path.IsPathFullyQualified(path))
            {
                return path;
            }

            if (mediaLibraryPath != null && Path.IsPathFullyQualified(mediaLibraryPath))
            {
                var libraryResolved = Path.GetFullPath(Path.Combine(mediaLibraryPath, path));
                if (File.Exists(libraryResolved))
                {
                    return libraryResolved;
                }
            }

            return ToAbsolutePath(playlistDirectoryPath, path);
        }

        /// <summary>
        /// True if <paramref name="path"/> lives under <paramref name="directoryPath"/>.
        /// </summary>
        public static bool IsUnderDirectory(string? directoryPath, string path)
        {
            if (directoryPath == null || !Path.IsPathFullyQualified(directoryPath))
            {
                return false;
            }

            // GetRelativePath returns `path` unchanged (still rooted) when the two are on
            // different volumes, and a leading ".." when `path` sits above `directoryPath`.
            var relative = Path.GetRelativePath(directoryPath, path);
            return !Path.IsPathRooted(relative)
                   && relative != ".."
                   && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                   && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
        }
    }
}