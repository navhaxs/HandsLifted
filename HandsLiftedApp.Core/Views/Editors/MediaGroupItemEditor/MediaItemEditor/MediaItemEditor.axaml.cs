using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Utils;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Editors.FreeText
{
    public partial class MediaItemEditor : UserControl
    {
        private IDisposable? _pathSubscription;

        public MediaItemEditor()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
            {
                this.DataContext = new MediaGroupItem.MediaItem() { SourceMediaFilePath = "avares://HandsLiftedApp.Core/Assets/DefaultTheme/VisionScreens_1440_placeholder.png" };
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            _pathSubscription?.Dispose();
            _pathSubscription = null;

            if (DataContext is MediaGroupItem.MediaItem mediaItem)
            {
                _pathSubscription = mediaItem.WhenAnyValue(x => x.SourceMediaFilePath)
                    .Subscribe(path => LoadPreview(mediaItem, path));
            }
        }

        private void LoadPreview(MediaGroupItem.MediaItem mediaItem, string? path)
        {
            // avares:// and cache hits are already handled synchronously by the XAML binding.
            if (string.IsNullOrEmpty(path) || path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                return;

            if (BitmapLoader.Cache.GetBitmap(path) != null)
                return;

            _ = LoadPreviewAsync(mediaItem, path);
        }

        private async Task LoadPreviewAsync(MediaGroupItem.MediaItem mediaItem, string path)
        {
            var bitmap = await BitmapLoader.LoadBitmapAsync(path);

            // Guard against a stale load finishing after the user picked a different file/item.
            if (DataContext != mediaItem || mediaItem.SourceMediaFilePath != path) return;

            PreviewImage.Source = bitmap;
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