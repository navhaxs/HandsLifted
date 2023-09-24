using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace HandsLiftedApp.Models.SlideState
{
    public class SongSlideStateImpl : SlideStateBase<SongSlide<SongSlideStateImpl>>, ISongSlideState
    {
        readonly SongSlide<SongSlideStateImpl> songSlide;

        public SongSlideStateImpl(ref SongSlide<SongSlideStateImpl> songSlide) : base(ref songSlide)
        {
           this.songSlide = songSlide;
      //      songSlide.WhenAnyValue(s => s.Text)
      ////.ObserveOn(RxApp.MainThreadScheduler)
      //.Throttle(TimeSpan.FromMilliseconds(1000), RxApp.TaskpoolScheduler)
      //.Subscribe(text =>
      //      {
      //          this.songSlide.cached = StupidSimplePreRenderer.Render(new DesignerSlideTemplate() { DataContext = this.songSlide });
      //          //return;
      //      });
        }

    }
}
