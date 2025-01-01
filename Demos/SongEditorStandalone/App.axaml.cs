using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Data.SlideTheme;

namespace SongEditorStandalone;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // must create renderer BEFORE slides start loading during Globals.OnStartup (auto-loading previous playlist)
        SlideRendererWorkerWindow slideRendererWorkerWindow = new SlideRendererWorkerWindow();
        slideRendererWorkerWindow.Show();

        // Globals.Instance.OnStartup(ApplicationLifetime);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // PlaylistInstance x = Globals.Instance.MainViewModel.Playlist;
            // var window = new SongEditorWindow() { DataContext = new SongEditorViewModel(new SongItemInstance(x), x) };
            var window = new SlideEditorWindow();
            window.Closing += (sender, args) => { slideRendererWorkerWindow.Close(); };
            
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}