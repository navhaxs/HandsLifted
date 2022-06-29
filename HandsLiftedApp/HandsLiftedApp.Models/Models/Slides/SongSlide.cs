using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static HandsLiftedApp.Data.Models.Items.SongItem;

namespace HandsLiftedApp.Data.Slides
{
    public class SongSlide : Slide
    {
        public SongSlide(SongStanza ownerSongStanza)
        {
            OwnerSongStanza = ownerSongStanza;
        }

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

        // ref
        public SongStanza OwnerSongStanza { get; }
    }
}
