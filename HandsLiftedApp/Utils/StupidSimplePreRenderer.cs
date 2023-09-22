using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace HandsLiftedApp.Utils
{
    public static class StupidSimplePreRenderer
    {
        public static Bitmap Render(Control control)
        {
            using (SKBitmap bitmap = new SKBitmap(1920, 1080))
            {
                //using (SKCanvas canvas = new SKCanvas(bitmap))
                //{

                //    canvas.DrawRect(0, 0, 1920, 1080, new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.Black });


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

                //WriteableBitmap bitmap = new WriteableBitmap(new PixelSize(1920, 1080), new Vector(96.0, 96.0));

                //SlideRendererWorkerWindow wnd = new SlideRendererWorkerWindow() { DataContext = Globals.MainWindowViewModel };
                //wnd.Show();
                //wnd.root.Children.Add(control);
                RenderTargetBitmap rtb = new RenderTargetBitmap(new PixelSize(1920, 1080));
                rtb.Render(control);
                return rtb;
            }
        }
    }
