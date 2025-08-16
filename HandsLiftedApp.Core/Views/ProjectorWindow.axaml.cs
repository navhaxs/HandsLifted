using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HandsLiftedApp.Common;
using HandsLiftedApp.Core.Controller;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.Utils.MacOS;
using HandsLiftedApp.Extensions;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Views
{
    public partial class ProjectorWindow : Window, INotifyPropertyChanged
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
           
            this.PositionChanged += (s, e) =>
            {
                BoundPosition = e.Point;
                if (aTimer != null && ControlsOverlay.IsVisible)
                {
                    aTimer.Stop();
                    aTimer.Start();
                }
            };
        }

        private bool _isFullScreen = false;
        public bool IsFullScreen
        {
            get => _isFullScreen;
            set => SetProperty(ref _isFullScreen, value);
        }
        
        private PixelPoint _boundPosition;

        public PixelPoint BoundPosition
        {
            get => _boundPosition;
            set => SetProperty(ref _boundPosition, value);
        }

        private System.Timers.Timer? aTimer;
        public void RegisterControlsOverlayTimer()
        {
            ControlsOverlay.IsVisible = false;

            aTimer = new System.Timers.Timer();
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
        
        Rect? previousBounds = null;
        PixelPoint? previousPosition = null;

        public void onToggleFullscreen()
        {
            bool isRequestingFullscreen = !IsFullScreen;
            
            SetTopmost(isRequestingFullscreen);

            if (OperatingSystem.IsMacOS())
            {
                this.SystemDecorations = isRequestingFullscreen ? SystemDecorations.None : SystemDecorations.Full;
            }
            
            if (isRequestingFullscreen)
            {
                var screen = Screens.ScreenFromPoint(Position) ?? Screens.ScreenFromWindow(this);

                if (screen == null)
                {
                    Log.Error("Unable to find screen for projector window");
                    return;
                }

                this.previousBounds = this.Bounds;
                this.previousPosition = this.Position;
                
                this.Height = screen.Bounds.Height;
                this.Width = screen.Bounds.Width;
                
                this.Position = screen.Bounds.Position;
            }
            
            this.ShowInTaskbar = !isRequestingFullscreen; // make this user option
            
            if (OperatingSystem.IsWindows())
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.WindowState = isRequestingFullscreen ? WindowState.FullScreen : WindowState.Normal;
                    

                    if (!isRequestingFullscreen && previousBounds != null)
                    {
                        // Dispatcher.UIThread.InvokeAsync(() =>
                        // {
                        //     Thread.Sleep(1_000);

                            this.Height = previousBounds.Value.Height;
                            this.Width = previousBounds.Value.Width;
                        // });
                    }
                });
            }
            
            if (OperatingSystem.IsMacOS() && !isRequestingFullscreen)
            {
                this.Position = (PixelPoint)previousPosition;

                this.Height = previousBounds.Value.Height;
                this.Width = previousBounds.Value.Width;
            }
            
            IsFullScreen = isRequestingFullscreen;
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
            SetTopmost(!this.Topmost);
        }

        private void SetTopmost(bool requestTopmost)
        {
            // do not set Topmost flag on macOS - check if this breaks on Windows OS
            // Topmost = requestTopmost;
            
            if (OperatingSystem.IsMacOS())
            {
                if (requestTopmost)
                {
                    MacWindowLevel.RaiseAboveDock(this);
                }
                else
                {
                    MacWindowLevel.RestoreToNormal(this);
                }
            }
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

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region "disable topmost when contextmenu is open, because macOS fullscreen hack will otherwise hide it"
        private void ContextMenu_OnOpening(object? sender, CancelEventArgs e)
        {
            SetTopmost(false);
        }

        private void ContextMenu_OnClosing(object? sender, CancelEventArgs e)
        {
            SetTopmost(IsFullScreen);
        }
        #endregion
    }
}