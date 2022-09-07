using Avalonia.Animation;

namespace HandsLiftedApp.XTransitioningContentControl
{
    public interface ISlideRender
    {
        public Task OnPreloadSlide();
        public Task OnEnterSlide();
        public Task OnLeaveSlide();

        public IPageTransition? PageTransition { get; set; }
    }
}
