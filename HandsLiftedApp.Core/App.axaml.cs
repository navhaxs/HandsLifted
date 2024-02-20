using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Config.Net;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.Views;

namespace HandsLiftedApp.Core;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Globals.OnStartup(ApplicationLifetime);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            WindowState windowState = WindowState.Normal;

            if (Globals.MainViewModel.settings.LastWindowState != null)
            {
                windowState = (WindowState)Globals.MainViewModel.settings.LastWindowState;
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = Globals.MainViewModel,
                WindowState = windowState
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = Globals.MainViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}