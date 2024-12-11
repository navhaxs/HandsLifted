using Avalonia.Media.Imaging;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Models.RuntimeData
{
    public interface ISlideInstance
    {
        Bitmap? Cached { get; set; }
        Bitmap? Thumbnail { get; set; }
        ItemAutoAdvanceTimer? SlideTimerConfig { get; }
        SlideThumbnailBadge? SlideThumbnailBadge { get; }
    }
    
    public static class ISlideInstanceExtension
    {
        public static ISlideInstance? GetAsISlideInstance(this Slide t)
        {
            if (t is ISlideInstance itemSlide)
            {
                return itemSlide;
            }

            return null;
        }
    }
}