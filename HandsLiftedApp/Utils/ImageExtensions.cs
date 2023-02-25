using System.Drawing;
using System.Drawing.Imaging;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace HandsLiftedApp.Utils
{
    internal static class ImageExtensions
    {
        public static Bitmap ConvertToAvaloniaBitmap(this Image bitmap)
        {
            if (bitmap == null)
                return null;
            return ConvertToAvaloniaBitmap(new System.Drawing.Bitmap(bitmap));
        }

        public static Bitmap ConvertToAvaloniaBitmap(this System.Drawing.Bitmap bitmapTmp)
        {
            if (bitmapTmp == null)
                return null;

            var bitmapdata = bitmapTmp.LockBits(new Rectangle(0, 0, bitmapTmp.Width, bitmapTmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            Bitmap bitmap1 = new Bitmap(Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul,
                bitmapdata.Scan0,
                new Avalonia.PixelSize(bitmapdata.Width, bitmapdata.Height),
                new Avalonia.Vector(96, 96),
                bitmapdata.Stride);
            bitmapTmp.UnlockBits(bitmapdata);
            bitmapTmp.Dispose();
            return bitmap1;
        }
    }
}
