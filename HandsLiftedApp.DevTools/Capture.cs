using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace HandsLiftedApp.DevTools;

internal static class Capture
{
    public static string OutputDir
    {
        get
        {
            var dir = Path.Combine(Path.GetTempPath(), "handslifted-devtools");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Renders a visual (a whole window/popup, or one control) to PNG bytes through Avalonia's own renderer.
    /// Limitation: native-hosted layers (libmpv motion background / video, WebView) are not part of the visual
    /// composition and will not appear.
    /// </summary>
    public static byte[] Png(Visual visual, double scale)
    {
        var tl = TopLevel.GetTopLevel(visual);
        var rs = tl?.RenderScaling ?? 1.0;
        var size = visual is TopLevel t ? t.ClientSize : visual.Bounds.Size;
        if (size.Width < 1 || size.Height < 1) throw new DevToolsException("target has no size (not laid out / collapsed)", 422);

        var px = new PixelSize((int)Math.Ceiling(size.Width * rs * scale), (int)Math.Ceiling(size.Height * rs * scale));
        using var rtb = new RenderTargetBitmap(px, new Vector(96 * rs * scale, 96 * rs * scale));
        rtb.Render(visual);
        using var ms = new MemoryStream();
        rtb.Save(ms);
        return ms.ToArray();
    }
}

internal static class PropDump
{
    /// <summary>Avalonia properties of a visual: only those with a local/styled value by default, or everything with all=true.</summary>
    public static Dictionary<string, object?> Of(AvaloniaObject obj, bool all)
    {
        var reg = AvaloniaPropertyRegistry.Instance;
        var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in reg.GetRegistered(obj.GetType()).Concat(reg.GetRegisteredAttached(obj.GetType())))
        {
            try
            {
                if (!all && !obj.IsSet(p)) continue;
                result[p.Name] = Format(obj.GetValue(p));
            }
            catch (Exception ex)
            {
                result[p.Name] = "<error: " + ex.Message + ">";
            }
        }
        return new Dictionary<string, object?>(result);
    }

    private static object? Format(object? v) => v switch
    {
        null => null,
        string s => s.Length > 200 ? s[..200] + "…" : s,
        bool or int or long or double or float => v,
        Enum e => e.ToString(),
        _ => v.ToString() is { } s2 ? (s2.Length > 200 ? s2[..200] + "…" : s2) : v.GetType().Name,
    };

    public static object? Convert(string text, Type target)
    {
        var t = Nullable.GetUnderlyingType(target) ?? target;
        if (text == "null") return null;
        if (t == typeof(string)) return text;
        if (t == typeof(bool)) return bool.Parse(text);
        if (t.IsEnum) return Enum.Parse(t, text, ignoreCase: true);
        var conv = TypeDescriptor.GetConverter(t);
        if (conv.CanConvertFrom(typeof(string))) return conv.ConvertFromInvariantString(text);
        throw new DevToolsException($"cannot convert '{text}' to {t.Name}");
    }
}
