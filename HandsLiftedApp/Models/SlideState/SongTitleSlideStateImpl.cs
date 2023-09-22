using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views;

namespace HandsLiftedApp.Models.SlideState
{
    public class SongTitleSlideStateImpl : SlideStateBase<SongTitleSlide<SongTitleSlideStateImpl>>, ISongTitleSlideState
    {
        public SongTitleSlideStateImpl(ref SongTitleSlide<SongTitleSlideStateImpl> songTitleSlide) : base(ref songTitleSlide)
        {
            songTitleSlide.cached = StupidSimplePreRenderer.Render(new DesignerSlideTitle() { DataContext = songTitleSlide });
        }
    }
}
