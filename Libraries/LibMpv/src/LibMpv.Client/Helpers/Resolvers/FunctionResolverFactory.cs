using LibMpv.Client.Native;
using System.Runtime.InteropServices;

namespace LibMpv.Client;

public static class FunctionResolverFactory
{
    public static PlatformID GetPlatformId()
    {
        return Environment.OSVersion.Platform;
    }

    public static IFunctionResolver Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
                return new MacFunctionResolver();
        }
        // var os = System.Environment.OSVersion;
        switch (GetPlatformId())
        {
            // case PlatformID.MacOSX:
            case PlatformID.Unix:
                {
                    var isAndroid = RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID"));
                    return isAndroid ? new AndroidFunctionResolver() : new LinuxFunctionResolver();
                }
            case PlatformID.Win32NT:
                return new WindowsFunctionResolver();
            default:
                throw new PlatformNotSupportedException();
        }
    }
}
