using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.XTransitioningContentControl;
using LibVLCSharp.Shared;
using ReactiveUI;
using System;

namespace HandsLiftedApp.Models.SlideState
{
    // state - create VLC when slide activated. destroy VLC after some timeout after slide deactive
    public class VideoSlideState : SlideStateBase, ISlideRender
    {
        private LibVLC _libVLC;
        public Media _media { get; set; }

        public VideoSlideState(VideoSlide data, int index) : base(data, index)
        {
            VideoPath = data.VideoPath;

            try
            {
                _libVLC = new LibVLC();
                MediaPlayer = new MediaPlayer(_libVLC);


                string absolute = new Uri(VideoPath).AbsoluteUri;
                bool isfile = absolute.StartsWith("file://");
                _media = new Media(_libVLC, VideoPath, isfile ? FromType.FromPath : FromType.FromLocation);
                MediaPlayer.Media = _media;
            }
            catch
            {

            }
            //MediaPlayer.Play();
        }

        // TODO: thumbnail
        //private Bitmap? _image;

        //public Bitmap? Image
        //{
        //    get => _image;
        //    private set => this.RaiseAndSetIfChanged(ref _image, value);
        //}
        public string VideoPath { get; set; }
        public MediaPlayer MediaPlayer { get; }

        public void OnLeaveSlide()
        {
            MediaPlayer?.Stop();
        }
    }
}
