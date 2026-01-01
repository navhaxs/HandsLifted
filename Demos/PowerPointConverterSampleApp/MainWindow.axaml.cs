using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Importer.PDF;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;
using HandsLiftedApp.Importer.PowerPointLib;

namespace PowerPointConverterSampleApp;

public partial class MainWindow : Window
{
    static ViewModel viewModel = new();

    public MainWindow()
    {
        InitializeComponent();

        DataContext = viewModel;
        
        NativePowerPointInteropService.OnCompletion += (_, _) => viewModel.Status = ApplicationState.Completed;

        _dropState = this.Get<TextBlock>("DropState");

        SetupDnd(
            "Files",
            async d =>
            {
                if (Assembly.GetEntryAssembly()?.GetModules().FirstOrDefault()?.FullyQualifiedName is { } name &&
                    TopLevel.GetTopLevel(this) is { } topLevel &&
                    await topLevel.StorageProvider.TryGetFileFromPathAsync(name) is { } storageFile)
                {
                    d.Set(DataFormats.Files, new[] { storageFile });
                }
            },
            DragDropEffects.Copy);
    }

    public class ProgressReporter : IProgress<ImportStats>
    {
        public void Report(ImportStats value)
        {
            Debug.Print(value.JobPercentage.ToString());
            viewModel.ProgressValue = value.JobPercentage;

            if (value.JobStatus == ImportStats.JobStatusEnum.CompletionSuccess)
            {
                viewModel.Status = ApplicationState.Completed;
            }
        }
    }

    private void RunConvertButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var inputFilePath = viewModel.FilePath;
        if (inputFilePath == null || viewModel.IsBusy) return;

        viewModel.Status = ApplicationState.Busy;

        if (inputFilePath.ToLower().EndsWith(".pdf"))
        {
            Task.Run(() =>
            {
                ConvertPDF.Convert(new ImportTask
                {
                    InputFile = inputFilePath,
                    OutputDirectory = Path.GetDirectoryName(inputFilePath)!,
                    ExportFileFormat = ImportTask.ExportFileFormatType.PNG
                }, new ProgressReporter());
            });
            return;
        }
        
        var importTask = new ImportTask
        {
            InputFile = inputFilePath,
            OutputDirectory = Path.GetDirectoryName(inputFilePath)!,
            ExportFileFormat = PDF_RadioButton.IsChecked == true
                ? ImportTask.ExportFileFormatType.PDF
                : ImportTask.ExportFileFormatType.PNG
        };

        if (UseNativeImport.IsChecked == true)
        {
            NativePowerPointInteropService.StartHelper();
            NativePowerPointInteropService.StartPipeServerAsync(viewModel)
                .ContinueWith(_ => { NativePowerPointInteropService.SendToHelper(importTask); });
        }
        else
        {
            Task.Run(() =>
            {
                PresentationFileFormatConverter.Run(importTask, new ProgressReporter());
                viewModel.Status = ApplicationState.Completed;
                return Task.CompletedTask;
            });
        }
    }

    private readonly TextBlock _dropState;

    private void SetupDnd(string suffix, Func<DataObject, Task> factory, DragDropEffects effects)
    {
        var dragMe = this.Get<Border>("DragMe" + suffix);

        void DragLeave(object? sender, DragEventArgs e)
        {
            dragMe[!BorderBrushProperty] = new DynamicResourceExtension("DarkGrayColor");
        }

        void DragOver(object? sender, DragEventArgs e)
        {
            dragMe[!BorderBrushProperty] = new DynamicResourceExtension("SystemAccentColor");

            if (e.Source is Control c && c.Name == "MoveTarget")
            {
                e.DragEffects = e.DragEffects & (DragDropEffects.Move);
            }
            else
            {
                e.DragEffects = e.DragEffects & (DragDropEffects.Copy);
            }

            // Only allow if the dragged data contains text or filenames.
            if (!e.Data.Contains(DataFormats.Text)
                && !e.Data.Contains(DataFormats.Files))
                e.DragEffects = DragDropEffects.None;
        }

        async void Drop(object? sender, DragEventArgs e)
        {
            dragMe[!BorderBrushProperty] = new DynamicResourceExtension("DarkGrayColor");

            if (e.Source is Control c && c.Name == "MoveTarget")
            {
                e.DragEffects = e.DragEffects & (DragDropEffects.Move);
            }
            else
            {
                e.DragEffects = e.DragEffects & (DragDropEffects.Copy);
            }

            if (e.Data.Contains(DataFormats.Text))
            {
                _dropState.Text = e.Data.GetText();
            }
            else if (e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles() ?? Array.Empty<IStorageItem>();
                foreach (var item in files)
                {
                    if (item is IStorageFile file)
                    {
                        SelectFile(file.Path.LocalPath);
                        return;
                    }
                }
            }
#pragma warning disable CS0618 // Type or member is obsolete
            else if (e.Data.Contains(DataFormats.FileNames))
            {
                var files = e.Data.GetFileNames();
                _dropState.Text = string.Join(Environment.NewLine, files ?? Array.Empty<string>());
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
        AddHandler(DragDrop.DragLeaveEvent, DragLeave);
    }

    private async void BrowseOpenFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog() { AllowMultiple = false };
        var window = TopLevel.GetTopLevel(this) as Window;
        var filePaths = await dialog.ShowAsync(window);

        if (filePaths == null || filePaths.Length == 0) return;
        SelectFile(filePaths[0]);
    }

    private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
    {
        viewModel.FilePath = null;
        viewModel.Status = ApplicationState.Init;
    }

    private void SelectFile(string localPath)
    {
        // DragTargetBorder[!BackgroundProperty] = new DynamicResourceExtension("AccentColor");

        // Source - https://stackoverflow.com/a
        // Posted by David Thibault, modified by community. See post 'Timeline' for change history
        // Retrieved 2025-12-30, License - CC BY-SA 4.0

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        var file = new FileInfo(localPath);
        double len = file.Length;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
        // show a single decimal place, and no space.
        string result = String.Format("{0:0.##} {1}", len, sizes[order]);
        
        viewModel.Status = ApplicationState.Ready;
        viewModel.FilePath = localPath;

        _dropState.Text = $"Selected file:\n{file.Name}\n({result})";
    }

    private void ShowOutputButton_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenInExplorer(viewModel.OutputFilePath);
    }

    private void OpenInExplorer(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (Directory.Exists(path))
        {
            Process.Start("explorer.exe", path);
        }
        else if (File.Exists(path))
        {
            Process.Start("explorer.exe", $"/select, \"{path}\"");
        }
    }
}