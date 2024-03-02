using System;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
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