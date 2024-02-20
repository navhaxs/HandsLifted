using System;
using Avalonia;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Common;
using HandsLiftedApp.Core;

namespace HandsLiftedApp.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Logging.InitLogging();
        if (OperatingSystem.IsWindows())
        {
            Caffeine.KeepAwake(true);
            // TODO macOS: keep awake
        }
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // .WithInterFont() this font is gross
            .LogToTrace()
            .UseReactiveUI();
}