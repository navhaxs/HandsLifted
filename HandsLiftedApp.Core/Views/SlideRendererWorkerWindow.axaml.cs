using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.Views.Designer;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Views
{
    public partial class SlideRendererWorkerWindow : Window, INotifyPropertyChanged
    {
        public SlideRendererWorkerWindow()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
                return;

            //Performing some magic to hide the form from Alt+Tab
            if (OperatingSystem.IsWindows())
            {
                IntPtr? handle = this.TryGetPlatformHandle()?.Handle;
                if (handle != null)
                {
                    Win32.SetWindowLong((IntPtr)handle, Win32.GWL_EX_STYLE,
                        (Win32.GetWindowLong((IntPtr)handle, Win32.GWL_EX_STYLE) | Win32.WS_EX_TOOLWINDOW) &
                        ~Win32.WS_EX_APPWINDOW);
                }
            }

            this.Opened += SlideRendererWorkerWindow_Opened;

            MessageBus.Current.Listen<SlideRenderRequestMessage>()
                .Subscribe((request) =>
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        RenderTargetBitmap rtb = new RenderTargetBitmap(new PixelSize(1920, 1080));
                    
                        Control? templateControl = null;
                    
                        if (typeof(SongSlideInstance) == request.Data.GetType())
                        {
                            templateControl = new SongSlideView() { DataContext = request.Data };
                        }
                        else if (typeof(SongTitleSlideInstance) == request.Data.GetType())
                        {
                            templateControl = new DesignerSlideTitle() { DataContext = request.Data };
                        }
                        else
                        {
                            Log.Error("Unknown slide type: " + request.Data.GetType());
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

    public record SlideRenderRequestMessage(Slide Data, Action<Bitmap> Callback);
}