using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Importer.PowerPointInteropData;
using HandsLiftedApp.Importer.PowerPointLib;
using ProtoBuf;

namespace PowerPointConverterSampleApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (UseNativeImport.IsChecked == true)
        {
            // TODO ensure Host is started
            using (var client = new NamedPipeClientStream(".", "MyPipe", PipeDirection.Out))
            {
                client.Connect();

                ImportTask importTask = new ImportTask() { PPTXFilePath = InputPPTX.Text };
                Serializer.Serialize(client, importTask);
            }
            // TODO shutdown Host
        }
        else
        {
            PresentationImporter.Run(InputPPTX.Text);
        }
    }
    private readonly TextBlock _dropState;
     private void SetupDnd(string suffix, Func<DataObject, Task> factory, DragDropEffects effects)
        {
            var dragMe = this.Get<Border>("DragMe" + suffix);
            var dragState = this.Get<TextBlock>("DragState" + suffix);

            async void DoDrag(object? sender, PointerPressedEventArgs e)
            {
                var dragData = new DataObject();
                await factory(dragData);

                var result = await DragDrop.DoDragDrop(e, dragData, effects);
                switch (result)
                {
                    case DragDropEffects.Move:
                        dragState.Text = "Data was moved";
                        break;
                    case DragDropEffects.Copy:
                        dragState.Text = "Data was copied";
                        break;
                    case DragDropEffects.Link:
                        dragState.Text = "Data was linked";
                        break;
                    case DragDropEffects.None:
                        dragState.Text = "The drag operation was canceled";
                        break;
                    default:
                        dragState.Text = "Unknown result";
                        break;
                }
            }

            void DragOver(object? sender, DragEventArgs e)
            {
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
                    var contentStr = "";

                    foreach (var item in files)
                    {
                        if (item is IStorageFile file)
                        {
                            // var content = await DialogsPage.ReadTextFromFile(file, 500);
                            contentStr += $"File {item.Name}:{Environment.NewLine}{file.Name}{Environment.NewLine}{Environment.NewLine}";
                        }
                        else if (item is IStorageFolder folder)
                        {
                            var childrenCount = 0;
                            await foreach (var _ in folder.GetItemsAsync())
                            {
                                childrenCount++;
                            }
                            contentStr += $"Folder {item.Name}: items {childrenCount}{Environment.NewLine}{Environment.NewLine}";
                        }
                    }

                    _dropState.Text = contentStr;
                }
#pragma warning disable CS0618 // Type or member is obsolete
                else if (e.Data.Contains(DataFormats.FileNames))
                {
                    var files = e.Data.GetFileNames();
                    _dropState.Text = string.Join(Environment.NewLine, files ?? Array.Empty<string>());
                }
#pragma warning restore CS0618 // Type or member is obsolete
            }

            dragMe.PointerPressed += DoDrag;

            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
        }

}