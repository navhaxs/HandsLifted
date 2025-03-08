using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.ViewModels.Editor.FreeText;
using HandsLiftedApp.Core.ViewModels.SlideElementEditor;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Core.Views.Editors;
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
        // must create renderer BEFORE slides start loading during Globals.OnStartup (auto-loading previous playlist)
        SlideRendererWorkerWindow slideRendererWorkerWindow = new SlideRendererWorkerWindow();
        slideRendererWorkerWindow.Show();

        // Globals.Instance.OnStartup(ApplicationLifetime);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            
            var suspension = new AutoSuspendHelper(desktop);
            RxApp.SuspensionHost.CreateNewAppState = () => new CustomSlide();
            RxApp.SuspensionHost.SetupDefaultSuspendResume(
                new XmlSuspensionDriver<CustomSlide>(APP_STATE_FILEPATH));
            suspension.OnFrameworkInitializationCompleted();

            // Load the saved view model state.
            CustomSlide data = RxApp.SuspensionHost.GetAppState<CustomSlide>();
            
            // PlaylistInstance x = Globals.Instance.MainViewModel.Playlist;
            // var window = new SongEditorWindow() { DataContext = new SongEditorViewModel(new SongItemInstance(x), x) };
            var window = new SlideEditorWindow() { DataContext = new FreeTextSlideEditorViewModel() { Slide = data }};
            
            window.Closing += (sender, args) => { slideRendererWorkerWindow.Close(); };
            
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}