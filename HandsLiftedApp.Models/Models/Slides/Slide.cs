using Avalonia.Animation;
using Avalonia.Media.Imaging;
using HandsLiftedApp.XTransitioningContentControl;
using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    public abstract class Slide : ReactiveObject 
    {

        private int _index;
        public int Index
        {
            get => _index; set
            {
                this.RaiseAndSetIfChanged(ref _index, value);
                this.RaisePropertyChanged(nameof(SlideNumber));
            }
        }

        public int SlideNumber { get => Index + 1; }

        // meta - group labels, slide number, etc.
        public abstract string? SlideLabel { get; }
        public abstract string? SlideText { get; }

        // slide-override: page transition
        private IPageTransition? _pageTransition;
        public IPageTransition? PageTransition { get => _pageTransition; set => this.RaiseAndSetIfChanged(ref _pageTransition, value); }

        public virtual async Task OnEnterSlide()
        {
        }

        public virtual async Task OnLeaveSlide()
        {
        }

        public virtual async Task OnPreloadSlide()
        {
        }
    }

    public interface ISlideState
    {
        public virtual async Task OnSlideEnterEvent()
        {

        }
        public virtual async Task OnSlideLeaveEvent()
        {

        }
    }
}
