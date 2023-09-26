using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;
using Serilog;
using System;
using System.Threading.Tasks;

namespace HandsLiftedApp.Views.Render
{
    public partial class MpvVideoSlideRenderer : UserControl
    {

        bool _isMounted = false;

        public MpvVideoSlideRenderer()
        {
            InitializeComponent();

            Log.Debug("MpvVideoSlideRenderer Init");

            this.DataContextChanged += VideoSlide_DataContextChanged;
            this.AttachedToVisualTree += VideoSlideRenderer_AttachedToVisualTree;
            this.DetachedFromVisualTree += MpvVideoSlideRenderer_DetachedFromVisualTree;
        }

        private void OnSlideDestroy(object sender, VideoSlideStateImpl.SlideLeaveEventArgs e)
        {
            VideoView.MpvContext = null;
            Log.Debug("MpvVideoSlideRenderer internally detached");
        }

        private void MpvVideoSlideRenderer_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_isMounted && VideoView.MpvContext != null)
            {
                //Globals.GlobalMpvContextInstance.SetPropertyFlag("pause", true);
            }
            VideoView.MpvContext = null;
            _isMounted = false;
            Log.Debug("MpvVideoSlideRenderer detached");
        }

        private void VideoSlide_DataContextChanged(object? sender, EventArgs e)
        {
            if (this.DataContext is VideoSlide<VideoSlideStateImpl> v)
            {
                v.State.OnSlideLeave += OnSlideDestroy;
            }
        }

        private void VideoSlide_TemplateApplied(object? sender, Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
        {
            //sAsync();
        }

        private void VideoSlideRenderer_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (this.VisualRoot as Window is ProjectorWindow)
            {
                if (this.DataContext is VideoSlide<VideoSlideStateImpl> videoSlide)
                {
                    Log.Debug("MpvVideoSlideRenderer Attached");
                    _isMounted = true;
                    VideoView.MpvContext = Globals.GlobalMpvContextInstance;

                    Task.Run(() =>
                    {
                        Task.Delay(1000).Wait(); // a delay here fixes a noticeable "entire UI" lag when entering VideoSlid

                        if (_isMounted)
                        {
                            // only run if slide still active
                            Globals.GlobalMpvContextInstance.Command("loadfile", videoSlide.SourceMediaPath, "replace");
                            Globals.GlobalMpvContextInstance.SetPropertyFlag("pause", false);
                            Log.Debug("MpvVideoSlideRenderer loadfile and play");
                        }

                    });
                }
            }
            else if (VideoView != null)
            {
                VideoView.IsVisible = false;
            }



        }

    }
}
