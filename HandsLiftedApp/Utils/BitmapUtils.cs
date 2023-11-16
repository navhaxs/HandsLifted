using Avalonia.Media.Imaging;
using Serilog;
using System.IO;

namespace HandsLiftedApp.Utils
    {
    internal static class BitmapUtils
        {
        public static Bitmap? LoadBitmap(string filepath, int width)
            {
            // TODO logging
            Log.Information($"Loading {filepath} @ {width}");
            if (File.Exists(filepath))
                {
                using (Stream imageStream = File.OpenRead(filepath))
                    {
                    // TODO: AccessViolationException thrown here cannot be caught, crashes entire app :(
                    // replace this with own library
                    return Bitmap.DecodeToWidth(imageStream, width);
                    }
                }
            return null;
            }
        }
    }
