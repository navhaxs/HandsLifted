using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.Models.RuntimeData
{
    public class LogoItemInstance : LogoItem, IItemInstance
    {
        private int _selectedSlideIndex;
        public int SelectedSlideIndex { get => _selectedSlideIndex; set => this.RaiseAndSetIfChanged(ref _selectedSlideIndex, value); }
        
        private Slide _activeSlide = new LogoSlide();
        public Slide ActiveSlide
        {
            get => _activeSlide;
            set => this.RaiseAndSetIfChanged(ref _activeSlide, value);
        }

    }
}