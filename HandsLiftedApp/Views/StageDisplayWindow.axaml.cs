using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using HandsLiftedApp.Views.BaseWindows;
using System.Linq;

namespace HandsLiftedApp.Views
{
    public partial class StageDisplayWindow : BaseOutputWindow
    {
        public StageDisplayWindow()
        {
            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif

            if (this.Screens.ScreenCount > 1)
            {
                var secondaryScreen = this.Screens.All.Where(screen => screen.Primary == false).Last();
                this.Position = new PixelPoint(secondaryScreen.Bounds.X, secondaryScreen.Bounds.Y);
                onToggleFullscreen(true);

                // perhaps a bug, the WindowState.FullScreen needs to be set again for it to stick
                // bug observable in toggle
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.WindowState = WindowState.FullScreen;
                });
                //this.Width = this.Screens.All[1].Bounds.Width;
                //this.Height= this.Screens.All[1].Bounds.Height;
            }

        }
        //public void onToggleFullscreen(bool? fullscreen = null)
        //{
        //    bool isFullScreenNext = (fullscreen != null) ? (bool)fullscreen : (this.WindowState != WindowState.FullScreen);
        //    this.Topmost = isFullScreenNext;
        //    this.WindowState = isFullScreenNext ? WindowState.FullScreen : WindowState.Normal;
        //}

        //private void ProjectorWindow_DoubleTapped(object? sender, RoutedEventArgs e)
        //{
        //    if (IControlExtension.FindAncestor<Button>((IControl)e.Source) != null)
        //        return;

        //    toggleFullscreen();
        //}

        //public void toggleFullscreen()
        //{
        //    this.WindowState = (this.WindowState == WindowState.FullScreen) ? WindowState.Normal : WindowState.FullScreen;
        //}
    }
}
