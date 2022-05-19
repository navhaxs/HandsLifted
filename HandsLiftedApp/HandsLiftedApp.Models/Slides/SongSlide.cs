using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace HandsLiftedApp.Data.Slides
{
    public class SongSlide : Slide
    {
        public string Text { get; set; } = "SongSlide.SongSlideText default value";

        // Slide interface accessors for rendering
        public override string SlideLabel
        {
            get
            {
                return "Verse 1";
            }
        }

        public override string SlideText
        {
            get
            {
                return Text;
            }
        }

        public override string SlideNumber
        {
            get
            {
                return "1";
            }
        }
    }
}
