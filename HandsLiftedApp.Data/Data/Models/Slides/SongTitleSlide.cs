using Avalonia.Media.Imaging;
using ReactiveUI;
using System;

namespace HandsLiftedApp.Data.Slides
{
    public class SongTitleSlide : Slide
    {
        private string _title = "";
        public string Title
        {
            get => _title; set
            {
                this.RaiseAndSetIfChanged(ref _title, value);
                //_cached = null;
            }
        }

        private string _copyright = "";
        public string Copyright
        {
            get => _copyright; set
            {
                this.RaiseAndSetIfChanged(ref _copyright, value);
                //_cached = null;
            }
        }

        public override string? SlideText => Title;

        public override string? SlideLabel => null;

        private Bitmap _cached;

        public SongTitleSlide()
        {
        }

        // TODO make this an interface
        public Bitmap? cached { get => _cached; set => this.RaiseAndSetIfChanged(ref _cached, value); }
    }
    public interface ISongTitleSlideState : ISlideState { }
}
