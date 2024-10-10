using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HandsLiftedApp.Utils;
using Serilog;
using Serilog.Templates;

namespace HandsLiftedApp.Core
{
    public static class Logging
    {
        public static void InitLogging()
        {
            if (OperatingSystem.IsWindows() && !Debugger.IsAttached)
            {
                ConsoleUtils.AllocConsole();
            }
            //var myWriter = new ConsoleTraceListener();
            //Trace.Listeners.Add(myWriter);

            ExpressionTemplate OUTPUT_TEMPLATE = new ExpressionTemplate(
                "[{@t:HH:mm:ss} {@l:u3}]{#if SourceContext is not null} [{SourceContext:l}]{#end} {@m}\n{@x}");
            Log.Logger = new LoggerConfiguration()
                //.MinimumLevel.Debug()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .WriteTo.Debug(formatter: OUTPUT_TEMPLATE)
                .WriteTo.File(path: "logs/visionscreens_app_log.txt", formatter: OUTPUT_TEMPLATE)
                .WriteTo.Console()
                .CreateLogger();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // Trace.Listeners.Add(new ConsoleTraceListener());

            Log.Information("VisionScreens App {Version} Build {GitHash} {BuildDateTime}", Assembly.GetExecutingAssembly().GetName().Version, BuildInfo.Version.GetGitHash(), BuildInfo.Version.GetBuildDateTime());
            var isAssemblyDebugBuild = Assembly.GetExecutingAssembly().GetCustomAttributes(false).OfType<DebuggableAttribute>().Any(da => da.IsJITTrackingEnabled);
            Log.Information("Debug: {IsAssemblyDebugBuild}", isAssemblyDebugBuild);
            Log.Information("Startup at {Now}", DateTime.Now);

            Log.Information("Runtime Identifier: {RID}", RuntimeInformation.RuntimeIdentifier);
            Log.Information("Avalonia {Version}", Assembly.GetAssembly(typeof(Avalonia.Application))?.GetName().Version);
            Log.Information("{DotNetVersion}", RuntimeInformation.FrameworkDescription);

            // Windows-only
            // https://stackoverflow.com/a/646500/
            // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            // {
            //     bool windowEventResult;
            //     s_event = new EventWaitHandle(false,
            //         EventResetMode.ManualReset, "HandsLiftedApp#startup", out windowEventResult);
            //
            //     if (!windowEventResult)
            //     {
            //         // TODO: Focus already running app instance
            //         return;
            //     }
            // }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Fatal("CurrentDomain_UnhandledExceptionEventArgs. Please report this error. {ex}", e.ExceptionObject);
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Fatal( e.Exception, "TaskScheduler_UnobservedTaskException. Please report this error.");
        }
    }
}