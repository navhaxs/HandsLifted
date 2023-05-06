using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.Utils;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using System;
using System.Threading.Tasks;

namespace HandsLiftedApp.Views.Render
{
    public partial class VideoSlideRenderer : UserControl
    {

        LibMpv.Client.MpvContext mpvContext;
        public VideoSlideRenderer()
        {
            InitializeComponent();

            this.DetachedFromLogicalTree += VideoSlide_DetachedFromLogicalTree;
            this.TemplateApplied += VideoSlide_TemplateApplied;

            if (Design.IsDesignMode)
                return;

            this.VideoView.MpvContext = mpvContext = new LibMpv.Client.MpvContext();
        }

        private VideoSlide<VideoSlideStateImpl> GetVideoSlide()
        {
            if (this.DataContext is VideoSlide<VideoSlideStateImpl>)
                return ((VideoSlide<VideoSlideStateImpl>)this.DataContext);

            return null;
        }

        private void VideoSlideRenderer_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            //if (this.VisualRoot as Window is ProjectorWindow)
            //{
            //    if (this.DataContext is VideoSlide<VideoSlideStateImpl>)
            //    {

            //        MediaPlayer = ((VideoSlide<VideoSlideStateImpl>)this.DataContext).State.MediaPlayer;

            //        if (this.VisualRoot as Window is ProjectorWindow)
            //            VideoView.MediaPlayer = MediaPlayer;
            //    }
            //}
            //else if (VideoView != null)
            //{
            //    VideoView.IsVisible = false;
            //}
        }

        private void VideoSlide_TemplateApplied(object? sender, Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
        {
            sAsync();
        }

        private void VideoSlide_DetachedFromLogicalTree(object? sender, Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
        {
            mpvContext?.StopRendering();
        }

        private async Task sAsync()
        {
            mpvContext.Load(GetVideoSlide().VideoPath);
            mpvContext.Play();

            //if (MediaPlayer == null || VideoView == null || VideoView.MediaPlayer == null) // } || MediaPlayer != null && MediaPlayer.Hwnd == IntPtr.Zero)
            //    return;

            //await Task.Run(() =>
            //    {
            //        // HACK waits for Video control to *fully* initialise first...
            //        Task.Delay(200).Wait(); // a delay here fixes a noticeable "entire UI" lag when entering VideoSlide
            //        Dispatcher.UIThread.InvokeAsync(() =>
            //        {
            //            if (MediaPlayer != null && !MediaPlayer.IsPlaying)
            //            {
            //                MediaPlayer.Play();

            //                VideoSlide<VideoSlideStateImpl> videoSlide = GetVideoSlide();
            //                if (videoSlide != null)
            //                {
            //                    MediaPlayer.Mute = videoSlide.IsMute;
            //                }
            //            }
            //        });
            //    });
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            //if (MediaPlayer != null && MediaPlayer.IsPlaying)
            //{
            //    MediaPlayer.Stop();
            //}
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            //if (MediaPlayer != null && !MediaPlayer.IsPlaying)
            //{
            //    MediaPlayer.Play();
            //}
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            //MediaPlayer?.Pause();
        }

        //public void OnLeaveSlide()
        //{
        //    MediaPlayer?.Stop();
        //}
    }
}
