﻿using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Importer.OnlineSongLyrics;

namespace HandsLiftedApp.Core.Views
{
    public partial class DebugWindow : Window
    {
        public DebugWindow()
        {
            InitializeComponent();
            
            var themeVariants = this.Get<ComboBox>("ThemeVariants");
            themeVariants.SelectedItem = Application.Current!.RequestedThemeVariant;
            themeVariants.SelectionChanged += (sender, e) =>
            {
                if (themeVariants.SelectedItem is ThemeVariant themeVariant)
                {
                    Application.Current!.RequestedThemeVariant = themeVariant;
                }
            };
            
        }

        private void DebugClick(object? sender, RoutedEventArgs e)
        {
            Debugger.Launch();
            if (Debugger.IsAttached)
            {
                var x = Globals.Instance.AppPreferences;
                Debugger.Break();
            }
        }  
        
        private void ClearActiveSlideClick(object? sender, RoutedEventArgs e)
        {
            Globals.Instance.MainViewModel.Playlist.NavigateToReference(new SlideReference() { ItemIndex = -1});
        }

        private void WebBrowserClick(object? sender, RoutedEventArgs e)
        {
            BrowserWindow window = new();
            window.Show();
        }
    }
}