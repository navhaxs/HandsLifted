using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Utils;
using HandsLiftedApp.ViewModels;
using HandsLiftedApp.Views;
using ReactiveUI;

namespace HandsLiftedApp
{
    public class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        MainWindowViewModel mainWindowViewModel;
        public override void OnFrameworkInitializationCompleted()
        {
            // Create the AutoSuspendHelper.
            var suspension = new AutoSuspendHelper(ApplicationLifetime);
            RxApp.SuspensionHost.CreateNewAppState = () => new PreferencesViewModel();
            RxApp.SuspensionHost.SetupDefaultSuspendResume(new NewtonsoftJsonSuspensionDriver("appstate.json"));
            suspension.OnFrameworkInitializationCompleted();

            // Load the saved view model state.
            Globals.preferencesViewModel = RxApp.SuspensionHost.GetAppState<PreferencesViewModel>();

            // Normal Avalonia init
            mainWindowViewModel = new MainWindowViewModel();
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
