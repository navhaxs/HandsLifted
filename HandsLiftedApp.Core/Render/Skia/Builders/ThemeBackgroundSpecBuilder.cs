// HandsLiftedApp.Core/Render/Skia/Builders/ThemeBackgroundSpecBuilder.cs
using System;
using Avalonia.Media;
using HandsLiftedApp.Data.SlideTheme;
using SkiaSharp;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

/// <summary>
/// Turns a theme's background settings (colour, image, motion video, opacity) into a <see cref="BackgroundSpec"/>.
/// Shared by every theme-driven spec builder so they all blend the same way.
/// </summary>
public static class ThemeBackgroundSpecBuilder
{
    /// <summary>
    /// Precedence (first match wins): a video frame bitmap, a motion video, a theme image, the plain colour.
    /// <see cref="BaseSlideTheme.BackgroundOpacity"/> only applies to the first three (media over the colour).
    /// </summary>
    /// <remarks>
    /// A live motion video plays in its own layer BELOW the slide canvas, so it can't be blended here. Instead the
    /// canvas paints the theme colour OVER it at alpha (1 - opacity): source-over gives colour*(1-o) + video*o, the same
    /// as the video at opacity o over the colour. At 100 nothing is painted (<see cref="TransparentBackground"/>).
    /// </remarks>
    public static BackgroundSpec Build(BaseSlideTheme? theme, bool hasMotionBackground, SKBitmap? videoFrame = null)
    {
        if (theme == null)
        {
            if (videoFrame != null) return new SkiaBitmapBackground(videoFrame);
            return hasMotionBackground ? new TransparentBackground() : new SolidBackground(SKColors.Black);
        }

        var colour = ToSkColor(theme.BackgroundAvaloniaColour);
        float opacity = theme.BackgroundOpacity / 100f;

        if (videoFrame != null)
            return new SkiaBitmapBackground(videoFrame, colour, opacity);

        if (hasMotionBackground)
        {
            if (opacity >= 1f) return new TransparentBackground();
            return new SolidBackground(colour.WithAlpha((byte)Math.Round(colour.Alpha * (1f - opacity))));
        }

        if (!string.IsNullOrEmpty(theme.BackgroundGraphicFilePath))
            return new ImageBackground(theme.BackgroundGraphicFilePath, colour, opacity);

        return new SolidBackground(colour);
    }

    private static SKColor ToSkColor(Color c) => new(c.R, c.G, c.B, c.A);
}
