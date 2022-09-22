using Avalonia;
using Avalonia.ReactiveUI;
using LibVLCSharp.Avalonia;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HandsLiftedApp
{
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
                Log.Logger = new LoggerConfiguration()
                       .MinimumLevel.Debug()
                       .WriteTo.Console()
                       .WriteTo.File("logs/myapp.txt")
                       .CreateLogger();


                Log.Information("Hello, world!");

                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

                // Windows-only
                // https://stackoverflow.com/a/646500/
                bool created;
                s_event = new EventWaitHandle(false,
                    EventResetMode.ManualReset, "HandsLiftedApp#startup", out created);
                if (created)
                {
                    // todo: globally handle unhandled, uncaught exceptions here
                    BuildAvaloniaApp()
                        .StartWithClassicDesktopLifetime(args);
                }
                else
                {
                    // TODO: Focus already running app instance
                }
            }
            catch (Exception e)
            {
                // here we can work with the exception, for example add it to our log file
                Log.Fatal(e, "Something very bad happened");
            }
            finally
            {
                // This block is optional. 
                // Use the finally-block if you need to clean things up or similar
                Log.CloseAndFlush();
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Fatal(e.ToString(), "TaskScheduler_UnobservedTaskException");
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            // workaround for https://github.com/AvaloniaUI/AvaloniaVS/issues/250
            GC.KeepAlive(typeof(AvaloniaNDI.NDISendContainer).Assembly);

            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI()
                .With(new SkiaOptions { MaxGpuResourceSizeBytes = 0x20000000 })
                .UseVLCSharp(renderingOptions: LibVLCAvaloniaRenderingOptions.Avalonia)
            ;
        }
    }
}


