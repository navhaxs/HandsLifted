using System;
using HandsLiftedApp.XTransitioningContentControl;

namespace HandsLiftedApp.Data.Slides
{
    public class LogoSlide : Slide, ISlideRender
    {
        // TODO: could have multiple "Event holding / title / section graphics" user-assignable!?
        public LogoSlide() : base()
        {
            PageTransition = new XFade(TimeSpan.FromMilliseconds(300));
        }

        public override string? SlideText => null;

        public override string? SlideLabel => null;
        
        public override bool Equals(object? obj)
        {
            return obj is LogoSlide slide;
        }
    }
}
