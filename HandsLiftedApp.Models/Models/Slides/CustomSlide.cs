using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Data.Models.Slides
{
    public class CustomSlide : Slide
    {
        public override string? SlideLabel => "abc";

        public override string? SlideText => "123";
    }
}
