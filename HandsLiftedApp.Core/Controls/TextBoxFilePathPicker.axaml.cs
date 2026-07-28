using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Serilog;

namespace HandsLiftedApp.Core.Controls
{
    public partial class TextBoxFilePathPicker : UserControl
    {
        private readonly object _listenToTextBoxChangedEventsLock = new();
        private bool _listenToTextBoxChangedEvents = false;

        public string? FilePath
        {
            get { return _filePath; }
            set { SetAndRaise(FilePathProperty, ref _filePath, value); }
        }

        private string? _filePath;

        public static readonly DirectProperty<TextBoxFilePathPicker, string?> FilePathProperty =
            AvaloniaProperty.RegisterDirect<TextBoxFilePathPicker, string?>(
                nameof(FilePath), o => o.FilePath, (o, v) => o.FilePath = v,
                null,
                BindingMode.TwoWay
            );

        public TextBoxFilePathPicker()
        {
            InitializeComponent();

            FilePathProperty.Changed.Subscribe(x =>
            {
                lock (_listenToTextBoxChangedEventsLock)
                {
                    _listenToTextBoxChangedEvents = false;
                    FilePathTextBox.Text = FilePath;
                    _listenToTextBoxChangedEvents = true;
                }
            });

            FilePathTextBox.TextChanged += (s, e) =>
            {
                lock (_listenToTextBoxChangedEventsLock)
                {
                    if (_listenToTextBoxChangedEvents)
                    {
                        var pathOrUri = FilePathTextBox.Text;
                        if (pathOrUri == null || pathOrUri.Length == 0)
                        {
                            FilePath = null;
                        }
                        else if (File.Exists(pathOrUri) || AssetLoader.Exists(new Uri(pathOrUri)))
                        {
                            FilePath = pathOrUri;
                        }
                    }
                }
            };
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            OpenFilePicker();
        }

        private async void OpenFilePicker()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    AllowMultiple = false
                });

                var filePaths = files.Select(f => f.TryGetLocalPath())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();

                if (filePaths.Length == 0) return;
                FilePath = filePaths[0];
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error picking file");
            }
        }
    }
}