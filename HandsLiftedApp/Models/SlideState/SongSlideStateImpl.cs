using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Models.SlideState {
    public class SongSlideStateImpl : SlideStateBase<SongSlide<SongSlideStateImpl>>, ISongSlideState
    {
        public SongSlideStateImpl(ref SongSlide<SongSlideStateImpl> songSlide) : base(ref songSlide) { }
    }
}
