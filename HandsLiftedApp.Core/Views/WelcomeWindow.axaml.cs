using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.Views.Setup;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
            
            DateTime date = DateTime.Now;
            DayOfWeekString.Text = date.ToString("dddd");
            DateString.Text = date.ToString("d MMMM yyyy");
        }

        private void LoadRecentPlaylistEntryCommand(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: string filePath })
            {
                MessageBus.Current.SendMessage(new LoadPlaylistAction() {FilePath = filePath});
                Close();
            }
        }

        private void NewClicked(object? sender, RoutedEventArgs e)
        {
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
            var topLevel = TopLevel.GetTopLevel(this);

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
                MessageBus.Current.SendMessage(new LoadPlaylistAction() {FilePath = filePath});
                Close();
            }
        }
    }
}