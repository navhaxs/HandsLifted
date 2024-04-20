﻿using System;
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
        public static Bitmap CreateThumbnail(Bitmap? source)
        {
            if (source == null)
            {
                return null;
            }
            using (SKBitmap bitmap = new SKBitmap((int)source.Size.Width, (int)source.Size.Height))
            {
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

                // BmpSharp as workaround to encode to BMP. This is MUCH faster than using SkiaSharp to encode to PNG.
                // https://github.com/mono/SkiaSharp/issues/320#issuecomment-582132563
                BmpSharp.BitsPerPixelEnum bitsPerPixel = bitmap.BytesPerPixel == 4
                    ? BmpSharp.BitsPerPixelEnum.RGBA32
                    : BmpSharp.BitsPerPixelEnum.RGB24;
                BmpSharp.Bitmap bmp =
                    new BmpSharp.Bitmap(bitmap.Width, bitmap.Height, bitmap.Bytes, bitsPerPixel);
                Bitmap destBitmap = new Bitmap(bmp.GetBmpStream(fliped: true));

                var dstSize = new PixelSize(500, (int)(source.Size.Height/ source.Size.Width * 500));
                return destBitmap.CreateScaledBitmap(dstSize);
            }
        }
    }
}