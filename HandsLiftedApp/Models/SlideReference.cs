using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Models
{
    public class SlideReference
    {
        public int? SlideIndex { get; set; }
        public Slide? Slide { get; set; }

        public int ItemIndex { get; set; }
    }
}
