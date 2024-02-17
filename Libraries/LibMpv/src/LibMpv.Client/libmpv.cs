using System.Runtime.InteropServices;

namespace LibMpv.Client;

public partial class libmpv
{
    public static readonly int EAGAIN;

    public static readonly int ENOMEM = 12;

    public static readonly int EINVAL = 22;

    public static readonly int EPIPE = 32;

    static libmpv()
    {
        EAGAIN = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? 35
            : 11
        ;

        DynamicallyLoadedBindings.Initialize();
    }


    /// <summary>
    ///     Gets or sets the root path for loading libraries.
    /// </summary>
    /// <value>The root path.</value>
    public static string RootPath { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

}
