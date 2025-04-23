using System;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HandsLiftedApp.Core.Controller;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Extensions;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views
{
    public partial class StageDisplayWindow : Window
    {
        public StageDisplayWindow()
        {
            InitializeComponent();

            WindowUtils.RegisterWindowWatcher(this);

            this.DataContextChanged += (sender, args) =>
            {
                if (DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel
                        .WhenAnyValue(x => x.Playlist.ActiveSlide)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(x =>
                        {
                            TabControl.SelectedIndex = (x is SongTitleSlideInstance ||
                                                        x is SongSlideInstance)
                                ? 1
                                : 0;
                        });
                }
            };
            
            MessageBus.Current.Listen<OutputDisplayConfigurationChangeMessage>()
                .Subscribe(async msg =>
                {
                    if (IsVisible && msg.ChangedDisplay == OutputDisplayConfigurationChangeMessage.Display.StageDisplay)
                    {
                        Dispatcher.UIThread.InvokeAsync(() => WindowUtils.ShowAndRestoreWindowBounds(this, Globals.Instance.AppPreferences.StageDisplayBounds));
                    }
                });
        }
        
        private void StageDisplayWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            KeyboardSlideNavigation.OnKeyDown(e);
        }
        
        private void ProjectorWindow_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (ControlExtension.FindAncestor<Button>((Control)e.Source) != null)
                return;

            onToggleFullscreen();
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
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.WindowState = isFullScreenNext ? WindowState.FullScreen : WindowState.Normal;
            });
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
    }
}