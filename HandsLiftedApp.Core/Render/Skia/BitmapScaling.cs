using SkiaSharp;

namespace HandsLiftedApp.Core.Render.Skia;

public static class BitmapScaling
{
    // Exactly 2:1 with bilinear sampling lands each output pixel on the corner of a 2x2 source block: a perfect box average.
    private static readonly SKSamplingOptions Halve = new(SKFilterMode.Linear, SKMipmapMode.None);

    // For the last step (less than 2:1 remaining). Catmull-Rom keeps edges and text crisper than bilinear/trilinear.
    private static readonly SKSamplingOptions Finish = new(SKCubicResampler.CatmullRom);

    /// <summary>
    /// Scales <paramref name="source"/> to <paramref name="width"/> x <paramref name="height"/> without aliasing:
    /// repeated exact 2:1 box averages while at least 2:1 remains (every source pixel contributes, so fine text and
    /// thin lines are averaged rather than skipped), then one sharp cubic step for the remaining under-2:1 reduction.
    /// The cubic filter that <c>SKFilterQuality.High</c> maps to reads only a few source pixels per output pixel
    /// and aliases badly on big reductions; trilinear mipmaps avoid that but blur.
    /// Returns null if Skia cannot allocate or scale. The caller owns the result.
    /// </summary>
    public static SKBitmap? Downscale(SKBitmap source, int width, int height)
    {
        width = System.Math.Max(1, width);
        height = System.Math.Max(1, height);

        var current = source;
        var owned = false; // true once `current` is an intermediate we created (never dispose the caller's source)
        try
        {
            while (current.Width / 2 >= width && current.Height / 2 >= height)
            {
                var half = new SKImageInfo(current.Width / 2, current.Height / 2, source.ColorType, source.AlphaType);
                var next = current.Resize(half, Halve);
                if (owned)
                    current.Dispose();
                if (next == null)
                    return null;
                current = next;
                owned = true;
            }

            return current.Resize(new SKImageInfo(width, height, source.ColorType, source.AlphaType), Finish);
        }
        finally
        {
            if (owned)
                current.Dispose();
        }
    }
}
