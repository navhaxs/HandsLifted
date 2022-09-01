﻿using HandsLiftedApp.XTransitioningContentControl;
using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    public abstract class Slide : ReactiveObject, ISlideRender
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

        public virtual void OnEnterSlide()
        {
        }

        public virtual void OnLeaveSlide()
        {
        }
    }

    public interface ISlideState
    {
        public virtual void OnSlideEnterEvent()
        {

        }
        public virtual void OnSlideLeaveEvent()
        {

        }
    }
}
