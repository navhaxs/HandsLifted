using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Views.Editors.FreeText
{
    public partial class MediaItemEditor : UserControl
    {
        public MediaItemEditor()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
            {
                this.DataContext = new MediaGroupItem.MediaItem() { SourceMediaFilePath = "avares://HandsLiftedApp.Core/Assets/DefaultTheme/VisionScreens_1440_placeholder.png" };
            }
        }

        private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Media File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp" } },
                    new FilePickerFileType("Video files") { Patterns = new[] { "*.mp4", "*.mov", "*.avi", "*.mkv", "*.webm" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*.*" } },
                }
            });

            var filePath = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (filePath == null) return;

            if (DataContext is MediaGroupItem.MediaItem mediaItem)
            {
                mediaItem.SourceMediaFilePath = filePath;
            }
        }
    }
}