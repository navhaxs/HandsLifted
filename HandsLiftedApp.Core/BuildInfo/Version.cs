using System.IO;
using System.Reflection;

namespace HandsLiftedApp.Core.BuildInfo
{
    static class Version
    {
        public static string GetGitHash()
        {
            var result = string.Empty;
            using (Stream? stream = Assembly.GetExecutingAssembly()
                       .GetManifestResourceStream("HandsLiftedApp.Core.BuildInfo.GitHash.txt"))
                if (stream != null)
                {
                    using StreamReader reader = new StreamReader(stream);
                    result = reader.ReadToEnd();
                }

            return result.Trim();
        }

        public static string GetBuildDateTime()
        {
            var result = string.Empty;
            using (Stream? stream = Assembly.GetExecutingAssembly()
                       .GetManifestResourceStream("HandsLiftedApp.Core.BuildInfo.BuildDateTime.txt"))
                if (stream != null)
                {
                    using StreamReader reader = new StreamReader(stream);
                    result = reader.ReadToEnd();
                }

            return result.Trim();
        }
    }
}