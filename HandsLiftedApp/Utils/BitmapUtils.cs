using Avalonia;
using Avalonia.Media.Imaging;
using Serilog;
using System.IO;

namespace HandsLiftedApp.Utils
{
    internal static class BitmapUtils
    {
        public static Bitmap? LoadBitmap(string filepath, int targetWidth)
        {
            // TODO logging
            Log.Information($"Loading {filepath} @ {targetWidth}");
            if (File.Exists(filepath))
            {
                using (Stream fs = File.OpenRead(filepath))
                {
                    // TODO: AccessViolationException thrown here cannot be caught, crashes entire app :(
                    // replace this with own library
                    if (!"https://github.com/mono/SkiaSharp/issues/2645".Contains("2645"))
                    {
                        // this should be the expected implementation
                        return Bitmap.DecodeToWidth(fs, targetWidth, BitmapInterpolationMode.MediumQuality);
                    }
                    else
                    {
                        // this implementation is slower but does not crash due to https://github.com/mono/SkiaSharp/issues/2645
                        using Bitmap fullImage = new(fs);
                        var newHeight = fullImage.Size.Width > targetWidth
                            ? targetWidth / fullImage.Size.Width * fullImage.Size.Height
                            : fullImage.Size.Height;

                        var thumbnail = fullImage.CreateScaledBitmap(new PixelSize(targetWidth, (int)newHeight));

                        return thumbnail;
                    }
                }
            }
            return null;
        }
    }
}
