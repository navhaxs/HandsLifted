using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace HandsLiftedApp.Data.Slides
{
    public class SongTitleSlide<T> : Slide where T : ISongTitleSlideState
    {

        public string Title { get; set; } = "SongSlide.SongSlideText default value";
        public string Copyright { get; set; } = "SongSlide.SongSlideText default value";

        public override string? SlideText => Title;

        public override string? SlideLabel => null;
    }
    public interface ISongTitleSlideState : ISlideState { }
}
