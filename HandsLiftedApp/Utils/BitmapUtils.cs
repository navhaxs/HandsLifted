using Avalonia.Media.Imaging;
using System;
using System.IO;

namespace HandsLiftedApp.Utils
{
    internal static class BitmapUtils
    {
        public static Bitmap? LoadBitmap(string filepath, int width)
        {
            // TODO logging
            if (File.Exists(filepath))
            {
                using (Stream imageStream = File.OpenRead(filepath))
                {
                    // TODO: Attempted to read or write protected memory. This is often an indication that other memory is corrupt.
                    try
                    {
                        return Bitmap.DecodeToWidth(imageStream, width);
                    }
                    catch (AccessViolationException)
                    {
                        return null;
                    }
                }
            }
            return null;
        }
    }
}
