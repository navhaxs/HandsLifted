﻿namespace HandsLiftedApp.Data.Slides
{
    public abstract class Slide
    {
        //// rendered slide graphics
        //public abstract UIElement RenderSlide { get; }

        //// rendered slide thumbnail
        //public abstract UIElement RenderThumbnail { get; }

        // meta - group labels, slide number, etc.
        public abstract string SlideLabel { get; }

        public abstract string SlideText { get; }

        // slide stage display message
        public abstract string SlideNumber { get; }
    }
}