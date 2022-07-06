using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HandsLiftedApp.Data.Slides
{
    public class SongSlide<T> : Slide where T : ISongSlideState
    {
        T _state;
        public T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }

        public SongSlide(SongStanza ownerSongStanza)
        {
            State = (T)Activator.CreateInstance(typeof(T), this);

            OwnerSongStanza = ownerSongStanza;
        }

        public string Text { get; set; } = "SongSlide.SongSlideText default value";

        // Slide interface accessors for rendering
        //public override string SlideLabel
        //{
        //    get
        //    {
        //        return "Verse 1";
        //    }
        //}

        public override string SlideText
        {
            get
            {
                return Text;
            }
        }

        // ref
        public SongStanza OwnerSongStanza { get; }
    }

    public interface ISongSlideState : ISlideState { }
}
