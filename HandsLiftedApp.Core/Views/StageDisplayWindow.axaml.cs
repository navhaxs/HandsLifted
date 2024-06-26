﻿using System;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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