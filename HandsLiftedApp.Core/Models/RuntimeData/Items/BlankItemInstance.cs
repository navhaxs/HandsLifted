using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class BlankItemInstance : IItemInstance
    {
        public int SelectedSlideIndex { get; set; }
        public Slide ActiveSlide { get => new BlankSlide(); }
    }
}