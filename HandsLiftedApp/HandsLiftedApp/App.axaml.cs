using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.ViewModels;
using HandsLiftedApp.Views;

namespace HandsLiftedApp
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        MainWindowViewModel mainWindowViewModel = new MainWindowViewModel();

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainWindowViewModel,
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
