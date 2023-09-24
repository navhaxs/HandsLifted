using Avalonia.Media.Imaging;

namespace HandsLiftedApp.XTransitioningContentControl
{
    public interface ISlideBitmapCacheable
    {
        Bitmap? cached { get; set; }
        public virtual Bitmap? GetBitmap()
        {
            return cached;
        }
        public virtual void SetBitmap(Bitmap bitmap)
        {
            cached = bitmap;
        }
        public virtual void InvalidateBitmap()
        {
            //cached = null;
        }
    }
}
