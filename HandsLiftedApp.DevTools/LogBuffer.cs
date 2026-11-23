using Avalonia.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using LogEventLevel = Avalonia.Logging.LogEventLevel;

namespace HandsLiftedApp.DevTools;

/// <summary>Ring buffer of recent app logs: Serilog events plus Avalonia's own warnings (binding errors etc.).</summary>
public static class LogBuffer
{
    public sealed record Entry(DateTime Time, string Level, string Source, string Message);

    private const int Capacity = 2000;
    private static readonly object Gate = new();
    private static readonly LinkedList<Entry> Entries = new();
    private static bool _installed;

    public static void Add(string level, string source, string message)
    {
        lock (Gate)
        {
            Entries.AddLast(new Entry(DateTime.Now, level, source, message));
            if (Entries.Count > Capacity) Entries.RemoveFirst();
        }
    }

    public static IReadOnlyList<Entry> Snapshot()
    {
        lock (Gate) return Entries.ToList();
    }

    public static void Clear()
    {
        lock (Gate) Entries.Clear();
    }

    public static void Install()
    {
        if (_installed) return;
        _installed = true;

        // Serilog: tee into a new root logger that also forwards to the existing one (which keeps its own level switch/sinks).
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new SerilogSink())
            .WriteTo.Logger(Log.Logger)
            .CreateLogger();

        // Avalonia: wrap the existing sink so warnings (binding failures) are captured as well as forwarded.
        Avalonia.Logging.Logger.Sink = new AvaloniaSink(Avalonia.Logging.Logger.Sink);
    }

    private sealed class SerilogSink : ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent e)
        {
            var src = e.Properties.TryGetValue("SourceContext", out var sc) ? sc.ToString().Trim('"') : "app";
            var msg = e.RenderMessage();
            if (e.Exception != null) msg += " | " + e.Exception.GetType().Name + ": " + e.Exception.Message;
            Add(e.Level.ToString(), src, msg);
        }
    }

    private sealed class AvaloniaSink(ILogSink? inner) : ILogSink
    {
        public bool IsEnabled(LogEventLevel level, string area) =>
            (inner?.IsEnabled(level, area) ?? false) || level >= LogEventLevel.Warning;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
            Record(level, area, source, messageTemplate, []);

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
            Record(level, area, source, messageTemplate, propertyValues);

        private void Record(LogEventLevel level, string area, object? source, string template, object?[] values)
        {
            if (level >= LogEventLevel.Warning)
            {
                string msg;
                try
                {
                    var i = 0;
                    msg = System.Text.RegularExpressions.Regex.Replace(template, @"\{[^}]*\}", _ => i < values.Length ? values[i++]?.ToString() ?? "null" : "?");
                }
                catch { msg = template; }
                Add(level.ToString(), "avalonia:" + area, msg + (source != null ? $" (source: {source.GetType().Name})" : ""));
            }

            if (inner?.IsEnabled(level, area) == true)
            {
                if (values.Length == 0) inner.Log(level, area, source, template);
                else inner.Log(level, area, source, template, values);
            }
        }
    }
}
