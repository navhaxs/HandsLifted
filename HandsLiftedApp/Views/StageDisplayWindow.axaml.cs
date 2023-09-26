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
            }

        }
    }
}
