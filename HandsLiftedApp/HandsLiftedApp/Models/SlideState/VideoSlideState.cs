using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using LibVLCSharp.Shared;
using ReactiveUI;
using System;

namespace HandsLiftedApp.Models.SlideState
{
    // state - create VLC when slide activated. destroy VLC after some timeout after slide deactive
    public class VideoSlideState : SlideStateBase
    {
        private LibVLC _libVLC;

        public VideoSlideState(VideoSlide data, int index) : base(data, index)
        {
            VideoPath = data.VideoPath;

            try
            {
                return;
                _libVLC = new LibVLC();
                MediaPlayer = new MediaPlayer(_libVLC);


                string absolute = new Uri(VideoPath).AbsoluteUri;
                bool isfile = absolute.StartsWith("file://");
                MediaPlayer.Media = new Media(_libVLC, VideoPath, isfile ? FromType.FromPath : FromType.FromLocation);
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

    }
}
