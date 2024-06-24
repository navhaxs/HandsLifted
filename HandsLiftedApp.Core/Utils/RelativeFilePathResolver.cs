
using System.IO;

namespace HandsLiftedApp.Core.Utils
{
    public static class RelativeFilePathResolver
    {
        public static string ToAbsolutePath(string relativeTo, string path)
        {
            if (Path.IsPathFullyQualified(path))
            {
                return path;
            }
            else
            {
                return Path.Combine(relativeTo, path);
            }
        }

        public static string ToRelativePath(string relativeTo, string path)
        {
            return Path.GetRelativePath(relativeTo, path);
        }
    }
}