using Avalonia.Controls;
using Avalonia.Input;
using HandsLiftedApp.Core.Controller;
using HandsLiftedApp.Extensions;

namespace HandsLiftedApp.Core.Views
{
    public partial class ProjectorWindow : Window
    {
        private bool _isUserEvent = false;
        public ProjectorWindow()
        {
            InitializeComponent();
            this.PositionChanged += (sender, args) =>
            {
                // TODO dont use flag... actually check for PixelPointEventArgs and see if monitor has changed
                if (!_isUserEvent && this.WindowState == WindowState.Maximized)
                {
                    this.WindowState = WindowState.Normal;
                    this.WindowState = WindowState.Maximized;
                }
            };
        }
        
        private void ProjectorWindow_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (ControlExtension.FindAncestor<Button>((Control)e.Source) != null)
                return;

            onToggleFullscreen();
        }
        
        public void onToggleFullscreen(bool? fullscreen = null)
        {
            _isUserEvent = true;
            bool isFullScreenNext = (fullscreen != null) ? (bool)fullscreen : (this.WindowState != WindowState.FullScreen);
            this.WindowState = isFullScreenNext ? WindowState.FullScreen : WindowState.Normal;
            //this.Topmost = isFullScreenNext;
            _isUserEvent = false;
        }


        private void ProjectorWindow_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            // var window = TopLevel.GetTopLevel(this);
            //
            // // if (!window.IsFocused)
            // // {
            // //     return;
            // // }
            //
            // // TODO: if a textbox, datepicker etc is selected - then skip this func.
            // var focusManager = TopLevel.GetTopLevel(this).FocusManager;
            // var focusedElement = focusManager.GetFocusedElement();
            //
            // if (focusedElement is TextBox || focusedElement is DatePicker)
            //     return;

            KeyboardSlideNavigation.OnKeyDown(e);
        }

        private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
        {
            this.IsVisible = false;
            e.Cancel = true;
        }
    }
}