using Avalonia;
using Avalonia.Headless;
using HandsLiftedApp.Core;
using ReactiveUI.Avalonia;

namespace HandsLiftedApp.UiTests;

/// <summary>Avalonia app configuration for headless runs: the real <see cref="App"/> (so styles/themes match), real Skia rendering, no display.</summary>
public static class HeadlessEntry
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseReactiveUI(_ => { })
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

[TestClass]
public static class HeadlessSession
{
    public static string DataDir { get; private set; } = "";
    private static HeadlessUnitTestSession? _session;

    [AssemblyInitialize]
    public static void Init(TestContext _)
    {
        // Redirect all app state away from the user's real %APPDATA%\HandsLifted. Must happen before
        // HandsLiftedApp.Core.Constants is first touched.
        DataDir = Path.Combine(Path.GetTempPath(), "HandsLiftedUiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDir);
        Environment.SetEnvironmentVariable("HANDSLIFTED_DATA_DIR", DataDir);

        _session = HeadlessUnitTestSession.StartNew(typeof(HeadlessEntry));
    }

    [AssemblyCleanup]
    public static void Cleanup()
    {
        _session?.Dispose();
        try { Directory.Delete(DataDir, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>Runs <paramref name="body"/> on the headless UI thread and returns when it completes.</summary>
    public static Task Run(Action body) => _session!.Dispatch(body, CancellationToken.None);

    public static Task<T> Run<T>(Func<T> body) => _session!.Dispatch(body, CancellationToken.None);

    public static Task Run(Func<Task> body) => _session!.Dispatch(body, CancellationToken.None);

    /// <summary>
    /// Runs an async <paramref name="body"/> on the headless UI thread, pumping its dispatcher (timers, continuations)
    /// until the body finishes, and surfaces its exceptions. Use this, not <see cref="Run(Func{Task})"/>, for any test
    /// that awaits: HeadlessUnitTestSession has no Dispatch(Func&lt;Task&gt;) overload, so <c>Run(Func&lt;Task&gt;)</c>
    /// binds to Dispatch&lt;Task&gt;(Func&lt;T&gt;), which returns after the body's first await without observing it
    /// (its assertions can never fail the test, and timers never fire).
    /// </summary>
    public static Task RunAsync(Func<Task> body) =>
        _session!.Dispatch<object?>(async () => { await body(); return null; }, CancellationToken.None);
}
