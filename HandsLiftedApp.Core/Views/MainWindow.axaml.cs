using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Controls;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views.App;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
        
        Win10DropshadowWorkaround.Register(this);

        if (OperatingSystem.IsMacOS())
        {
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.Default;
            ExtendClientAreaTitleBarHeightHint = 0;
            ExtendClientAreaToDecorationsHint = false;
        }

        // When the window is activated, registers a handler for the ShowOpenFileDialog interaction.
        this.WhenActivated(d => d(ViewModel.ShowOpenFileDialog.RegisterHandler(ShowOpenFileDialog)));

        MessageBus.Current.Listen<MessageWindowViewModel>().Subscribe(mwvm =>
        {
            Shade.IsVisible = true;
            MessageWindow mw = new MessageWindow() { DataContext = mwvm };
            mw.Closed += (object? sender, EventArgs e) =>
            {
                Shade.IsVisible = false;
                IsEnabled = true;
            };
            IsEnabled = false;
            mw.ShowDialog(this);
        });

        MessageBus.Current.Listen<MainWindowMessage>().Subscribe(mwvm =>
        {
            Window wnd;
            switch (mwvm.Action)
            {
                case ActionType.AboutWindow:
                    wnd = new AboutWindow();
                    Shade.IsVisible = true;
                    wnd.Closed += (object? sender, EventArgs e) =>
                    {
                        Shade.IsVisible = false;
                        IsEnabled = true;
                    };
                    IsEnabled = false;
                    wnd.ShowDialog(this);
                    break;
                case ActionType.WelcomeWindow:
                    WelcomeWindow welcomeWindow = new() { DataContext = new WelcomeWindowViewModel(this.DataContext as MainViewModel) };
                    welcomeWindow.ShowDialog(this);
                    break;
                case ActionType.CloseWindow:
                    Close();
                    break;
            }
        });

        MessageBus.Current.Listen<MainWindowModalMessage>()
            .Subscribe(x =>
            {
                if (x.Window.IsVisible)
                    return;
                
                if (x.ShowAsDialog)
                    Shade.IsVisible = true;

                //TODO do not always want to set DataContext if object has set it itself
                if (x.Window.DataContext == null)
                    x.Window.DataContext = x.DataContext ?? this.DataContext;

                x.Window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                x.Window.Closed += (object? sender, EventArgs e) => { Shade.IsVisible = false; };

                if (x.ShowAsDialog)
                {
                    this.IsEnabled = false;
                    x.Window.ShowDialog(this);
                    this.IsEnabled = true;
                }
                else
                    x.Window.Show(this);
            });

        this.Loaded += (e, s) =>
        {
            if (DataContext is MainViewModel vm)
            {
                if (vm.settings.LastWindowState != null)
                {
                    WindowState = (WindowState)vm.settings.LastWindowState;
                }

                if (!Debugger.IsAttached && !Environment.MachineName.Contains("JEREMY"))
                {
                    ThisIsATestBuildWarningWindow warningWindow = new();
                    warningWindow.ShowDialog(this);
                }

            }
        };

        this.Closing += MainWindow_Closing;

        this.GetObservable(WindowStateProperty)
            .Subscribe(v =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.settings.LastWindowState = v;
                }
            });
        //
        SubscribeToWindowState();

        // HACK for Windows 10 drop shadows 
        this.Loaded += (e, s) => { updateWin32Border(this.WindowState); };

        this.GetObservable(WindowStateProperty)
            .Subscribe(v => { updateWin32Border(v); });
    }

    private void updateWin32Border(WindowState v)
    {
        if (v != WindowState.Maximized && OperatingSystem.IsWindows())
        {
            var margins = new Win32.MARGINS
            {
                cyBottomHeight = 1,
                cxRightWidth = 1,
                cxLeftWidth = 1,
                cyTopHeight = 1
            };

            Win32.DwmExtendFrameIntoClientArea(this.TryGetPlatformHandle().Handle, ref margins);
        }
    }

    private async void SubscribeToWindowState()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Window hostWindow = (Window)this.VisualRoot;

        while (hostWindow == null)
        {
            // TODO stupid hack
            hostWindow = (Window)this.VisualRoot;
            await Task.Delay(50);
        }

        hostWindow.GetObservable(WindowStateProperty).Subscribe(s =>
        {
            if (s != WindowState.Maximized)
            {
                hostWindow.Padding = new Thickness(0, 0, 0, 0);
            }

            if (s == WindowState.Maximized)
            {
                hostWindow.Padding = new Thickness(7, 7, 7, 7);

                // This should be a more universal approach in both cases, but I found it to be less reliable, when for example double-clicking the title bar.
                /*hostWindow.Padding = new Thickness(
                        hostWindow.OffScreenMargin.Left,
                        hostWindow.OffScreenMargin.Top,
                        hostWindow.OffScreenMargin.Right,
                        hostWindow.OffScreenMargin.Bottom);*/
            }
        });
    }

    bool _isConfirmedExiting = false;

    public async void ExitApp()
    {
        if (this.DataContext is MainViewModel vm)
        {
            // feature: unsaved changes dirty bit
            var isPlaylistEmpty = (vm.Playlist.Title.Length == 0 && vm.Playlist.Items.Count == 0);
            if (vm.Playlist.IsDirty && !isPlaylistEmpty)
            {
                Shade.IsVisible = true;
                UnsavedChangesConfirmationWindow unsavedChangesConfirmationWindow =
                    new UnsavedChangesConfirmationWindow();
                await unsavedChangesConfirmationWindow.ShowDialog(this);
                Shade.IsVisible = false;

                switch (unsavedChangesConfirmationWindow.Result)
                {
                    case UnsavedChangesConfirmationWindow.DialogResult.Save:
                        if (vm.Playlist.PlaylistFilePath == null)
                        {
                            // give user chance to pick save file path
                            var filePath = await PlaylistSaveService.ShowSaveAsDialog(this, vm.Playlist);
                            if (filePath != null)
                            {
                                // update MRU list
                                MessageBus.Current.SendMessage(new UpdateLastOpenedPlaylistAction() {FilePath = filePath});
                            }
                        }
                        // do save
                        if (vm.Playlist.PlaylistFilePath != null)
                        {
                            PlaylistDocumentService.SaveDocument(vm.Playlist);
                        }
                        break;
                    case UnsavedChangesConfirmationWindow.DialogResult.Discard:
                        break;
                    case UnsavedChangesConfirmationWindow.DialogResult.Cancel:
                        // abort application exit
                        return;
                }
            }
        }

        _isConfirmedExiting = true;

        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            Close();
            App.ExitApplication(desktopLifetime, this);
        }
        else
        {
            MainWindow hostWindow = (MainWindow)this.VisualRoot;
            hostWindow.Close();
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isConfirmedExiting)
        {
            e.Cancel = true;

            MessageBus.Current.SendMessage(new MainWindowModalMessage(new ExitConfirmationWindow()
                { parentWindow = this }));
        }
    }

    private async Task ShowOpenFileDialog(
        IInteractionContext<FilePickerOpenOptions?, IReadOnlyList<IStorageFile>?> interaction)
    {
        try
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(interaction.Input ??
                new FilePickerOpenOptions
                {
                    AllowMultiple = true
                });
            interaction.SetOutput(files);
        }
        catch (Exception e)
        {
            Debug.Print(e.Message);
            interaction.SetOutput(null);
        }
    }

    private void TopLevel_OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OnMainWindowOpened();
        }
    }
}