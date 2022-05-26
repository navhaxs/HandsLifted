using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using LibVLCSharp.Shared;
using ReactiveUI;
using System;

namespace HandsLiftedApp.Models.Render
{
    public class VideoSlideState : SlideState
    {
        private LibVLC _libVLC;

        public VideoSlideState(VideoSlide data) : base(data)
        {
            _libVLC = new LibVLC();
            MediaPlayer = new MediaPlayer(_libVLC);

            VideoPath = data.VideoPath;

            string absolute = new Uri(VideoPath).AbsoluteUri;
            bool isfile = absolute.StartsWith("file://");
            MediaPlayer.Media = new Media(_libVLC, VideoPath, isfile ? FromType.FromPath : FromType.FromLocation);
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

    }
}
