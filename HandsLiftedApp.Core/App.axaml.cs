using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Core.ViewModels;
using Newtonsoft.Json;
using Serilog;

namespace HandsLiftedApp.Core;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public static void ExitApplication(IApplicationLifetime? applicationLifetime, Window sender)
    {
        if (applicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            // this will close *all windows*
            foreach (var window in desktopLifetime.Windows.ToList())
            {
                if (window != sender)
                {
                    try
                    {
                        window.Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error closing window {WindowType}", window.GetType().Name);
                    }
                }
            }
            Globals.Instance.OnShutdown();
            desktopLifetime.Shutdown();
        }
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (Design.IsDesignMode)
        {
            return;
        }
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            desktop.Exit += (_, __) =>
            {
                var json = JsonConvert.SerializeObject(Globals.Instance.AppPreferences);
                File.WriteAllText(Constants.APP_STATE_FILEPATH, json);
            };
            
            SplashWindow splashScreen = new();
            desktop.MainWindow = splashScreen;
            try {
                await Task.Delay(2_000);
                Globals.Instance.OnStartup(ApplicationLifetime);
                
                var welcome = new WelcomeWindow
                {
                    DataContext = new WelcomeWindowViewModel(Globals.Instance.MainViewModel)
                };
                desktop.MainWindow = welcome;
                welcome.Show();

            }
            catch (TaskCanceledException) {
            }
            splashScreen.Close();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = Globals.Instance.MainViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}