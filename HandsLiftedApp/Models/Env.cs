using System.IO;

namespace HandsLiftedApp.Models
{
    public class Env
    {
        public string GoogleApiKey { get; set; }
        public string TempDirectory { get; set; } = Path.Join(System.IO.Path.GetTempPath(), "VisionScreensAppTemp");

    }
}
