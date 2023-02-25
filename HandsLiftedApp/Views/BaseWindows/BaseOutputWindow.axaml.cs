using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Extensions;

namespace HandsLiftedApp.Views.BaseWindows
{
    public partial class BaseOutputWindow : Window
    {
        public BaseOutputWindow()
        {
            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif

            //OverlayControls = this.FindControl<Grid>("OverlayControls");
            this.FindControl<MenuItem>("toggleFullscreen").Click += (s, e) =>
            {
                onToggleFullscreen();
            };
            this.FindControl<MenuItem>("toggleTopmost").Click += (s, e) =>
            {
                this.Topmost = !this.Topmost;
            };
            this.FindControl<MenuItem>("close").Click += (s, e) =>
            {
                Close();
            };

            this.DoubleTapped +=
                (s, e) =>
                {
                    if (ControlExtension.FindAncestor<Button>((Control)e.Source) != null)
                        return;

                    onToggleFullscreen();
                };
        }
        public void onToggleFullscreen(bool? fullscreen = null)
        {
            bool isFullScreenNext = (fullscreen != null) ? (bool)fullscreen : (this.WindowState != WindowState.FullScreen);
            this.WindowState = isFullScreenNext ? WindowState.FullScreen : WindowState.Normal;
            this.Topmost = isFullScreenNext;
        }
    }
}
