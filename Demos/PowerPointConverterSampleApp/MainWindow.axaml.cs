using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Importer.PowerPointInteropData;
using HandsLiftedApp.Importer.PowerPointLib;
using ProtoBuf;

namespace PowerPointConverterSampleApp;

public partial class MainWindow : Window
{
    ViewModel dataContext = new();
    
    public MainWindow()
    {
        InitializeComponent();

        DataContext = dataContext;
        
        _dropState = this.Get<TextBlock>("DropState");
        // new Thread(() =>
        // {
        //     while (true)
        //     {
        //         using (var server = new NamedPipeServerStream("MyPipeResult"))
        //         {
        //             Console.WriteLine("Waiting for connection...");
        //             server.WaitForConnection();
        //
        //             var x = Serializer.Deserialize<ImportResult>(server);
        //             System.Diagnostics.Debug.Print(x.ToString());
        //         }
        //     }
        // }).Start();
        
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

    private void RunConvertButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var inputFilePath = dataContext.FilePath;
        
        if (inputFilePath == null) return;
        
        if (UseNativeImport.IsChecked == true)
        {
            // TODO ensure Host is started
            using (var client = new NamedPipeClientStream(".", "MyPipe", PipeDirection.Out))
            {
                client.Connect();

                ImportTask importTask = new ImportTask() { PPTXFilePath = inputFilePath };
                Serializer.Serialize(client, importTask);
            }
            // TODO shutdown Host
        }
        else
        {
            PresentationImporter.Run(inputFilePath);
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

            // dragMe.PointerPressed += DoDrag;

            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
            AddHandler(DragDrop.DragLeaveEvent, DragLeave);
        }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog() { AllowMultiple = false };
        var window = TopLevel.GetTopLevel(this) as Window;
        var filePaths = await dialog.ShowAsync(window);

        if (filePaths == null || filePaths.Length == 0) return;
        SelectFile(filePaths[0]);
    }

    private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
    {
        dataContext.FilePath = null;
        dataContext.Status = DocumentStatus.Inactive;
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
        while (len >= 1024 && order < sizes.Length - 1) {
            order++;
            len = len/1024;
        }

// Adjust the format string to your preferences. For example "{0:0.#}{1}" would
// show a single decimal place, and no space.
        string result = String.Format("{0:0.##} {1}", len, sizes[order]);

        dataContext.Status = DocumentStatus.Active;
        dataContext.FilePath = localPath;
        
        _dropState.Text = $"Selected file:\n{file.Name}\n({result})";
    }
}