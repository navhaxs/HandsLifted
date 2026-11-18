using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HandsLiftedApp.Core.Services;
using Serilog;

namespace HandsLiftedApp.Core.Views
{
    /// <summary>Runs one Toolbox conversion and shows its progress and outcome.</summary>
    public partial class ConversionProgressWindow : Window
    {
        private readonly ToolboxConversion _kind;
        private readonly string _inputFile;
        private readonly string _outputDirectory;
        private string? _resultPath;
        private bool _running = true;

        // Designer / XAML loader only.
        public ConversionProgressWindow() : this(ToolboxConversion.PptxToPdf, "", "")
        {
        }

        public ConversionProgressWindow(ToolboxConversion kind, string inputFile, string outputDirectory)
        {
            InitializeComponent();
            _kind = kind;
            _inputFile = inputFile;
            _outputDirectory = outputDirectory;

            HeadingText.Text = $"Converting {Path.GetFileName(inputFile)}";
            StatusText.Text = "Starting...";

            Opened += async (_, _) => await RunAsync();
            // The conversion can't be cancelled, so keep the window until it finishes.
            Closing += (_, e) => e.Cancel = _running;
        }

        private async Task RunAsync()
        {
            try
            {
                _resultPath = await Task.Run(() => ConversionToolbox.Run(_kind, _inputFile, _outputDirectory,
                    stats => Dispatcher.UIThread.Post(() =>
                    {
                        if (!string.IsNullOrEmpty(stats.StatusMessage)) StatusText.Text = stats.StatusMessage;
                        if (stats.JobStatus == Importer.FileFormatConvertTaskData.ImportStats.JobStatusEnum.Running
                            && stats.JobPercentage > 0)
                        {
                            Progress.IsIndeterminate = false;
                            Progress.Value = stats.JobPercentage;
                        }
                    })));

                Progress.IsIndeterminate = false;
                Progress.Value = 100;
                Title = "Conversion complete";
                StatusText.Text = $"Saved to {_resultPath}";
                OpenFolderButton.IsVisible = true;
            }
            catch (Exception e)
            {
                Log.Error(e, "Toolbox conversion failed: {Kind} {File}", _kind, _inputFile);
                Progress.IsIndeterminate = false;
                Progress.Value = 0;
                Title = "Conversion failed";
                StatusText.Text = $"Conversion failed: {e.Message}";
            }
            finally
            {
                _running = false;
                CloseButton.IsEnabled = true;
            }
        }

        private async void OpenFolderClicked(object? sender, RoutedEventArgs e)
        {
            if (_resultPath is null) return;
            var folder = Directory.Exists(_resultPath) ? _resultPath : Path.GetDirectoryName(_resultPath);
            if (folder is null) return;
            try
            {
                await GetTopLevel(this)!.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not open folder {Folder}", folder);
            }
        }

        private void CloseClicked(object? sender, RoutedEventArgs e) => Close();
    }
}
