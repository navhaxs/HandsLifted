// HandsLiftedApp.Core/Controls/MotionBackgroundVideoPicker.axaml.cs
using System;
using System.IO;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Core.ViewModels;
using Serilog;

namespace HandsLiftedApp.Core.Controls
{
    public partial class MotionBackgroundVideoPicker : UserControl
    {
        public string? VideoPath
        {
            get => _videoPath;
            set => SetAndRaise(VideoPathProperty, ref _videoPath, value);
        }

        private string? _videoPath;

        public event EventHandler<string>? VideoPathPicked;

        public static readonly DirectProperty<MotionBackgroundVideoPicker, string?> VideoPathProperty =
            AvaloniaProperty.RegisterDirect<MotionBackgroundVideoPicker, string?>(
                nameof(VideoPath), o => o.VideoPath, (o, v) => o.VideoPath = v,
                null,
                BindingMode.TwoWay
            );

        public MotionBackgroundVideoPicker()
        {
            InitializeComponent();

            this.GetObservable(VideoPathProperty).Subscribe(v => PathTextBox.Text = v);
        }

        private static readonly HashSet<string> VideoExtensions =
            new(Constants.SUPPORTED_VIDEO, StringComparer.OrdinalIgnoreCase);

        // (Re)built each time the picker enters the visual tree - i.e. each time its flyout opens -
        // so it reflects the current Motion Background folder and the folder contents on disk.
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            var libraryPath = Globals.Instance.AppPreferences?.MotionBackgroundLibraryPath;
            var hasLibrary = !string.IsNullOrWhiteSpace(libraryPath) && Directory.Exists(libraryPath);

            BrowserHost.IsVisible = hasLibrary;
            NoLibraryHint.IsVisible = !hasLibrary;
            BrowseFileButton.IsVisible = !hasLibrary;

            if (!hasLibrary) return;

            var library = new Library(new LibraryConfig.LibraryDefinition
            {
                Label = "Motion Backgrounds",
                Directory = libraryPath,
                Type = LibraryType.Media
            });
            Browser.DataContext = new MediaLibraryQueryViewModel(library, VideoExtensions,
                entry => SetPicked(entry.FullPath));
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            (Browser.DataContext as MediaLibraryQueryViewModel)?.CancelPreviewLoading();
            base.OnDetachedFromVisualTree(e);
        }

        // OS file dialog fallback for when no Motion Background folder is configured.
        private async void BrowseFileButton_OnClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var videoFileType = new FilePickerFileType("Video Files")
                {
                    Patterns = new[] { "*.mp4", "*.mov", "*.avi", "*.wmv", "*.mkv", "*.webm" }
                };

                // Goes through the MainWindow-handled ShowOpenFileDialog interaction rather than
                // TopLevel.GetTopLevel(this) - this control is used inside nested Flyouts
                // (ItemEditDockRoot, SlideThemeDesigner), where GetTopLevel silently resolves to
                // null (see this project's CLAUDE.md).
                var files = await Globals.Instance.MainViewModel.ShowOpenFileDialog.Handle(
                    new FilePickerOpenOptions
                    {
                        AllowMultiple = false,
                        Title = "Select Motion Background Video",
                        FileTypeFilter = new[] { videoFileType }
                    });

                if (files == null || files.Count == 0) return;

                var path = files[0].TryGetLocalPath();
                if (path != null) SetPicked(path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error picking motion background video");
            }
        }

        private void SetPicked(string path)
        {
            VideoPath = path;
            // The OS dialog steals focus, which light-dismisses a hosting Flyout and detaches this
            // control, severing the two-way VideoPath binding. Hosts that live in a Flyout subscribe
            // to this to write the value back themselves.
            VideoPathPicked?.Invoke(this, path);
        }

        private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
        {
            VideoPath = null;
        }
    }
}
