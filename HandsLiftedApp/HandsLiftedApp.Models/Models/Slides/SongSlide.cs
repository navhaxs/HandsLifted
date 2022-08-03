using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    public class SongSlide<T> : Slide where T : ISongSlideState
    {
        T _state;
        public T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }

        public SongSlide(SongStanza? ownerSongStanza)
        {
            State = (T)Activator.CreateInstance(typeof(T), this);

            OwnerSongStanza = ownerSongStanza;
        }

        public string Text { get; set; } = "";
        public string? Label { get; set; }

        // Slide interface accessors for rendering
        //public override string SlideLabel
        //{
        //    get
        //    {
        //        return "Verse 1";
        //    }
        //}

        public override string? SlideText => Text;

        public override string? SlideLabel => Label;

        // ref
        public SongStanza? OwnerSongStanza { get; } = null;
    }

    public interface ISongSlideState : ISlideState { }
}
