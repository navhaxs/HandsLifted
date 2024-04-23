using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Skia.Helpers;
using SkiaSharp;

namespace HandsLiftedApp.Core
{
    public class BitmapUtils
    {
        public static Bitmap? CreateThumbnail(Bitmap? source)
        {
            if (source == null)
            {
                return null;
            }

            using var bitmap = new SKBitmap(
                (int)source.Size.Width,
                (int)source.Size.Height,
                OperatingSystem.IsMacOS() ? SKColorType.Bgra8888 : SKImageInfo.PlatformColorType,
                SKAlphaType.Opaque);
            
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                canvas.DrawRect(0, 0, (int)source.Size.Width, (int)source.Size.Height,
                    new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.Black });

                int xres = (int)source.Size.Width;
                int yres = (int)source.Size.Height;
                int stride = (xres * 32 /*BGRA bpp*/ + 7) / 8;
                int bufferSize = yres * stride;
                IntPtr bufferPtr = Marshal.AllocCoTaskMem(bufferSize);

                using IDrawingContextImpl contextImpl =
                    DrawingContextHelper.WrapSkiaCanvas(canvas, SkiaPlatform.DefaultDpi);

                source.CopyPixels(new PixelRect(0, 0, xres, yres), bufferPtr, bufferSize, stride);
                bitmap.SetPixels(bufferPtr);
            }

            SKBitmap? resizedBitmap = bitmap.Resize(new SKImageInfo(500, (int)(source.Size.Height / source.Size.Width * 500)),
                SKFilterQuality.High);

            // BmpSharp as workaround to encode to BMP. This is MUCH faster than using SkiaSharp to encode to PNG.
            // https://github.com/mono/SkiaSharp/issues/320#issuecomment-582132563
            BmpSharp.BitsPerPixelEnum bitsPerPixel = resizedBitmap.BytesPerPixel == 4
                ? BmpSharp.BitsPerPixelEnum.RGBA32
                : BmpSharp.BitsPerPixelEnum.RGB24;
            BmpSharp.Bitmap bmp =
                new BmpSharp.Bitmap(resizedBitmap.Width, resizedBitmap.Height, resizedBitmap.Bytes, bitsPerPixel);
                
            // return as Avalonia bitmap
            return new Bitmap(bmp.GetBmpStream(fliped: true));
        }
    }
}