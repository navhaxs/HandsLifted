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

        // Slide interface accessors for rendering
        //public override string SlideLabel
        //{
        //    get
        //    {
        //        return "Verse 1";
        //    }
        //}

        public override string SlideText
        {
            get
            {
                return Title;
            }
        }
    }
    public interface ISongTitleSlideState : ISlideState { }
}
