using System.Runtime.InteropServices;

namespace HandsLiftedApp.Utils
{
    internal static class ConsoleUtils
    {
        [DllImport("Kernel32")]
        public static extern void AllocConsole();

        [DllImport("Kernel32")]
        public static extern void FreeConsole();

    }
}
