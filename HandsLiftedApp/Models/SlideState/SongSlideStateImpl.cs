using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Views;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace HandsLiftedApp.Models.SlideState
{
    public class SongSlideStateImpl : SlideStateBase<SongSlide<SongSlideStateImpl>>, ISongSlideState
    {
        readonly SongSlide<SongSlideStateImpl> songSlide;

        public SongSlideStateImpl(ref SongSlide<SongSlideStateImpl> songSlide) : base(ref songSlide)
        {
            this.songSlide = songSlide;

            songSlide.WhenAnyValue(s => s.Text) // todo dirty bit?
            .ObserveOn(RxApp.MainThreadScheduler)
            .Throttle(TimeSpan.FromMilliseconds(200), RxApp.TaskpoolScheduler)
            .Subscribe(text =>
                  {
                      MessageBus.Current.SendMessage(new SlideRenderRequestMessage()
                      {
                          Data = this.songSlide,
                          Callback = (bitmap) =>
                          {
                              this.songSlide.cached = bitmap;
                          }
                      });
                  });


        }

    }
}
