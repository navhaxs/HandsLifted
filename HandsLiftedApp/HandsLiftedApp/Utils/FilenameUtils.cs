using System.IO;

namespace HandsLiftedApp.Utils
{
    public static class FilenameUtils
    {
        public static string ReplaceInvalidChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
