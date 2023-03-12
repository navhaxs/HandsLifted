using Avalonia.Media.Imaging;
using HandsLiftedApp.XTransitioningContentControl;
using ReactiveUI;

namespace HandsLiftedApp.Data.Slides {
    public class SongTitleSlide<T> : Slide, ISlideBitmapCacheable where T : ISongTitleSlideState {
        private string _title = "";
        public string Title {
            get => _title; set {
                this.RaiseAndSetIfChanged(ref _title, value);
                _cached = null;
            }
        }

        private string _copyright = "";
        public string Copyright {
            get => _copyright; set {
                this.RaiseAndSetIfChanged(ref _copyright, value);
                _cached = null;
            }
        }

        public override string? SlideText => Title;

        public override string? SlideLabel => null;

        private Bitmap? _cached;
        public Bitmap? cached { get => _cached; set => _cached = value; }
    }
    public interface ISongTitleSlideState : ISlideState { }
}
