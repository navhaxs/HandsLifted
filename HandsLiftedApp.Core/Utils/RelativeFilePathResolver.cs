
using System.IO;

namespace HandsLiftedApp.Core.Utils
{
    public static class RelativeFilePathResolver
    {
        public static string? ToAbsolutePath(string? relativeTo, string? path)
        {
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