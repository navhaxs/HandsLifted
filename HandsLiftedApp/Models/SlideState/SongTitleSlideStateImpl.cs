using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.Events;
using HandsLiftedApp.Views;
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
            MessageBus.Current.Listen<InvalidateSlideBitmapMessage>()
               .Subscribe(x =>
               {
                   MessageBus.Current.SendMessage(new SlideRenderRequestMessage()
                   {
                       Data = this._songTitleSlide,
                       Callback = (bitmap) =>
                       {
                           this._songTitleSlide.cached = bitmap;
                       }
                   });
               });
        }
    }
}
