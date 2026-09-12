using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HandsLiftedApp.Utils;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Templates;

namespace HandsLiftedApp.Core
{
    public static class Logging
    {
        public static readonly LoggingLevelSwitch LevelSwitch = new LoggingLevelSwitch(
#if DEBUG
            LogEventLevel.Verbose
#else
            LogEventLevel.Debug
#endif
        );

        public static void InitLogging()
        {
            if (LoggingConfig.Instance.LogLevel != null)
            {
                LevelSwitch.MinimumLevel = LoggingConfig.Instance.LogLevel.Value;
            }

            // Surfaces Seq send failures (unreachable server etc.), which Serilog otherwise swallows internally.
            Serilog.Debugging.SelfLog.Enable(msg => System.Diagnostics.Debug.WriteLine($"[SerilogSelfLog] {msg}"));

            ExpressionTemplate OUTPUT_TEMPLATE = new ExpressionTemplate(
                "[{@t:HH:mm:ss} {@l:u3}]{#if SourceContext is not null} [{SourceContext:l}]{#end} {@m}\n{@x}");
            var seqUrl = LoggingConfig.Instance.SeqUrl;

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LevelSwitch)
                .Enrich.FromLogContext()
                .WriteTo.Debug(formatter: OUTPUT_TEMPLATE);

            if (LoggingConfig.Instance.EnableLogConsole)
            {
                if (OperatingSystem.IsWindows())
                {
                    ConsoleUtils.AllocConsole();

                    // AllocConsole() doesn't repoint the process's std handles to the new console,
                    // so Console.Out/Error must be rebound via the CONOUT$ device directly.
                    Console.SetOut(new StreamWriter(new FileStream("CONOUT$", FileMode.Open, FileAccess.Write, FileShare.Write)) { AutoFlush = true });
                    Console.SetError(new StreamWriter(new FileStream("CONOUT$", FileMode.Open, FileAccess.Write, FileShare.Write)) { AutoFlush = true });
                }
                loggerConfig = loggerConfig.WriteTo.Console(formatter: OUTPUT_TEMPLATE);
            }
            
            if (LoggingConfig.Instance.EnableLogFile)
            {
                loggerConfig =
                    loggerConfig.WriteTo.File(path: "logs/visionscreens_app_log.txt", formatter: OUTPUT_TEMPLATE);
            }

            string? seqSkippedReason = null;
            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                if (Uri.TryCreate(seqUrl, UriKind.Absolute, out var seqUri) &&
                    (seqUri.Scheme == Uri.UriSchemeHttp || seqUri.Scheme == Uri.UriSchemeHttps))
                {
                    try
                    {
                        loggerConfig = loggerConfig.WriteTo.Seq(seqUrl);
                    }
                    catch (Exception ex)
                    {
                        seqSkippedReason = $"failed to configure Seq sink: {ex.Message}";
                    }
                }
                else
                {
                    seqSkippedReason = $"invalid SeqUrl in logging.yml: \"{seqUrl}\"";
                }
            }

            Log.Logger = loggerConfig.CreateLogger();

            if (seqSkippedReason != null)
            {
                Log.Warning("Seq logging disabled - {Reason}", seqSkippedReason);
            }

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // Trace.Listeners.Add(new ConsoleTraceListener());

            Log.Information("VisionScreens App {Version} Build {GitHash} {BuildDateTime}", Assembly.GetExecutingAssembly().GetName().Version, BuildInfo.Version.GetGitHash(), BuildInfo.Version.GetBuildDateTime());
            var isAssemblyDebugBuild = Assembly.GetExecutingAssembly().GetCustomAttributes(false).OfType<DebuggableAttribute>().Any(da => da.IsJITTrackingEnabled);
            Log.Information("Debug: {IsAssemblyDebugBuild}", isAssemblyDebugBuild);
            Log.Information("Startup at {Now}", DateTime.Now);
            Log.Information("Current Directory [{CurrentDirectory}]", Environment.CurrentDirectory);

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
            Log.Fatal("[HandsLiftedUnhandledException] {ex}", e.ExceptionObject);
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error( e.Exception, "[HandsLiftedUnobservedTaskException]. Please report this error.");
        }
    }
}