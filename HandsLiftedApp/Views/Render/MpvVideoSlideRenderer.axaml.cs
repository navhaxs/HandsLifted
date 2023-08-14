using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;
using LibMpv.Client;
using LibVLCSharp.Avalonia;
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

                    VideoView.MpvContext = Globals.GlobalMpvContextInstance;

                    //MediaPlayer = ((VideoSlide<VideoSlideStateImpl>)this.DataContext).State.MediaPlayer;

                    Task.Run(() =>
                    {
                        Task.Delay(1000).Wait(); // a delay here fixes a noticeable "entire UI" lag when entering VideoSlid
                        Globals.GlobalMpvContextInstance.Command("loadfile", videoSlide.VideoPath, "replace");
                        Globals.GlobalMpvContextInstance.SetPropertyFlag("pause", false);

                    });
                        //videoSlide
                        //MediaPlayer = ((VideoSlide<VideoSlideStateImpl>)this.DataContext).State.MediaPlayer;

                    //if (this.VisualRoot as Window is ProjectorWindow)
                    //{
                    //    VideoView.MediaPlayer = MediaPlayer;
                    //    VideoView.VlcRenderingOptions = LibVLCAvaloniaRenderingOptions.Avalonia;
                    //}
                }
            }
            else if (VideoView != null)
            {
                VideoView.IsVisible = false;
            }


        
        }

    }
}
