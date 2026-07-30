using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace HandsLiftedApp.Core
{
    public class BitmapUtils
    {
        public static Bitmap SKBitmapToAvalonia(SKBitmap skBitmap)
        {
            return CopySkBitmapPixels(skBitmap,
                skBitmap.AlphaType == SKAlphaType.Opaque ? AlphaFormat.Opaque : AlphaFormat.Premul);
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

                    source.CopyPixels(new PixelRect(0, 0, xres, yres), bufferPtr, bufferSize, stride);
                    bitmap.SetPixels(bufferPtr);
                }

                // bitmap.SetPixels only points at bufferPtr, it does not copy - Resize below
                // reads through that pointer into a fresh independent buffer, so bufferPtr
                // must stay alive until after Resize completes.
                using SKBitmap? resizedBitmapSource = bitmap.Resize(
                    new SKImageInfo(500, (int)(source.Size.Height / source.Size.Width * 500)),
                    SKFilterQuality.High);
                return resizedBitmapSource == null ? null : CopySkBitmapPixels(resizedBitmapSource, AlphaFormat.Opaque);
            }
            finally
            {
                if (bufferPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(bufferPtr);
                }
            }
        }

        // Copies an SKBitmap's pixel buffer directly into a new Avalonia WriteableBitmap. Replaces
        // what used to be a PNG/BMP encode-then-decode round trip (SKBitmapToAvalonia previously
        // encoded to PNG and re-decoded via `new Bitmap(stream)`; CreateThumbnail previously
        // BMP-encoded via BmpSharp and re-decoded the same way) - both ran on every slide render,
        // briefly spiking memory 2-3x for pure transient encode/decode buffers.
        private static unsafe WriteableBitmap CopySkBitmapPixels(SKBitmap skBitmap, AlphaFormat alphaFormat)
        {
            var writeableBitmap = new WriteableBitmap(
                new PixelSize(skBitmap.Width, skBitmap.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                alphaFormat);

            using var framebuffer = writeableBitmap.Lock();

            var srcPtr = (byte*)skBitmap.GetPixels().ToPointer();
            var dstPtr = (byte*)framebuffer.Address.ToPointer();
            int srcStride = skBitmap.RowBytes;
            int dstStride = framebuffer.RowBytes;
            int rowBytes = Math.Min(srcStride, dstStride);

            for (int y = 0; y < skBitmap.Height; y++)
            {
                Buffer.MemoryCopy(srcPtr + (long)y * srcStride, dstPtr + (long)y * dstStride, dstStride, rowBytes);
            }

            return writeableBitmap;
        }
    }
}