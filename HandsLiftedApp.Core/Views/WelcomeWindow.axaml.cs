using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.Views.Setup;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views
{
    public partial class WelcomeWindow : Window
    {
        private bool _openMainOnClose = false;
        
        public WelcomeWindow()
        {
            InitializeComponent();
            
            DateTime date = DateTime.Now;
            DayOfWeekString.Text = date.ToString("dddd");
            DateString.Text = date.ToString("d MMMM yyyy");
            GreetingText.Text = date.Hour < 12 ? "Good Morning" : 
                               date.Hour < 18 ? "Good Afternoon" : "Good Evening";

            this.Closed += (_, __) =>
            {
                if (_openMainOnClose)
                {
                    var main = new MainWindow
                    {
                        DataContext = Globals.Instance.MainViewModel,
                        WindowState = WindowState.Normal
                    };

                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                    {
                        desktopLifetime.MainWindow = main;
                    }
                    
                    main.Show();
                }
                else
                {
                    // no action was selected
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                    {
                        // terminate the application if the MainWindow is not already open
                        var mainIsOpen = desktopLifetime.MainWindow is MainWindow { IsVisible: true };
                        if (!mainIsOpen)
                        {
                            App.ExitApplication(desktopLifetime, this);
                        }
                    }
                }
            };
        }

        private void LoadRecentPlaylistEntryCommand(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: WelcomeWindowViewModel.RecentPlaylistEntry recentPlaylistEntry })
            {
                _openMainOnClose = true;
                MessageBus.Current.SendMessage(new LoadPlaylistAction() {FilePath = recentPlaylistEntry.FilePath});
                Close();
            }
        }

        private void NewClicked(object? sender, RoutedEventArgs e)
        {
            _openMainOnClose = true;
            Close();
        }

        private void SetUpClicked(object? sender, RoutedEventArgs e)
        {
            SetupWindow setupWindow = new SetupWindow();
            setupWindow.ShowDialog(this);
        }
        
        private async void OpenFileButton_Clicked(object sender, RoutedEventArgs args)
        {
            // Get top level from the current control. Alternatively, you can use Window reference instead.
            var topLevel = GetTopLevel(this);

            // Start async operation to open the dialog.
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Text File",
                AllowMultiple = false
            });

            if (files.Count >= 1)
            {
                // Open reading stream from the first file.
                // await using var stream = await files[0].OpenReadAsync();
                var filePath = files[0].Path.LocalPath;
                _openMainOnClose = true;
                MessageBus.Current.SendMessage(new LoadPlaylistAction() {FilePath = filePath});
                Close();
            }
        }
    }
}