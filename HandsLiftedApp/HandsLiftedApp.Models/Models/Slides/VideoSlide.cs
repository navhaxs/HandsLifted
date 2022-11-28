using HandsLiftedApp.XTransitioningContentControl;
using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    public class VideoSlide<T> : Slide, IDynamicSlideRender where T : IVideoSlideState
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

        public override async Task OnEnterSlide()
        {
            await base.OnEnterSlide();
            await State.OnSlideEnterEvent();
        }

        public override async Task OnLeaveSlide()
        {
            await base.OnLeaveSlide();
            await State.OnSlideLeaveEvent();
        }
    }

    public interface IVideoSlideState : ISlideState
    {
    }

}
