using System.IO;
using System.Reflection;

namespace HandsLiftedApp.BuildInfo
{
    static class Version
    {
        static public string getGitHash()
        {
            string result = string.Empty;
            using (Stream stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("HandsLiftedApp.BuildInfo.GitHash.txt"))
            using (StreamReader reader = new StreamReader(stream))
            {
                result = reader.ReadToEnd();
            }
            return result.Trim();
        }

        static public string getBuildDate()
        {
            string result = string.Empty;
            using (Stream stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("HandsLiftedApp.BuildInfo.BuildDate.txt"))
            using (StreamReader reader = new StreamReader(stream))
            {
                result = reader.ReadToEnd();
            }
            return result.Trim();
        }
    }
}
