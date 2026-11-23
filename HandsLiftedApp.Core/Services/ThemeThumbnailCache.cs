using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Services;

/// <summary>
/// One rendered thumbnail per theme object, shared by every control that shows that theme. A thumbnail is dropped (and
/// <see cref="Invalidated"/> fires) when the theme is edited, so the next <see cref="GetOrRender"/> draws a fresh one.
/// UI thread only. Entries are held weakly, so a deleted theme's bitmap and subscription go away with the theme.
/// </summary>
public static class ThemeThumbnailCache
{
    /// <summary>Thumbnail width in pixels (16:9). Wide enough for a theme picker tile on a high-DPI screen.</summary>
    public const int PixelWidth = 320;

    private sealed class Entry
    {
        public Bitmap? Bitmap;
        public IDisposable? Subscription;
    }

    private static readonly ConditionalWeakTable<BaseSlideTheme, Entry> Entries = new();
    private static readonly Subject<BaseSlideTheme> InvalidatedSubject = new();

    /// <summary>Fires on the UI thread after an edit that changes how the theme looks (not for derived <c>Calculated*</c> values).</summary>
    public static IObservable<BaseSlideTheme> Invalidated { get; } =
        InvalidatedSubject.ObserveOn(RxSchedulers.MainThreadScheduler);

    /// <summary>Number of thumbnails rendered so far. Test hook.</summary>
    public static int RenderCount { get; private set; }

    /// <summary>The theme's thumbnail, rendered on first request and cached until the theme is edited. Null for a null theme or if rendering failed.</summary>
    public static Bitmap? GetOrRender(BaseSlideTheme? theme)
    {
        if (theme == null)
            return null;

        var entry = Entries.GetValue(theme, Subscribe);
        if (entry.Bitmap != null)
            return entry.Bitmap;

        try
        {
            var spec = ThemePreviewSpecBuilder.Build(theme);
            // A replaced bitmap is never disposed: several controls may still be showing it. The GC reclaims it.
            entry.Bitmap = BitmapUtils.RenderThumbnail(spec, PixelWidth);
            RenderCount++;
            return entry.Bitmap;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Theme thumbnail render failed for theme {ThemeName}", theme.Name);
            return null;
        }
    }

    /// <summary>
    /// Whether a property change affects a theme's appearance. <c>Calculated*</c> properties are derived values that
    /// update asynchronously after the real property changed, so they must not trigger a second render.
    /// </summary>
    internal static bool IsRelevantChange(string propertyName) =>
        !propertyName.StartsWith("Calculated", StringComparison.Ordinal);

    private static Entry Subscribe(BaseSlideTheme theme)
    {
        var entry = new Entry();
        // The subscription captures only the entry and a weak reference: a strong reference to the theme would keep
        // the theme (the table key) alive forever and defeat the weak table.
        var weakTheme = new WeakReference<BaseSlideTheme>(theme);
        entry.Subscription = theme.Changed
            .Where(e => IsRelevantChange(e.PropertyName ?? ""))
            .Subscribe(_ =>
            {
                entry.Bitmap = null;
                if (weakTheme.TryGetTarget(out var t))
                    InvalidatedSubject.OnNext(t);
            });
        return entry;
    }
}
