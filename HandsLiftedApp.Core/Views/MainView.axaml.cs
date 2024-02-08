using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Utils;
using Serilog;
using System;
using Avalonia.Controls.ApplicationLifetimes;

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
            // vm.CurrentPlaylist.Playlist.Title = "Hello";
        }
    }

    private async void NewFileButton_Clicked(object sender, RoutedEventArgs args)
    {
        
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
                try
                {
                    vm.CurrentPlaylist.Playlist = XmlSerialization.ReadFromXmlFile<Playlist>(stream);
                }
                catch (Exception e)
                {
                    Log.Error("[DOC] Failed to parse playlist XML");
                    Console.WriteLine(e);
                }
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
                    vm.settings.LastOpenedPlaylistFullPath = file.Path.AbsolutePath;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to write XML");
                    //MessageBus.Current.SendMessage(new MainWindowModalMessage(new MessageWindow() { Title = "Playlist Saved" }, true, new MessageWindowAction() { Title = "Playlist Failed to Save :(", Content = $"Target was {TEST_SERVICE_FILE_PATH}\n${e.Message}" }));
                }
            }
        }
    }
    
    private void CloseWindow(object sender, RoutedEventArgs e)
    {
        // MessageBus.Current.SendMessage(new MainWindowMessage(ActionType.CloseWindow));
        ((ClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current.ApplicationLifetime).Shutdown();
    }


}