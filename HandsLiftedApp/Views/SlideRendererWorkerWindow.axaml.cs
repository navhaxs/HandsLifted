using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;
using ReactiveUI;
using System;
using HandsLiftedApp.Utils;

namespace HandsLiftedApp.Views
{
    public partial class SlideRendererWorkerWindow : Window
    {

        public SlideRendererWorkerWindow()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
                return;


            //Performing some magic to hide the form from Alt+Tab
            IntPtr? handle = this.TryGetPlatformHandle()?.Handle;
            if (handle != null)
            {
                Win32.SetWindowLong((IntPtr)handle, Win32.GWL_EX_STYLE, (Win32.GetWindowLong((IntPtr)handle, Win32.GWL_EX_STYLE) | Win32.WS_EX_TOOLWINDOW) & ~Win32.WS_EX_APPWINDOW);
            }

            this.Opened += SlideRendererWorkerWindow_Opened;

            MessageBus.Current.Listen<SlideRenderRequestMessage>()
                .Subscribe((request) =>
                {
                    //Bitmap rtb;
                    //using (SKBitmap bitmap = new SKBitmap(1920, 1080))
                    //{
                    //    using (SKCanvas canvas = new SKCanvas(bitmap))
                    //    {
                    //        canvas.DrawRect(0, 0, 1920, 1080, new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.Red });
                    //    }

                    //    var image = SKImage.FromBitmap(bitmap);
                    //    SKData encoded = image.Encode();
                    //    Stream stream = encoded.AsStream();
                    //    rtb = new Bitmap(stream);
                    //    //rtb.Save(@"R:\buffer.bmp");
                    //}

                    //request.Callback(rtb);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        RenderTargetBitmap rtb = new RenderTargetBitmap(new PixelSize(1920, 1080));

                        Control? templateControl = null;

                        if (typeof(SongSlide<SongSlideStateImpl>) == request.Data.GetType())
                        {
                            templateControl = new DesignerSlideTemplate() { DataContext = request.Data };
                        }
                        else if (typeof(SongTitleSlide<SongTitleSlideStateImpl>) == request.Data.GetType())
                        {
                            templateControl = new DesignerSlideTitle() { DataContext = request.Data };
                        }

                        if (templateControl != null)
                        {
                            root.Children.Add(templateControl);
                            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
                            rtb.Render(templateControl);
                            //rtb.Save(@"R:\buffer.bmp");
                        }
                        request.Callback(rtb);
                        root.Children.Clear();
                    });

                });
        }

        private void SlideRendererWorkerWindow_Opened(object? sender, EventArgs e)
        {
            this.Opacity = 0;
            this.IsHitTestVisible = false;
            this.SystemDecorations = SystemDecorations.None;
            this.Height = 0;
            this.Width = 0;
        }
    }

    public class SlideRenderRequestMessage
    {
        public Slide Data { get; set; }
        public Action<Bitmap> Callback { get; set; }
    }
}
