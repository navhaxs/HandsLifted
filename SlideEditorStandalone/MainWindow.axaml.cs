using System.IO;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;

namespace SlideEditorStandalone;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void LoadButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // XML
        if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
        {
            Open();
        }
    }

    private async void Open()
    {
        var topLevel = TopLevel.GetTopLevel(this);

        // Start async operation to open the dialog.
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = false
        });
        
        XmlSerializer serializer = new XmlSerializer(typeof(MediaGroupItem));

        using (FileStream stream = new FileStream(files[0].TryGetLocalPath(), FileMode.Open))
        {
            var x = serializer.Deserialize(stream);
            if (x != null)
            {
                
                DataContext = ItemInstanceFactory.ToItemInstance((MediaGroupItem)x, null);
            }
        }

        // if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
        // {
        //
        // }
    }

    private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Save();
    }


    private async void Save()
    {
        var topLevel = TopLevel.GetTopLevel(this);

        if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
        {
            Item convertMe = HandsLiftedDocXmlSerializer.SerializeItem(mediaGroupItemInstance, null);
            using (MemoryStream memoryStream = new MemoryStream())
            {
                XmlSerializer serializer = new XmlSerializer(typeof(MediaGroupItem));
                serializer.Serialize(memoryStream, convertMe);
                
                // Start async operation to open the dialog.
                var files = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save File",
                });

                // serialization was successful - only now do we write to disk
                await using var stream = await files.OpenWriteAsync();

                memoryStream.WriteTo(stream);
            }
        }
    }
}