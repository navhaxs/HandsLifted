using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HandsLiftedApp.Common;
using HandsLiftedApp.Core.Controller;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Extensions;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Views
{
    public partial class ProjectorWindow : Window
    {
        public ProjectorWindow()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
                return;

            Log.Information("Created ProjectorWindow");

            if (OperatingSystem.IsWindows())
            {
                WindowUtils.RegisterWindowWatcher(this);
            }

            this.WhenAnyValue(value => value.IsVisible).Subscribe(isVisible =>
            {
                Log.Information("ProjectorWindow IsVisible {State}", isVisible);

                // TODO macOS: keep awake
                if (OperatingSystem.IsWindows() && !Debugger.IsAttached)
                {
                    Caffeine.KeepAwake(isVisible);
                }
            });

            MessageBus.Current.Listen<OutputDisplayConfigurationChangeMessage>()
                .Subscribe(async msg =>
                {
                    if (IsVisible && msg.ChangedDisplay == OutputDisplayConfigurationChangeMessage.Display.Projector)
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                            WindowUtils.ShowAndRestoreWindowBounds(this,
                                Globals.Instance.AppPreferences.OutputDisplayBounds));
                    }
                });

            RegisterControlsOverlayTimer();
        }

        public void RegisterControlsOverlayTimer()
        {
            ControlsOverlay.IsVisible = false;

            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += ((sender, args) =>
            {
                aTimer.Stop();
                Dispatcher.UIThread.InvokeAsync(() => ControlsOverlay.IsVisible = false);
            });
            aTimer.Interval = 5000; // ~ 5 seconds
            aTimer.Enabled = true;

            this.PointerMoved += (sender, args) =>
            {
                ControlsOverlay.IsVisible = true;
                aTimer.Stop();
                aTimer.Start();
            };
        }

        public void onToggleFullscreen(bool? fullscreen = null)
        {
            bool isFullScreenNext =
                (fullscreen != null) ? (bool)fullscreen : (this.WindowState != WindowState.FullScreen);

            if (isFullScreenNext)
            {
                var bounds = Screens.ScreenFromWindow(this).Bounds;
                this.Height = bounds.Height;
                this.Width = bounds.Width;
            }

            this.ShowInTaskbar = !isFullScreenNext; // make this user option
            if (!OperatingSystem.IsMacOS())
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.WindowState = isFullScreenNext ? WindowState.FullScreen : WindowState.Normal;
                });
            }
        }

        private void ProjectorWindow_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (ControlExtension.FindAncestor<Button>((Control)e.Source) != null)
                return;

            onToggleFullscreen();
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

        private void ToggleFullscreen_OnClick(object? sender, RoutedEventArgs e)
        {
            onToggleFullscreen();
        }

        private void ToggleTopmost_OnClick(object? sender, RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;
        }

        private void Close_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PreviousButton_OnClick(object? sender, RoutedEventArgs e)
        {
            MessageBus.Current.SendMessage(new ActionMessage()
                { Action = ActionMessage.NavigateSlideAction.PreviousSlide });
        }

        private void NextButton_OnClick(object? sender, RoutedEventArgs e)
        {
            MessageBus.Current.SendMessage(new ActionMessage()
                { Action = ActionMessage.NavigateSlideAction.NextSlide });
        }

        private void FullscreenToggleButton_OnClick(object? sender, RoutedEventArgs e)
        {
            onToggleFullscreen();
        }
    }
}