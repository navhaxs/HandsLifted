﻿using HandsLiftedApp.Data.Slides;
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

            // TODO: get parent playlist -> list of designs
            // TODO: get parent item -> selected design
            //MessageBus.Current.SendMessage(new SlideRenderRequestMessage()
            //{
            //    Data = this._songTitleSlide,
            //    Callback = (bitmap) =>
            //    {
            //        this._songTitleSlide.cached = bitmap;
            //    }
            //});

            {
                // TODO: fixme
                // once this is fixed                     // TODO do not keep re-creating the slide object, rather just update it
                songTitleSlide.WhenAnyValue(s => s.Title, s => s.Copyright, s => s.SlideText) // todo dirty bit?
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Throttle(TimeSpan.FromMilliseconds(200), RxApp.TaskpoolScheduler)
                    .Subscribe(text =>
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
