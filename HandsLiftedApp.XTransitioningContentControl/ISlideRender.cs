using Avalonia.Animation;
namespace HandsLiftedApp.XTransitioningContentControl
{
    public interface ISlideRender
    {
        public void OnPreloadSlide();
        public void OnEnterSlide();
        public void OnLeaveSlide();

        public IPageTransition? PageTransition { get; set; }
    }
}
