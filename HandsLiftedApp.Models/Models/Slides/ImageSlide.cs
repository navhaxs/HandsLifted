using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    public class ImageSlide<T> : Slide where T : IImageSlideState
    {
        T _state;
        public T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }

        public string ImagePath { get; set; }

        public ImageSlide(String imagePath = @"C:\VisionScreens\TestImages\SWEC App Announcement.png")
        {
            ImagePath = imagePath;
            State = (T)Activator.CreateInstance(typeof(T), this);
        }

        public override string? SlideText => null;

        public override string? SlideLabel => Path.GetFileName(ImagePath);

        public override async Task OnPreloadSlide()
        {
            // does not need to be async
            await base.OnEnterSlide();
            await State.OnSlideEnterEvent();
        }
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
    public interface IImageSlideState : ISlideState { }

}
