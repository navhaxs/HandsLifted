using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using HandsLiftedApp.Utils;

namespace HandsLiftedApp.Views.Editor
{
    public partial class SlideInfoWindow : Window
    {
        public SlideInfoWindow()
        {
            InitializeComponent();

            CancelButton.Click += (s, e) => Close();

            this.Loaded += (e, s) => {
                updateWin32Border(this.WindowState);
            };

            this.GetObservable(Window.WindowStateProperty)
                .Subscribe(v => updateWin32Border(v));
        }

        void updateWin32Border(WindowState v) {
            if (v != WindowState.Maximized) {

                var margins = new Win32.MARGINS {
                    cyBottomHeight = 1,
                    cxRightWidth = 1,
                    cxLeftWidth = 1,
                    cyTopHeight = 1
                };

                Win32.DwmExtendFrameIntoClientArea(this.TryGetPlatformHandle().Handle, ref margins);
            }
        }
    }
}
