using System;
using Avalonia.Controls;
using Avalonia.Platform;
using HandsLiftedApp.Core.Utils.MacOS;
using HandsLiftedApp.Utils;

namespace HandsLiftedApp.Core.Views.Setup
{
    public partial class DisplayIdentifyWindow : Window
    {
        public Screen Screen { get; set; }

        public DisplayIdentifyWindow()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
                return;

            this.PointerPressed += DisplayIdentifyWindow_PointerPressed;
            this.Opened += DisplayIdentifyWindow_Opened;

            MacWindowLevel.RaiseAboveDock(this);
        }

        private void DisplayIdentifyWindow_Opened(object? sender, EventArgs e)
        {
            this.Height = Screen.Bounds.Height;
            this.Width = Screen.Bounds.Width;

            this.Position = Screen.Bounds.Position;

            if (OperatingSystem.IsWindows())
            {
                IntPtr? handle = this.TryGetPlatformHandle()?.Handle;
                if (handle == null)
                    return;

                var style = Win32.GetWindowLong((IntPtr)handle, Win32.GWL_EX_STYLE);
                Win32.SetWindowLong((IntPtr)handle, Win32.GWL_EX_STYLE,
                    style | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT);
                Win32.SetLayeredWindowAttributes((IntPtr)handle, 0, 255, 0x2);
            }
            else if (OperatingSystem.IsMacOS())
            {
                MacClickThrough.SetClickThrough(this, true);
            }
        }

        private void DisplayIdentifyWindow_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            Close();
        }
    }
}