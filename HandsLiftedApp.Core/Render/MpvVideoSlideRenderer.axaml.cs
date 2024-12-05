using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using HandsLiftedApp.Core.Views;
using Serilog;

namespace HandsLiftedApp.Core.Render
{
    public partial class MpvVideoSlideRenderer : UserControl
    {
        bool _isMounted = false;

        public MpvVideoSlideRenderer()
        {
            InitializeComponent();

            this.AttachedToVisualTree += VideoSlideRenderer_AttachedToVisualTree;
            this.DetachedFromVisualTree += MpvVideoSlideRenderer_DetachedFromVisualTree;
        }

        // private void OnSlideDestroy(object sender, VideoSlideStateImpl.SlideLeaveEventArgs e)
        // {
        //     VideoView.MpvContext = null;
        //     Log.Debug("MpvVideoSlideRenderer internally detached");
        // }

        private void MpvVideoSlideRenderer_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_isMounted && VideoView.MpvContext != null)
            {
                //Globals.GlobalMpvContextInstance.SetPropertyFlag("pause", true);
            }

            VideoView.MpvContext = null;
            _isMounted = false;
        }

        // private void VideoSlide_DataContextChanged(object? sender, EventArgs e)
        // {
        //     if (this.DataContext is VideoSlideInstance v)
        //     {
        //         v.OnLeaveSlide += OnSlideDestroy;
        //     }
        // }

        private void VideoSlide_TemplateApplied(object? sender, Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
        {
            //sAsync();
        }

        private void VideoSlideRenderer_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (this.VisualRoot as Window is ProjectorWindow)
            {
                NonProjectorWindowText.IsVisible = false;
                if (this.DataContext is VideoSlideInstance videoSlide)
                {
                    Log.Debug("MpvVideoSlideRenderer Attached in ProjectorWindow");
                    _isMounted = true;
                    VideoView.MpvContext = Globals.Instance.MpvContextInstance;

                    Task.Run(() =>
                    {
                        //Task.Delay(1000).Wait(); // a delay here fixes a noticeable "entire UI" lag when entering VideoSlid

                        if (_isMounted)
                        {
                            // only run if slide still active
                            if (Globals.Instance.MpvContextInstance == null)
                            {
                                Log.Debug("MpvVideoSlideRenderer failed: MpvContextInstance not initialized");
                                return;
                            }
                            Globals.Instance.MpvContextInstance.Command("loadfile", videoSlide.SourceMediaFilePath, "replace");
                            Globals.Instance.MpvContextInstance.SetPropertyFlag("pause", false);
                            Log.Debug("MpvVideoSlideRenderer loadfile and play");
                        }
                    });
                }
            }
            else if (VideoView != null)
            {
                VideoView.IsVisible = false;
                NonProjectorWindowText.IsVisible = true;
            }
        }
    }
}