using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Skia;

namespace HandsLiftedApp.Tests.TestSupport;

// Lets tests lay out and render real Avalonia controls (text shaping, fonts, RenderTargetBitmap) with no window.
// Call only from inside DispatcherTestThread.Run (the thread Avalonia's platform was initialised on).
internal static class AvaloniaTextRendering
{
    public static void EnsureInitialized()
    {
        if (Application.Current != null) return;
        AppBuilder.Configure<Application>()
            .UseStandardRuntimePlatformSubsystem()
            .UseWindowingSubsystem(() => { })
            .UseSkia()
            .UseHarfBuzz()
            .SetupWithoutStarting();
    }

    /// <summary>Measures and arranges <paramref name="content"/> at <paramref name="width"/> x <paramref name="height"/>.</summary>
    public static Border Layout(Control content, double width, double height, IBrush? background = null)
    {
        var root = new Border { Width = width, Height = height, Background = background ?? Brushes.Black, Child = content };
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        return root;
    }

    public static void SavePng(Control laidOutRoot, string path)
    {
        var size = new PixelSize((int)laidOutRoot.Width, (int)laidOutRoot.Height);
        using var rtb = new RenderTargetBitmap(size, new Vector(96, 96));
        rtb.Render(laidOutRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        rtb.Save(path);
    }

    /// <summary>Set HANDSLIFTED_TEST_IMAGE_DIR to have render tests write PNGs there for eyeballing.</summary>
    public static void MaybeSavePng(Control laidOutRoot, string name)
    {
        var dir = Environment.GetEnvironmentVariable("HANDSLIFTED_TEST_IMAGE_DIR");
        if (!string.IsNullOrEmpty(dir)) SavePng(laidOutRoot, Path.Combine(dir, name + ".png"));
    }
}
