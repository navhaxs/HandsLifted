using System.Runtime.InteropServices;

namespace HandsLiftedApp.Core.Utils;

public static class ThumbnailEngineSettings
{
    // On Windows: defaults to false (Win32 is the default path).
    // On non-Windows: always true (Win32 is unavailable).
    // Set to true at app startup to opt into mpv-backed thumbnailing on Windows.
    public static bool UseMpvEngine { get; set; } =
        !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}
