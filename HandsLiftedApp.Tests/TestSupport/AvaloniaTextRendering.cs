using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
            // No real windowing; just the one service XAML-built controls need: Cursor="Hand" (e.g. NumericSliderBox,
            // the editor toggle style) asks for a cursor factory as soon as the control is constructed.
            .UseWindowingSubsystem(RegisterStubCursorFactory)
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

    // ICursorFactory is marked not-implementable by user code, and Avalonia.Headless isn't referenced, so borrow the
    // stub the XAML previewer uses (it ships in Avalonia.DesignerSupport, part of the Avalonia package) and register
    // it through the (internal in Avalonia 12) AvaloniaLocator.CurrentMutable.Bind<T>().ToConstant(...).
    private static void RegisterStubCursorFactory()
    {
        const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var stubType = Type.GetType("Avalonia.DesignerSupport.Remote.CursorFactoryStub, Avalonia.DesignerSupport", throwOnError: true)!;
        var stub = (ICursorFactory)Activator.CreateInstance(stubType, nonPublic: true)!;

        var locator = typeof(AvaloniaLocator).GetProperty("CurrentMutable", any)!.GetValue(null)!;
        var helper = locator.GetType().GetMethod("Bind", any)!.MakeGenericMethod(typeof(ICursorFactory)).Invoke(locator, null)!;
        var toConstant = helper.GetType().GetMethod("ToConstant", any)!;
        if (toConstant.ContainsGenericParameters) toConstant = toConstant.MakeGenericMethod(typeof(ICursorFactory));
        toConstant.Invoke(helper, [stub]);
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
