using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Services;
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

        /// <summary>
        /// Renders a slide thumbnail straight from its spec at <see cref="ThumbnailRenderSize.CurrentWidth"/>,
        /// avoiding the render-at-1920-then-shrink path of <see cref="CreateThumbnail"/>.
        /// </summary>
        public static Bitmap RenderThumbnail(SlideRenderSpec spec) =>
            RenderThumbnail(spec, ThumbnailRenderSize.CurrentWidth);

        /// <summary>Same as <see cref="RenderThumbnail(SlideRenderSpec)"/> at an explicit pixel width.</summary>
        public static Bitmap RenderThumbnail(SlideRenderSpec spec, int pixelWidth)
        {
            using var skBitmap = SlideRenderer.RenderThumbnail(spec, pixelWidth);
            return CopySkBitmapPixels(skBitmap, AlphaFormat.Opaque);
        }

        /// <param name="targetWidth">Thumbnail pixel width; null = <see cref="ThumbnailRenderSize.CurrentWidth"/>.</param>
        public static Bitmap? CreateThumbnail(Bitmap? source, int? targetWidth = null)
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
                var thumbWidth = Math.Max(1, targetWidth ?? ThumbnailRenderSize.CurrentWidth);
                var thumbHeight = Math.Max(1, (int)(source.Size.Height / source.Size.Width * thumbWidth));
                // Shrinking uses an area-averaging filter (BitmapScaling.Downscale); the cubic filter used for
                // enlarging aliases badly on big reductions, which is exactly what a thumbnail is.
                using SKBitmap? resizedBitmapSource = thumbWidth < bitmap.Width
                    ? BitmapScaling.Downscale(bitmap, thumbWidth, thumbHeight)
                    : bitmap.Resize(new SKImageInfo(thumbWidth, thumbHeight), new SKSamplingOptions(SKCubicResampler.Mitchell));
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