using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Extensions;

namespace HandsLiftedApp.Views
{
    public partial class StageDisplayWindow : Window
    {
        public StageDisplayWindow()
        {
            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ProjectorWindow_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (IControlExtension.FindAncestor<Button>((IControl)e.Source) != null)
                return;

            toggleFullscreen();
        }

        public void toggleFullscreen()
        {
            this.WindowState = (this.WindowState == WindowState.FullScreen) ? WindowState.Normal : WindowState.FullScreen;
        }
    }
}
