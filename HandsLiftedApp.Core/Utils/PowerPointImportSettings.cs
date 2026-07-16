using System.Runtime.InteropServices;

namespace HandsLiftedApp.Core.Utils;

public static class PowerPointImportSettings
{
    // Windows-only native COM path. Default false = opt-in required.
    // Non-Windows: always false regardless of setter (COM unavailable).
    public static bool UseNativeInterop
    {
        get => _useNativeInterop && RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        set => _useNativeInterop = value;
    }

    private static bool _useNativeInterop = false;
}
