using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views;

namespace HandsLiftedApp.Models.SlideState
{
    public class SongSlideStateImpl : SlideStateBase<SongSlide<SongSlideStateImpl>>, ISongSlideState
    {
        public SongSlideStateImpl(ref SongSlide<SongSlideStateImpl> songSlide) : base(ref songSlide) { }
    }
}
