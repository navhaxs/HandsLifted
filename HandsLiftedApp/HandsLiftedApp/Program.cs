using Avalonia;
using Avalonia.ReactiveUI;
using LibVLCSharp.Avalonia;
using System;
using System.Threading;

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


