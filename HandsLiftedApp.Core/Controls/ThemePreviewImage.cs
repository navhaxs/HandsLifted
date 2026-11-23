using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;

namespace HandsLiftedApp.Core.Controls;

/// <summary>
/// A 16:9 preview of a slide theme, drawn from <see cref="ThemeThumbnailCache"/>. The bitmap is only fetched while the
/// control is attached to a visual tree (so virtualised or closed dropdown items cost nothing) and is refreshed,
/// debounced, when the theme is edited. A null <see cref="SlideTheme"/> measures to zero size.
/// </summary>
public class ThemePreviewImage : Control
{
    public static readonly StyledProperty<BaseSlideTheme?> SlideThemeProperty =
        AvaloniaProperty.Register<ThemePreviewImage, BaseSlideTheme?>(nameof(SlideTheme));

    private static readonly TimeSpan RefreshDebounce = TimeSpan.FromMilliseconds(250);
    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 128, 128, 128)), 1);
    private const double CornerRadius = 3;

    private IDisposable? _invalidatedSubscription;
    private bool _attached;

    static ThemePreviewImage()
    {
        AffectsMeasure<ThemePreviewImage>(SlideThemeProperty);
        AffectsRender<ThemePreviewImage>(SlideThemeProperty);
    }

    public BaseSlideTheme? SlideTheme
    {
        get => GetValue(SlideThemeProperty);
        set => SetValue(SlideThemeProperty, value);
    }

    /// <summary>The bitmap currently drawn. Null until attached, or when there is no theme.</summary>
    public Bitmap? Source { get; private set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Load();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _invalidatedSubscription?.Dispose();
        _invalidatedSubscription = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SlideThemeProperty && _attached)
            Load();
    }

    /// <summary>Fetches the current theme's bitmap and (re)subscribes to its edits. Only called while attached.</summary>
    private void Load()
    {
        _invalidatedSubscription?.Dispose();
        _invalidatedSubscription = null;

        var theme = SlideTheme;
        Source = ThemeThumbnailCache.GetOrRender(theme);
        InvalidateVisual();
        if (theme == null)
            return;

        _invalidatedSubscription = ThemeThumbnailCache.Invalidated
            .Where(t => ReferenceEquals(t, theme))
            .Throttle(RefreshDebounce, RxSchedulers.MainThreadScheduler)
            .Subscribe(_ =>
            {
                // A failed render (null) keeps the previous picture rather than blanking the tile.
                Source = ThemeThumbnailCache.GetOrRender(theme) ?? Source;
                InvalidateVisual();
            });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (SlideTheme == null)
            return default;

        var width = double.IsInfinity(availableSize.Width) ? ThemeThumbnailCache.PixelWidth : availableSize.Width;
        return new Size(width, width * 9 / 16);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (SlideTheme == null)
            return;

        var bounds = new Rect(Bounds.Size);
        if (Source != null)
        {
            using (context.PushClip(new RoundedRect(bounds, CornerRadius)))
                context.DrawImage(Source, new Rect(Source.Size), bounds);
        }

        context.DrawRectangle(null, BorderPen, bounds.Deflate(0.5), CornerRadius, CornerRadius);

        if (!string.IsNullOrEmpty(SlideTheme.DefaultMotionBackgroundVideoPath))
            DrawPlayBadge(context, bounds);
    }

    /// <summary>A small dark disc with a white play triangle, bottom-right: marks a theme whose background is a motion video.</summary>
    private static void DrawPlayBadge(DrawingContext context, Rect bounds)
    {
        var size = Math.Clamp(bounds.Width * 0.16, 12, 22);
        var margin = Math.Max(3, size * 0.25);
        var disc = new Rect(bounds.Right - size - margin, bounds.Bottom - size - margin, size, size);
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)), null, disc);

        var c = disc.Center;
        var h = size * 0.3;
        var triangle = new StreamGeometry();
        using (var g = triangle.Open())
        {
            g.BeginFigure(new Point(c.X - h * 0.4, c.Y - h), true);
            g.LineTo(new Point(c.X - h * 0.4, c.Y + h));
            g.LineTo(new Point(c.X + h * 0.8, c.Y));
            g.EndFigure(true);
        }
        context.DrawGeometry(Brushes.White, null, triangle);
    }
}
