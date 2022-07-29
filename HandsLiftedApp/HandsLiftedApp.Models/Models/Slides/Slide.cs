using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    public abstract class Slide : ReactiveObject
    {
        //// rendered slide graphics
        //public abstract UIElement RenderSlide { get; }

        //// rendered slide thumbnail
        //public abstract UIElement RenderThumbnail { get; }

        // meta - group labels, slide number, etc.
        public abstract string? SlideLabel { get; }

        public abstract string? SlideText { get; }
    }

    public interface ISlideState
    {
        //public abstract int SlideNumber { get; set; }
    }
}
