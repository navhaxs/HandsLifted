using Avalonia.Animation;
using ReactiveUI;
using System;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Slides
{
    public abstract class Slide : ReactiveObject
    {

        //private int _index;
        //[XmlIgnore]
        //public int Index // does this make sense here? or should all slides have a Slides[<wrapper: index+slide>]
        //{
        //    get => _index; set
        //    {
        //        this.RaiseAndSetIfChanged(ref _index, value);
        //        this.RaisePropertyChanged(nameof(SlideNumber));
        //    }
        //}

        //public int SlideNumber { get => Index + 1; }

        // meta - group labels, slide number, etc.
        public Guid ParentItemUUID { get; set; }
        public abstract string? SlideLabel { get; }
        public abstract string? SlideText { get; }

        // slide-override: page transition
        private IPageTransition? _pageTransition;
        [XmlIgnore]
        public IPageTransition? PageTransition { get => _pageTransition; set => this.RaiseAndSetIfChanged(ref _pageTransition, value); }

        public virtual void OnEnterSlide()
        {
        }

        public virtual void OnLeaveSlide()
        {
        }

        public virtual void OnPreloadSlide()
        {
        }
    }

    public interface ISlideState
    {
        public virtual void OnSlideEnterEvent() { }
        public virtual void OnSlideLeaveEvent() { }
    }
}
