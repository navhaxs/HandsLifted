using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;

namespace HandsLiftedApp.Data.Slides
{
    public class SongSlide : Slide
    {
        public string Id { get; set; }

        public SongSlide(SongStanza? ownerSongStanza, string id)
        {
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
        public Bitmap? cached { get => _cached; set => this.RaiseAndSetIfChanged(ref _cached, value); }

        public override bool Equals(Object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                SongSlide p = (SongSlide)obj;
                //return (Text == p.Text) && (Label == p.Label);
                return (Id == p.Id);
            }
        }
    }

    public interface ISongSlideState : ISlideState
    {
    }
}
