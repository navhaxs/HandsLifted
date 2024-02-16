using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Extensions;

namespace HandsLiftedApp.Core.Views
{
    public partial class ProjectorWindow : Window
    {
        public ProjectorWindow()
        {
            InitializeComponent();
        }
        
        private void ProjectorWindow_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (ControlExtension.FindAncestor<Button>((Control)e.Source) != null)
                return;

            onToggleFullscreen();
        }
        
        public void onToggleFullscreen(bool? fullscreen = null)
        {
            bool isFullScreenNext = (fullscreen != null) ? (bool)fullscreen : (this.WindowState != WindowState.FullScreen);
            this.WindowState = isFullScreenNext ? WindowState.FullScreen : WindowState.Normal;
            //this.Topmost = isFullScreenNext;
        }

    }
}