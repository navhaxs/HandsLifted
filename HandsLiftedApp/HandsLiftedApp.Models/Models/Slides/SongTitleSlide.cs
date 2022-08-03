namespace HandsLiftedApp.Data.Slides
{
    public class SongTitleSlide<T> : Slide where T : ISongTitleSlideState
    {

        public string Title { get; set; } = "";
        public string Copyright { get; set; } = "";

        public override string? SlideText => Title;

        public override string? SlideLabel => null;
    }
    public interface ISongTitleSlideState : ISlideState { }
}
