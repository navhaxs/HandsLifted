using Avalonia.Media.Imaging;
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
                    return Bitmap.DecodeToWidth(imageStream, width);
                }
            }
            return null;
        }
    }
}
