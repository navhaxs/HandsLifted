using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Core.ViewModels;

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
            // this will close *all windows*. namely, SlideRendererWorkerWindow
            foreach (var window in desktopLifetime.Windows.ToList())
            {
                if (window != sender)
                    window.Close();
            }
            desktopLifetime.Shutdown();
        }
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        // must create renderer BEFORE slides start loading during Globals.OnStartup (auto-loading previous playlist)
        SlideRendererWorkerWindow slideRendererWorkerWindow = new SlideRendererWorkerWindow();
        slideRendererWorkerWindow.Show();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
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