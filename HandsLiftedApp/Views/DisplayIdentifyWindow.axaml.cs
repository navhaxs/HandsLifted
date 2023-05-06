using Avalonia.Controls;
using System;
using System.Runtime.InteropServices;
using HandsLiftedApp.Utils;

namespace HandsLiftedApp.Views
{
    public partial class DisplayIdentifyWindow : Window
    {
        public DisplayIdentifyWindow()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
                return;

            this.PointerPressed += DisplayIdentifyWindow_PointerPressed;
            this.Opened += DisplayIdentifyWindow_Opened;
        }

        private void DisplayIdentifyWindow_Opened(object? sender, EventArgs e)
        {
            IntPtr? handle = this.TryGetPlatformHandle()?.Handle;
            if (handle == null)
                return;

            var style = Win32.GetWindowLong((IntPtr)handle, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong((IntPtr)handle, Win32.GWL_EXSTYLE, style | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT);
            Win32.SetLayeredWindowAttributes((IntPtr)handle, 0, 255, 0x2);
        }

        private void DisplayIdentifyWindow_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            Close();
        }
    }
}
