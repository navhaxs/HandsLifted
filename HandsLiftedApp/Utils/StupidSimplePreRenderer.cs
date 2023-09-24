using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HandsLiftedApp.Views;
using SkiaSharp;
using System.Drawing.Imaging;
using System.IO;

namespace HandsLiftedApp.Utils
{
    public static class StupidSimplePreRenderer
    {
        public static Bitmap Render(Control control)
        {
            Bitmap rtb;
            //await Dispatcher.UIThread.InvokeAsync(() =>
            //{
                using (SKBitmap bitmap = new SKBitmap(1920, 1080))
                {
                    using (SKCanvas canvas = new SKCanvas(bitmap))
                    {

                        canvas.DrawRect(0, 0, 1920, 1080, new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.Red });

                        //    int xres = 1920;
                        //    int yres = 1080;
                        //    int stride = (xres * 32/*BGRA bpp*/ + 7) / 8;
                        //    int bufferSize = yres * stride;
                        //    IntPtr bufferPtr = Marshal.AllocCoTaskMem(bufferSize);

                        //    // define the surface properties
                        //    var info = new SKImageInfo(xres, yres);

                        //    // construct a surface around the existing memory
                        //    var destinationSurface = SKSurface.Create(info, bufferPtr, info.RowBytes);

                        //    SKImage image = destinationSurface.Snapshot();
                    }
                
                    //WriteableBitmap avaloniaBitmap = new WriteableBitmap(new PixelSize(1920, 1080), new Vector(96.0, 96.0));
                    //avaloniaBitmap.pixel

                    var image = SKImage.FromBitmap(bitmap);
                    SKData encoded = image.Encode();
                    Stream stream = encoded.AsStream();
                    rtb = new Bitmap(stream);
                
                    //SlideRendererWorkerWindow wnd = new SlideRendererWorkerWindow() { DataContext = Globals.MainWindowViewModel };
                    //wnd.Show();
                    //wnd.root.Children.Add(control);
                    //RenderTargetBitmap rtb = new RenderTargetBitmap(new PixelSize(1920, 1080));
                    //rtb.Render(control);
                    rtb.Save(@"R:\buffer.bmp");
                }
            //});
            return rtb;
        }
    }
}