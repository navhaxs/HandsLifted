using System.Runtime.InteropServices;

namespace HandsLiftedApp.Utils
{
    public static class ConsoleUtils
    {
        [DllImport("Kernel32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();

        [DllImport("Kernel32")]
        public static extern void FreeConsole();
    }
}
