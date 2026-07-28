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
        public static Bitmap SKBitmapToAvalonia(SKBitmap skBitmap)
        {
            using var data = skBitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new System.IO.MemoryStream(data.ToArray());
            return new Bitmap(stream);
        }

        public static SKBitmap? AvaloniaToSKBitmap(Bitmap avaBitmap)
        {
            using var ms = new System.IO.MemoryStream();
            avaBitmap.Save(ms);
            ms.Seek(0, System.IO.SeekOrigin.Begin);
            return SKBitmap.Decode(ms);
        }

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
            
            IntPtr bufferPtr = IntPtr.Zero;
            try
            {
                using (SKCanvas canvas = new SKCanvas(bitmap))
                {
                    canvas.DrawRect(0, 0, (int)source.Size.Width, (int)source.Size.Height,
                        new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.Black });

                    int xres = (int)source.Size.Width;
                    int yres = (int)source.Size.Height;
                    int stride = (xres * 32 /*BGRA bpp*/ + 7) / 8;
                    int bufferSize = yres * stride;
                    bufferPtr = Marshal.AllocCoTaskMem(bufferSize);

                    using IDrawingContextImpl contextImpl =
                        DrawingContextHelper.WrapSkiaCanvas(canvas, SkiaPlatform.DefaultDpi);

                    source.CopyPixels(new PixelRect(0, 0, xres, yres), bufferPtr, bufferSize, stride);
                    bitmap.SetPixels(bufferPtr);
                }

                // bitmap.SetPixels only points at bufferPtr, it does not copy - Resize below
                // reads through that pointer into a fresh independent buffer, so bufferPtr
                // must stay alive until after Resize completes.
                using SKBitmap? resizedBitmapSource = bitmap.Resize(
                    new SKImageInfo(500, (int)(source.Size.Height / source.Size.Width * 500)),
                    SKFilterQuality.High);
                return EncodeToAvaloniaBitmap(resizedBitmapSource);
            }
            finally
            {
                if (bufferPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(bufferPtr);
                }
            }
        }

        private static Bitmap? EncodeToAvaloniaBitmap(SKBitmap? resizedBitmap)
        {
            if (resizedBitmap == null)
            {
                return null;
            }

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