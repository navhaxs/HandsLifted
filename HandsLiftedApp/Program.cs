using Avalonia;
using Avalonia.ReactiveUI;
using Serilog;
using Serilog.Templates;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HandsLiftedApp {
    class Program
    {
        static EventWaitHandle s_event;

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                ExpressionTemplate OUTPUT_TEMPLATE = new ExpressionTemplate("[{@t:HH:mm:ss} {@l:u3}]{#if SourceContext is not null} [{SourceContext:l}]{#end} {@m}\n{@x}");
                Log.Logger = new LoggerConfiguration()
                       .MinimumLevel.Debug()
                       .Enrich.FromLogContext()
                       .WriteTo.Debug(formatter: OUTPUT_TEMPLATE)
                       .WriteTo.File(path: "logs/visionscreens_app_log.txt", formatter: OUTPUT_TEMPLATE)
                       .CreateLogger();

                Log.Information("App startup");

                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

                // Windows-only
                // https://stackoverflow.com/a/646500/
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    bool windowEventResult;
                    s_event = new EventWaitHandle(false,
                        EventResetMode.ManualReset, "HandsLiftedApp#startup", out windowEventResult);

                    if (!windowEventResult)
                    {
                        // TODO: Focus already running app instance
                        return;
                    }
                }

                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception e)
            {
                // globally handle uncaught exceptions end up here
                Log.Fatal(e, "Global fatal exception. Please report this error.");

                // TODO UI here
            }
            finally
            {
                Log.Information("App shutdown");
                Log.CloseAndFlush();
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Fatal(e.ToString(), "TaskScheduler_UnobservedTaskException. Please report this error.");
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp() {
            // workaround for https://github.com/AvaloniaUI/AvaloniaVS/issues/250
            GC.KeepAlive(typeof(AvaloniaNDI.NDISendContainer).Assembly);

            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
        }

        //public static AppBuilder BuildAvaloniaApp()
        //{
        //    return AppBuilder.Configure<App>()
        //        .UsePlatformDetect()
        //        .LogToTrace()
        //        .UseReactiveUI()
        //        // customised MaxGpuResourceSizeBytes value (TODO: move to config file or ENV variable)
        //        .With(new SkiaOptions { MaxGpuResourceSizeBytes = 0x20000000 })
        //        // configure VLC to render within Avalonia - so we then have the pixels which we can send to NDI :)
        //        //.With(new VlcSharpOptions { RenderingOptions = LibVLCAvaloniaRenderingOptions.Avalonia) })
        //        //.UseVLCSharp(renderingOptions: LibVLCAvaloniaRenderingOptions.Avalonia)
        //    ;
        //}
    }
}


