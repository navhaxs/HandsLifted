using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Models.SlideState
{
    public class SongTitleSlideStateImpl : SlideStateBase<SongTitleSlide<SongTitleSlideStateImpl>>, ISongTitleSlideState
    {
        public SongTitleSlideStateImpl(ref SongTitleSlide<SongTitleSlideStateImpl> songTitleSlide) : base(ref songTitleSlide) { }
    }
}
