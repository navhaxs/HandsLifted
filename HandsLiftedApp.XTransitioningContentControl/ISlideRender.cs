using Avalonia.Animation;
using Avalonia.Media.Imaging;

namespace HandsLiftedApp.XTransitioningContentControl
{
    public interface ISlideRender
    {
        public Task OnPreloadSlide();
        public Task OnEnterSlide();
        public Task OnLeaveSlide();

        // if cached or prerendered
        public Bitmap TryGetBitmap();

        public IPageTransition? PageTransition { get; set; }
    }
}
