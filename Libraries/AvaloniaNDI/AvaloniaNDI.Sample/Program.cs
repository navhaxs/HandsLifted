using Avalonia;
using AvaloniaUI.DiagnosticsSupport;
using System;

namespace AvaloniaNDI.Sample
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {

            // workaround for https://github.com/AvaloniaUI/AvaloniaVS/issues/250
            GC.KeepAlive(typeof(AvaloniaNDI.NDISendContainer).Assembly);

            var builder = AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
#if DEBUG
            builder = builder.WithDeveloperTools();
#endif
            return builder;
        }
    }
}
