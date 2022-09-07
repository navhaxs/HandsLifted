using HandsLiftedApp.Data.Slides;
using LibVLCSharp.Shared;
using System;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.SlideState
{
    // state - create VLC when slide activated. destroy VLC after some timeout after slide deactive
    public class VideoSlideStateImpl : SlideStateBase<VideoSlide<VideoSlideStateImpl>>, IVideoSlideState
    {
        private LibVLC _libVLC;
        public Media _media { get; set; }

        public VideoSlideStateImpl(ref VideoSlide<VideoSlideStateImpl> videoSlide) : base(ref videoSlide)
        {
            VideoPath = videoSlide.VideoPath;

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

        public async Task OnSlideEnterEvent()
        {

        }
        public async Task OnSlideLeaveEvent()
        {
            MediaPlayer?.Stop();
        }
    }
}
