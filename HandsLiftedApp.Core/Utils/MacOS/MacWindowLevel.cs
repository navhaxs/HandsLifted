using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace HandsLiftedApp.Core.Utils.MacOS
{
    public static class MacWindowLevel
    {
        // CGWindowLevelKey values (subset)
        const int kCGNormalWindowLevelKey = 0;
        const int kCGScreenSaverWindowLevelKey = 13;

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        static extern nint CGWindowLevelForKey(int key);

        [DllImport("/usr/lib/libobjc.A.dylib")]
        static extern IntPtr sel_registerName(string name);

        // Import objc_msgSend with the exact signature we need (void setLevel:(NSInteger))
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        static extern void void_objc_msgSend_IntPtr_nint(IntPtr receiver, IntPtr selector, nint arg);
        
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        static extern nint nint_objc_msgSend(IntPtr receiver, IntPtr selector);
        
        class Holder { public nint Level; public bool Saved; }

        // Keep the original level per window so we can restore exactly.
        static readonly ConditionalWeakTable<Window, Holder> OriginalLevels = new();

        public static void RaiseAboveDock(Window window)
        {
            if (!OperatingSystem.IsMacOS())
                return;
            
            var handle = window.TryGetPlatformHandle();
            if (handle?.Handle == IntPtr.Zero || handle?.HandleDescriptor != "NSWindow")
                return;

            var setLevelSel = sel_registerName("setLevel:");
            
            // Save the original level once.
            var h = OriginalLevels.GetOrCreateValue(window);
            if (!h.Saved)
            {
                var levelSel = sel_registerName("level");
                h.Level = nint_objc_msgSend(handle.Handle, levelSel);
                h.Saved = true;
            }

            var level = CGWindowLevelForKey(kCGScreenSaverWindowLevelKey);
            void_objc_msgSend_IntPtr_nint(handle.Handle, setLevelSel, level);
        }

        public static void RestoreToNormal(Window window)
        {
            if (!OperatingSystem.IsMacOS())
                return;

            var handle = window.TryGetPlatformHandle();
            if (handle?.Handle == IntPtr.Zero || handle?.HandleDescriptor != "NSWindow")
                return;

            var setLevelSel = sel_registerName("setLevel:");
            
            // Prefer restoring the exact original level if we saved it.
            if (OriginalLevels.TryGetValue(window, out var h) && h.Saved)
            {
                void_objc_msgSend_IntPtr_nint(handle.Handle, setLevelSel, h.Level);
            }
            else
            {
                // Fall back to the canonical normal level.
                // A typical “normal” level sits below the main menu level
                var normal = CGWindowLevelForKey(kCGNormalWindowLevelKey);
                void_objc_msgSend_IntPtr_nint(handle.Handle, setLevelSel, normal);
            }

        }
    }
}