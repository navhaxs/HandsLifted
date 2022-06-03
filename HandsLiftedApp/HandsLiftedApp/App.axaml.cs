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

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var x = new MainWindowViewModel();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = x,
                };

                //ProjectorWindow p = new ProjectorWindow();
                //p.DataContext = x;
                //p.Show();

            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
