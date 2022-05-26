using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using System;
using LibVLCSharp.Avalonia;
using System.Threading;

namespace HandsLiftedApp
{
    class Program
    {
        static EventWaitHandle s_event;

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args) {

            // Windows-only
            // https://stackoverflow.com/a/646500/
            bool created;
            s_event = new EventWaitHandle(false,
                EventResetMode.ManualReset, "HandsLiftedApp#startup", out created);
            if (created)
            {
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
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI()
                .With(new SkiaOptions { MaxGpuResourceSizeBytes = 0x20000000 })
                .UseVLCSharp(renderingOptions: LibVLCAvaloniaRenderingOptions.Avalonia)
            ;
    }
}


