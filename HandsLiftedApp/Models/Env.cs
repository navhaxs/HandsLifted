using System.IO;

namespace HandsLiftedApp.Models
{
    public class Env
    {

        // TODO: move this to user preferences UI
        public string GoogleApiKey { get; set; }

        // TODO: move this to user preferences UI
        public string SongLibraryDirectory { get; set; } = @"C:\VisionScreens\Songs";

        public string TempDirectory { get; set; } = Path.Join(System.IO.Path.GetTempPath(), "VisionScreensAppTemp");

    }
}
