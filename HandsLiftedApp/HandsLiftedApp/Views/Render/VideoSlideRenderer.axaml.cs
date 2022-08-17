using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HandsLiftedApp.Controls;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using System;
using System.Threading.Tasks;

namespace HandsLiftedApp.Views.Render
{
    public partial class VideoSlideRenderer : UserControl, ISlideRender
    {

        private VideoView VideoView;
        private MediaPlayer? MediaPlayer;

        public VideoSlideRenderer()
        {
            InitializeComponent();

            this.DetachedFromLogicalTree += VideoSlide_DetachedFromLogicalTree;
            this.TemplateApplied += VideoSlide_TemplateApplied;


            if (Design.IsDesignMode)
                return;



            VideoView = this.Get<VideoView>("VideoView");
            //VideoView.VlcRenderingOptions = LibVLCAvaloniaRenderingOptions.Avalonia;

            //_libVLC = new LibVLC();
            //_mediaPlayer = new MediaPlayer(_libVLC);

            //#if DEBUG
            //            this.AttachDevTools();
            //#endif

            //var VideoPath = @"C:\VisionScreens\TestImages\WA22 Speaker Interview.mp4";

            //string absolute = new Uri(VideoPath).AbsoluteUri;
            //bool isfile = absolute.StartsWith("file://");
            //_mediaPlayer.Media = new Media(_libVLC, VideoPath, isfile ? FromType.FromPath : FromType.FromLocation);
            ////
            this.DataContextChanged += VideoSlide_DataContextChanged;
            this.AttachedToVisualTree += VideoSlideRenderer_AttachedToVisualTree;


        }

        private void VideoSlideRenderer_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (this.VisualRoot as Window is ProjectorWindow)
            {
                if (this.DataContext is VideoSlide<VideoSlideStateImpl>)
                {

                    MediaPlayer = ((VideoSlide<VideoSlideStateImpl>)this.DataContext).State.MediaPlayer;

                    if (this.VisualRoot as Window is ProjectorWindow)
                        VideoView.MediaPlayer = MediaPlayer;
                }
            }
            else
            {
                VideoView.IsVisible = false;
            }
        }

        private void VideoSlide_DataContextChanged(object? sender, EventArgs e)
        {
            if (this.DataContext == null)
            {
                MediaPlayer = null;
            }
            else
            {
                MediaPlayer = ((VideoSlide<VideoSlideStateImpl>)this.DataContext).State.MediaPlayer;

                if (this.VisualRoot as Window is ProjectorWindow)
                    VideoView.MediaPlayer = MediaPlayer;
            }
        }

        private void VideoSlide_TemplateApplied(object? sender, Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
        {
            sAsync();
        }

        private void VideoSlide_DetachedFromLogicalTree(object? sender, Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
        {
            MediaPlayer?.Stop();
        }

        private async Task sAsync()
        {

            if (MediaPlayer == null) // } || MediaPlayer != null && MediaPlayer.Hwnd == IntPtr.Zero)
                return;

            await Task.Run(() =>
                {
                    // HACK waits for Video control to *fully* initialise first...
                    Task.Delay(200).Wait(); // a delay here fixes a noticeable "entire UI" lag when entering VideoSlide
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (MediaPlayer != null && !MediaPlayer.IsPlaying)
                        {
                            MediaPlayer.Play();
                        }
                    });
                });
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }


        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer != null && MediaPlayer.IsPlaying)
            {
                MediaPlayer.Stop();
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer != null && !MediaPlayer.IsPlaying)
            {
                MediaPlayer.Play();
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer?.Pause();
        }

        public void OnLeaveSlide()
        {
            MediaPlayer?.Stop();
        }
    }
}
