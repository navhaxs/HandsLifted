using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.XTransitioningContentControl;
using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    public class SongSlide<T> : Slide, ISlideBitmapCacheable where T : ISongSlideState
    {
        T _state;
        public T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }
        public string Id { get; set; }

        public SongSlide(SongStanza? ownerSongStanza, string id)
        {
            State = (T)Activator.CreateInstance(typeof(T), this);

            OwnerSongStanza = ownerSongStanza;
            Id = id;
        }

        private string _text = "";
        public string Text
        {
            get => _text;
            set
            {
                this.RaiseAndSetIfChanged(ref _text, value);
                //cached = null;
                //this.RaisePropertyChanged(nameof(SlideText));
            }
        }

        private string _label = "";
        public string Label
        {
            get => _label;
            set
            {
                this.RaiseAndSetIfChanged(ref _label, value);
                //this.RaisePropertyChanged(nameof(SlideLabel));
            }
        }

        public override string? SlideText => Text;

        public override string? SlideLabel => Label;

        // ref
        public SongStanza? OwnerSongStanza { get; } = null;

        Bitmap _cached;
        public Bitmap? cached { get => _cached; set => _cached = value; }

        public override bool Equals(Object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                SongSlide<T> p = (SongSlide<T>)obj;
                //return (Text == p.Text) && (Label == p.Label);
                return (Id == p.Id);
            }
        }
    }

    public interface ISongSlideState : ISlideState {
    }
}
