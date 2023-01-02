using Avalonia.Controls;
using System;
using System.Runtime.InteropServices;

namespace HandsLiftedApp.Views
{
    public partial class DisplayIdentifyWindow : Window
    {

        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern long SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public const int GWL_EXSTYLE = -20;
        public const int GWL_STYLE = -16;
        public const int WS_EX_LAYERED = 0x80000;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public DisplayIdentifyWindow()
        {
            InitializeComponent();

            this.PointerPressed += DisplayIdentifyWindow_PointerPressed;

            this.Opened += DisplayIdentifyWindow_Opened;
        }

        private void DisplayIdentifyWindow_Opened(object? sender, EventArgs e)
        {

            IntPtr handle = this.PlatformImpl.Handle.Handle;
            var style = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(this.PlatformImpl.Handle.Handle, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            SetLayeredWindowAttributes(this.PlatformImpl.Handle.Handle, 0, 255, 0x2);
        }

        private void DisplayIdentifyWindow_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            Close();
        }
    }
}
