using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace HandsLiftedApp.Core.Utils.MacOS
{
    public static class MacClickThrough
    {
        [DllImport("/usr/lib/libobjc.A.dylib")]
        private static extern IntPtr sel_registerName(string name);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern void void_objc_msgSend_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg);

        /// <summary>
        /// Enables or disables click-through for the entire window.
        /// </summary>
        public static void SetClickThrough(Window window, bool enabled)
        {
            var handle = window.TryGetPlatformHandle();
            if (handle?.Handle == IntPtr.Zero || handle.HandleDescriptor != "NSWindow")
                return;

            // NSWindow selector: - (void)setIgnoresMouseEvents:(BOOL)flag
            var setSel = sel_registerName("setIgnoresMouseEvents:");
            void_objc_msgSend_bool(handle.Handle, setSel, enabled);
        }

    }
}