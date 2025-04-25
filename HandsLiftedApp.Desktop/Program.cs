using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Rendering.Composition;
using Avalonia.WebView.Desktop;
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
                throw e;
            }

            // TODO UI here
            if (Console.LargestWindowWidth != 0 && !Console.IsOutputRedirected)
            {
                /* we have a console */
                Console.ReadKey();
            }
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
            .UseDesktopWebView()
            .With(new CompositionOptions()
            {
                // https://github.com/AvaloniaUI/Avalonia/discussions/17808
                UseRegionDirtyRectClipping = true
            })
            .With<Win32PlatformOptions>(new Win32PlatformOptions()
                {
                    WinUICompositionBackdropCornerRadius = 0
                     // UseWindowsUIComposition = false
                })
            .LogToTrace()
            .UseReactiveUI();
}