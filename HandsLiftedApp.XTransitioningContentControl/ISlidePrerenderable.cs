using Avalonia.Media.Imaging;

namespace HandsLiftedApp.XTransitioningContentControl
{
    public interface ISlidePrerenderable
    {
        public Bitmap? TryGetCachedBitmap();
    }
}
