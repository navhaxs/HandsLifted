using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Common;
using HandsLiftedApp.Core;
using Serilog;

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

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            // globally handle uncaught exceptions end up here
            Log.Fatal(e, "Global fatal exception, please report this error!");

            Debugger.Launch();
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            // TODO UI here
        }
        finally
        {
            Log.Information("Clean app shutdown. Bye!");
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // .WithInterFont() this font is gross
            .LogToTrace()
            .UseReactiveUI();
}