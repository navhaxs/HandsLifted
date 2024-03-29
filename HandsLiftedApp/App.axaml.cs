using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.ViewModels;
using HandsLiftedApp.Views;
using System.Diagnostics;

namespace HandsLiftedApp
    {
    public class App : Application
        {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
            {
            // Initialize app preferences state here

            // Create the AutoSuspendHelper.
            //var suspension = new AutoSuspendHelper(ApplicationLifetime);
            //RxApp.SuspensionHost.CreateNewAppState = () => new PreferencesViewModel();
            //RxApp.SuspensionHost.SetupDefaultSuspendResume(new NewtonsoftJsonSuspensionDriver("appstate.json"));
            //suspension.OnFrameworkInitializationCompleted();

            //// Load the saved view model state.
            //Globals.Preferences = RxApp.SuspensionHost.GetAppState<PreferencesViewModel>();

            Globals.OnStartup(ApplicationLifetime);

            // The rest of the normal Avalonia init
            Globals.MainWindowViewModel = new MainWindowViewModel();

            Views.SlideRendererWorkerWindow slideRendererWorkerWindow = new Views.SlideRendererWorkerWindow()
                {
                mainWindowViewModel = Globals.MainWindowViewModel
                };
            slideRendererWorkerWindow.Show();


            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                desktop.MainWindow = new MainWindow
                    {
                    DataContext = Globals.MainWindowViewModel,
                    };
                }

            if (Debugger.IsAttached)
                {
                //TestModeApp.RunSongEditorWindow();
                }

            base.OnFrameworkInitializationCompleted();
            }
        }
    }
