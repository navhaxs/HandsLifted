using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    public class VideoSlide<T> : Slide where T : IVideoSlideState
    {
        T _state;
        public T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }

        public string VideoPath { get; set; }

        public VideoSlide(String videoPath = @"C:\VisionScreens\TestImages\WA22 Speaker Interview.mp4")
        {
            VideoPath = videoPath;
            State = (T)Activator.CreateInstance(typeof(T), this);
        }

        public override string? SlideText => null;

        public override string? SlideLabel => Path.GetFileName(VideoPath);
    }

    public interface IVideoSlideState : ISlideState { }

}
