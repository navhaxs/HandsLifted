using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views;

namespace HandsLiftedApp.Models.SlideState
{
    public class SongSlideStateImpl : SlideStateBase<SongSlide<SongSlideStateImpl>>, ISongSlideState
    {
        readonly SongSlide<SongSlideStateImpl> songSlide;

        public SongSlideStateImpl(ref SongSlide<SongSlideStateImpl> songSlide) : base(ref songSlide)
        {
            songSlide.cached = StupidSimplePreRenderer.Render(new DesignerSlideTemplate() { DataContext = songSlide });
        }

    }
}
