using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;
using LibMpv.Client;
using System;
using System.Threading.Tasks;

namespace HandsLiftedApp.Views.Render
{
    public partial class MpvVideoSlideRenderer : UserControl
    {

        public MpvVideoSlideRenderer()
        {
            InitializeComponent();


            this.DataContextChanged += VideoSlide_DataContextChanged;
            this.AttachedToVisualTree += VideoSlideRenderer_AttachedToVisualTree;
            this.DetachedFromVisualTree += MpvVideoSlideRenderer_DetachedFromVisualTree;
        }

        private void MpvVideoSlideRenderer_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (VideoView.MpvContext != null)
            {
                Globals.GlobalMpvContextInstance.SetPropertyFlag("pause", true);
            }
        }

        private void VideoSlide_DataContextChanged(object? sender, EventArgs e)
        {
            //if (this.DataContext == null)
            //{
            //    MediaPlayer = null;
            //}
            //else
            //{
            //    MediaPlayer = ((VideoSlide<VideoSlideStateImpl>)this.DataContext).State.MediaPlayer;

            //    if (this.VisualRoot as Window is ProjectorWindow)
            //        VideoView.MediaPlayer = MediaPlayer;
            //}
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

                    // VideoView.MpvContext = Globals.GlobalMpvContextInstance;

                    Task.Run(() =>
                    {
                        Task.Delay(1000).Wait(); // a delay here fixes a noticeable "entire UI" lag when entering VideoSlid
                        Globals.GlobalMpvContextInstance.Command("loadfile", videoSlide.VideoPath, "replace");
                        Globals.GlobalMpvContextInstance.SetPropertyFlag("pause", false);

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
