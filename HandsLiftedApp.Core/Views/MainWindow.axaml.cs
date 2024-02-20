using Avalonia.Controls;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Core.ViewModels;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views.App;

namespace HandsLiftedApp.Core.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

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
            Shade.IsVisible = true;
            AboutWindow mw = new AboutWindow() { DataContext = mwvm };
            mw.Closed += (object? sender, EventArgs e) =>
            {
                Shade.IsVisible = false;
                IsEnabled = true;
            };
            IsEnabled = false;
            mw.ShowDialog(this);
        });

        this.Loaded += (e, s) =>
        {
            if (DataContext is MainViewModel vm)
            {
                if (vm.settings.LastWindowState != null)
                {
                    WindowState = (WindowState)vm.settings.LastWindowState;
                }
            }
        };

        this.GetObservable(Window.WindowStateProperty)
            .Subscribe(v =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.settings.LastWindowState = v;
                }
            });
        //
        SubscribeToWindowState();
    }

    private async void SubscribeToWindowState()
    {
        Window hostWindow = (Window)this.VisualRoot;

        while (hostWindow == null)
        {
            hostWindow = (Window)this.VisualRoot;
            await Task.Delay(50);
        }

        hostWindow.GetObservable(Window.WindowStateProperty).Subscribe(s =>
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

    void UpdateWin32Border(WindowState v)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // set border thickness to 0 when maximised
        RootBorder.BorderThickness = new Thickness((v == WindowState.Maximized) ? 0 : 1);

        // apply workaround for avalonia bug:
        if (v != WindowState.Maximized)
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

    private async Task ShowOpenFileDialog(InteractionContext<Unit, string[]?> interaction)
    {
        try
        {
            var dialog = new OpenFileDialog() { AllowMultiple = true };
            var fileNames = await dialog.ShowAsync(this);
            interaction.SetOutput(fileNames);
        }
        catch (Exception e)
        {
            Debug.Print(e.Message);
            interaction.SetOutput(null);
        }
    }
}