using System.Threading.Tasks;
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

    public override async void OnFrameworkInitializationCompleted()
    {
        // must create renderer BEFORE slides start loading during Globals.OnStartup (auto-loading previous playlist)
        SlideRendererWorkerWindow slideRendererWorkerWindow = new SlideRendererWorkerWindow();
        slideRendererWorkerWindow.Show();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SplashWindow splashScreen = new();
            desktop.MainWindow = splashScreen;
            try {
                await Task.Delay(2000);
                Globals.OnStartup(ApplicationLifetime);

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
                desktop.MainWindow.Show();

            }
            catch (TaskCanceledException) {
            }
            splashScreen.Close();
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