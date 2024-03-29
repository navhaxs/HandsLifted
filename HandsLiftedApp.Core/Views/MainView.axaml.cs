using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Utils;
using Serilog;
using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using HandsLiftedApp.Core.Controller;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Views.Editor;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        this.AttachedToVisualTree += (sender, args) =>
        {
            var window = TopLevel.GetTopLevel(this);
            window.KeyDown += MainWindow_KeyDown;
        };
        
        LibraryToggleButton.Click += (object? sender, RoutedEventArgs e) =>
        {
            if (this.DataContext is MainViewModel vm)
            {
                bool forceVisible = vm.BottomLeftPanelSelectedTabIndex != 0;
                vm.BottomLeftPanelSelectedTabIndex = 0;
                ToggleBottomPanel(forceVisible);
            }
            DesignerToggleButton.IsChecked = false;
        };
        DesignerToggleButton.Click += (object? sender, RoutedEventArgs e) =>
        {
            if (this.DataContext is MainViewModel vm)
            {
                bool forceVisible = vm.BottomLeftPanelSelectedTabIndex != 1;
                vm.BottomLeftPanelSelectedTabIndex = 1;
                ToggleBottomPanel(forceVisible);
            }
            LibraryToggleButton.IsChecked = false;
        };
    }
    bool isLibraryVisible = false;
    GridLength lastLibraryContentGridLength = new GridLength(260);
    GridLength lastLibrarySplitterGridLength = new GridLength(0);

 
    private void ToggleBottomPanel(bool forceVisible = false)
    {
        if (forceVisible && isLibraryVisible)
        {
            // do nothing if already visible
            return;
        }

        if (isLibraryVisible)
        {
            lastLibraryContentGridLength = DockedLibraryWrapper.RowDefinitions[2].Height;
            lastLibrarySplitterGridLength = DockedLibraryWrapper.RowDefinitions[1].Height;
            DockedLibraryWrapper.RowDefinitions[2].Height = new GridLength(0);
            DockedLibraryWrapper.RowDefinitions[1].Height = new GridLength(0);
        }
        else
        {
            DockedLibraryWrapper.RowDefinitions[2].Height = lastLibraryContentGridLength;
            DockedLibraryWrapper.RowDefinitions[1].Height = lastLibrarySplitterGridLength;
        }
        isLibraryVisible = !isLibraryVisible;
    }

    private void MainWindow_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        var window = TopLevel.GetTopLevel(this);

        // if (!window.IsFocused)
        // {
        //     return;
        // }

        // TODO: if a textbox, datepicker etc is selected - then skip this func.
        var focusManager = TopLevel.GetTopLevel(this).FocusManager;
        var focusedElement = focusManager.GetFocusedElement();

        if (focusedElement is TextBox || focusedElement is DatePicker)
            return;

        KeyboardSlideNavigation.OnKeyDown(e);
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
        if (this.DataContext is MainViewModel vm)
        {
            vm.Playlist = new PlaylistInstance();
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
            // await using var stream = await files[0].OpenReadAsync();

            if (this.DataContext is MainViewModel vm)
            {
                try
                {
                    var x = HandsLiftedDocXmlSerializer.DeserializePlaylist(
                        files[0].Path.LocalPath);
                    vm.Playlist = x;
                    // vm.CurrentPlaylist.Playlist = XmlSerialization.ReadFromXmlFile<Playlist>(stream);
                }
                catch (Exception e)
                {
                    MessageBus.Current.SendMessage(new MessageWindowViewModel()
                        { Title = "Playlist failed to load :(", Content = $"{e.Message}" });
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
        var xmlFileType = new FilePickerFileType("XML Document")
        {
            Patterns = new[] { "*.xml" },
            MimeTypes = new[] { "text/xml" }
        };

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File",
            FileTypeChoices = new[] { xmlFileType }
        });

        if (file != null)
        {
            if (this.DataContext is MainViewModel vm)
            {
                try
                {
                    var filePath = file.Path.LocalPath;
                    HandsLiftedDocXmlSerializer.SerializePlaylist(vm.Playlist, filePath);
                    // XmlSerialization.WriteToXmlFile<Playlist>(file.Path.AbsolutePath, vm.CurrentPlaylist.Playlist);
                    //MessageBus.Current.SendMessage(new MainWindowModalMessage(new MessageWindow() { Title = "Playlist Saved" }, true, new MessageWindowAction() { Title = "Playlist Saved", Content = $"Written to {TEST_SERVICE_FILE_PATH}" }));
                    vm.settings.LastOpenedPlaylistFullPath = filePath;
                    MessageBus.Current.SendMessage(new MessageWindowViewModel()
                        { Title = "Playlist Saved" });
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to write XML");
                    MessageBus.Current.SendMessage(new MessageWindowViewModel()
                        { Title = "Playlist Failed to Save :(", Content = $"{e.Message}" });
                }
            }
        }
    }

    private void CloseWindow(object sender, RoutedEventArgs e)
    {
        MessageBus.Current.SendMessage(new MainWindowMessage(ActionType.CloseWindow));
        // ((ClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current.ApplicationLifetime).Shutdown();
    }

    private void Slide_OnClick(object? sender, RoutedEventArgs e)
    {
        SlideDesignerWindow slideDesignerWindow = new SlideDesignerWindow() { DataContext = this.DataContext };
        slideDesignerWindow.Show();
    }
    
    private void About_OnClick(object? sender, RoutedEventArgs e)
    {
        MessageBus.Current.SendMessage(new MainWindowMessage(ActionType.AboutWindow));
    }

    private void Debug_OnClick(object? sender, RoutedEventArgs e)
    {
        DebugWindow debugWindow = new DebugWindow() { DataContext = this.DataContext };
        debugWindow.Show();
    }
}