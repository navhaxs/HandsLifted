using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Data.Data.Models.Slides;
using ReactiveUI;

namespace SlideEditorStandalone;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    public const string APP_STATE_FILEPATH = "appstate.xml";

    public override void OnFrameworkInitializationCompleted()
    {
        // Globals.Instance.OnStartup(ApplicationLifetime);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            RxSuspension.SuspensionHost.CreateNewAppState = () => new CustomSlide();
            RxSuspension.SuspensionHost.SetupDefaultSuspendResume(
                new XmlSuspensionDriver<CustomSlide>(APP_STATE_FILEPATH));

            // Load the saved view model state.
            CustomSlide data = RxSuspension.SuspensionHost.GetAppState<CustomSlide>();

            // PlaylistInstance x = Globals.Instance.MainViewModel.Playlist;
            // var window = new SongEditorWindow() { DataContext = new SongEditorViewModel(new SongItemInstance(x), x) };
            var window = new MainWindow() { DataContext = new ExampleMediaGroupItemInstance() { }};

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}