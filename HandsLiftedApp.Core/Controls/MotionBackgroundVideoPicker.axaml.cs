// HandsLiftedApp.Core/Controls/MotionBackgroundVideoPicker.axaml.cs
using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

        private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
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
                if (path != null)
                {
                    VideoPath = path;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error picking motion background video");
            }
        }

        private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
        {
            VideoPath = null;
        }
    }
}
