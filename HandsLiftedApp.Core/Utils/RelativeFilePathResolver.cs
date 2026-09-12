
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
    }
}