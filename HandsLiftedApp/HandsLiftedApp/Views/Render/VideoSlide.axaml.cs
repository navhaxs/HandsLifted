using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HandsLiftedApp.Controls;
using HandsLiftedApp.Models.SlideState;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using System;
using System.Threading.Tasks;

namespace HandsLiftedApp.Views.Render
{
    public partial class VideoSlide : UserControl, ISlideRender
    {

        private VideoView VideoView;
        //private LibVLC _libVLC;
        //private MediaPlayer _mediaPlayer;

        public VideoSlide()
        {
            InitializeComponent();

            this.DetachedFromLogicalTree += VideoSlide_DetachedFromLogicalTree;
            this.TemplateApplied += VideoSlide_TemplateApplied;


            if (Design.IsDesignMode)
                return;


            VideoView = this.Get<VideoView>("VideoView");

            //_libVLC = new LibVLC();
            //_mediaPlayer = new MediaPlayer(_libVLC);

            VideoView.VlcRenderingOptions = LibVLCAvaloniaRenderingOptions.Avalonia;

            //#if DEBUG
            //            this.AttachDevTools();
            //#endif

            //var VideoPath = @"C:\VisionScreens\TestImages\WA22 Speaker Interview.mp4";

            //string absolute = new Uri(VideoPath).AbsoluteUri;
            //bool isfile = absolute.StartsWith("file://");
            //_mediaPlayer.Media = new Media(_libVLC, VideoPath, isfile ? FromType.FromPath : FromType.FromLocation);
            ////
            this.DataContextChanged += VideoSlide_DataContextChanged;
        }

        private void VideoSlide_DataContextChanged(object? sender, EventArgs e)
        {

            if (this.DataContext is VideoSlideState)
                VideoView.MediaPlayer = ((VideoSlideState)this.DataContext).MediaPlayer; ;
        }

        private void VideoSlide_TemplateApplied(object? sender, Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
        {
            sAsync();
        }

        private void VideoSlide_DetachedFromLogicalTree(object? sender, Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
        {
            VideoView.MediaPlayer.Stop();
        }

        private async Task sAsync()
        {
            await Task.Run(() => {
                Task.Delay(100).Wait(); // a delay here fixes a noticeable "entire UI" lag when entering VideoSlide
                Dispatcher.UIThread.InvokeAsync(() => {
                    if (!VideoView.MediaPlayer.IsPlaying)
                    {
                        if (this.DataContext == null)
                        {
                            return;
                        }
                        //VideoView.MediaPlayer.Play(new Media(_libVLC,
                        //    "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4", FromType.FromLocation));
                         ((VideoSlideState)this.DataContext).MediaPlayer.Play();
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
            if (VideoView.MediaPlayer.IsPlaying)
            {
                VideoView.MediaPlayer.Stop();
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!VideoView.MediaPlayer.IsPlaying)
            {
                ((VideoSlideState)this.DataContext).MediaPlayer.Play();
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            VideoView.MediaPlayer.Pause();
        }

        public void OnLeaveSlide()
        {
        }
    }
}
