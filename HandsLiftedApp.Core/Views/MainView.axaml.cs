using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Utils;
using Serilog;
using System;

namespace HandsLiftedApp.Core.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private async void TestButton_Clicked(object sender, RoutedEventArgs args)
    {
        if (this.DataContext is MainViewModel vm)
        {
            vm.CurrentPlaylist.Playlist.Title = "Hello";
        }
    }
    private async void OpenFileButton_Clicked(object sender, RoutedEventArgs args)
    {
        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var topLevel = TopLevel.GetTopLevel(this);

        // Start async operation to open the dialog.
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Text File",
            AllowMultiple = false
        });

        if (files.Count >= 1)
        {
            // Open reading stream from the first file.
            await using var stream = await files[0].OpenReadAsync();

            if (this.DataContext is MainViewModel vm)
            {
                vm.CurrentPlaylist.Playlist = XmlSerialization.ReadFromXmlFile<Playlist>(stream);
            }
        }
    }

    private async void SaveFileButton_Clicked(object sender, RoutedEventArgs args)
    {
        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var topLevel = TopLevel.GetTopLevel(this);

        // Start async operation to open the dialog.
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File",
        });

        if (file != null)
        {
            if (this.DataContext is MainViewModel vm)
            {
                try
                {
                    XmlSerialization.WriteToXmlFile<Playlist>(file.Path.AbsolutePath, vm.CurrentPlaylist.Playlist);
                    //MessageBus.Current.SendMessage(new MainWindowModalMessage(new MessageWindow() { Title = "Playlist Saved" }, true, new MessageWindowAction() { Title = "Playlist Saved", Content = $"Written to {TEST_SERVICE_FILE_PATH}" }));
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to write XML");
                    //MessageBus.Current.SendMessage(new MainWindowModalMessage(new MessageWindow() { Title = "Playlist Saved" }, true, new MessageWindowAction() { Title = "Playlist Failed to Save :(", Content = $"Target was {TEST_SERVICE_FILE_PATH}\n${e.Message}" }));
                }
            }
        }
    }
}