using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace HandsLiftedApp.Models.SlideState
{
    public class SongTitleSlideStateImpl : SlideStateBase<SongTitleSlide<SongTitleSlideStateImpl>>, ISongTitleSlideState
    {
        SongTitleSlide<SongTitleSlideStateImpl> _songTitleSlide;

        public SongTitleSlideStateImpl(ref SongTitleSlide<SongTitleSlideStateImpl> songTitleSlide) : base(ref songTitleSlide)
        {
            this._songTitleSlide = songTitleSlide;
            //songTitleSlide.cached = StupidSimplePreRenderer.Render(new DesignerSlideTitle() { DataContext = songTitleSlide });

            //this.songSlide = songSlide;
      //      songTitleSlide.WhenAnyValue(s => s.Title)
      ////.ObserveOn(RxApp.MainThreadScheduler)
      //.Throttle(TimeSpan.FromMilliseconds(1000), RxApp.TaskpoolScheduler)
      //.Subscribe(text =>
      {
          this._songTitleSlide.cached = StupidSimplePreRenderer.Render(new DesignerSlideTitle() { DataContext = _songTitleSlide });
          //return;
      }//);
        }
    }
}
