using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Utils;
using LibMpv.Client;
using Serilog;
using Serilog.Templates;
using System;
using System.Diagnostics;
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
                ConsoleUtils.AllocConsole();
                //var myWriter = new ConsoleTraceListener();
                //Trace.Listeners.Add(myWriter);

                ExpressionTemplate OUTPUT_TEMPLATE = new ExpressionTemplate("[{@t:HH:mm:ss} {@l:u3}]{#if SourceContext is not null} [{SourceContext:l}]{#end} {@m}\n{@x}");
                Log.Logger = new LoggerConfiguration()
                       //.MinimumLevel.Debug()
                       .MinimumLevel.Verbose()
                       .Enrich.FromLogContext()
                       .WriteTo.Debug(formatter: OUTPUT_TEMPLATE)
                       .WriteTo.File(path: "logs/visionscreens_app_log.txt", formatter: OUTPUT_TEMPLATE)
                       //.WriteTo.Console()
                       .CreateLogger();

                Log.Information("App startup");

                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

                Trace.Listeners.Add(new ConsoleTraceListener());

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

                
                Log.Information("Init LibMPV");
                FindLibMpv();
                Log.Information("Init LibMPV...OK");

                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception e)
            {
                // globally handle uncaught exceptions end up here
                Log.Fatal(e, "Global fatal exception. Please report this error.");

                Debugger.Launch();
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }

                // TODO UI here
            }
            finally
            {
                Log.Information("App shutdown. Bye!");
                Log.CloseAndFlush();
            }
        }

        private static void FindLibMpv()
        {
            var libMpvVersions = new[] { 2, 1 };

            // Search libmpv path on Linux
            if (FunctionResolverFactory.GetPlatformId() == PlatformID.Unix)
            {
                var libraryFolders = new[] {
                    "/lib/x86_64-linux-gnu",
                    "/usr/lib"
                };

                foreach (var folder in libraryFolders)
                {
                    foreach (var version in libMpvVersions)
                    {
                        var fullPath = System.IO.Path.Combine(folder, $"libmpv.so.{version}");
                        if (System.IO.File.Exists(fullPath))
                        {
                            //Set path and libmpv version
                            libmpv.RootPath = folder;
                            libmpv.LibraryVersionMap["libmpv"] = version;
                            return;
                        }
                    }
                }
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Fatal("CurrentDomain_UnhandledExceptionEventArgs. Please report this error.", e);
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Fatal("TaskScheduler_UnobservedTaskException. Please report this error.", e);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp() {
            // workaround for https://github.com/AvaloniaUI/AvaloniaVS/issues/250
            GC.KeepAlive(typeof(AvaloniaNDI.NDISendContainer).Assembly);

            return AppBuilder.Configure<App>()
                // TODO config
                .With(new SkiaOptions { MaxGpuResourceSizeBytes = 0x20000000 })
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
        }
    }
}


