using System;
using Avalonia.Controls;
using Avalonia.Input;
using HandsLiftedApp.Common;
using HandsLiftedApp.Core.Controller;
using HandsLiftedApp.Extensions;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views
{
    public partial class ProjectorWindow : Window
    {
        public ProjectorWindow()
        {
            InitializeComponent();
            
            WindowUtils.RegisterWindowWatcher(this);

            this.WhenAnyValue(value => value.IsVisible).Subscribe(isVisible =>
            {
                // TODO macOS: keep awake
                if (OperatingSystem.IsWindows())
                {
                    Caffeine.KeepAwake(isVisible);
                }
            });
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

        private void ProjectorWindow_KeyDown(object? sender, KeyEventArgs e)
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