using System;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Utils;

namespace HandsLiftedApp.Controls
{
    public static class Win10DropshadowWorkaround
    {
        public static void Register(Window window)
        {
            if (!OperatingSystem.IsWindows() || Environment.OSVersion.Version.Build >= 22000)
            {
                return;
            }
            
            window.Loaded += (e, s) =>
            {
                updateWin32Border(window.WindowState);
            };

            window.GetObservable(Window.WindowStateProperty)
                .Subscribe((WindowState v) =>
                {
                    updateWin32Border(v);
                });

            void updateWin32Border(WindowState v)
            {
                if (v != WindowState.Maximized)
                {

                    var margins = new Win32.MARGINS
                    {
                        cyBottomHeight = 1,
                        cxRightWidth = 1,
                        cxLeftWidth = 1,
                        cyTopHeight = 1
                    };

                    Win32.DwmExtendFrameIntoClientArea(window.TryGetPlatformHandle().Handle, ref margins);
                }
            }

        }
    }
}