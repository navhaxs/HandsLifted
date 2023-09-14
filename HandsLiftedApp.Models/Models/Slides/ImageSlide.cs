using Avalonia.Media.Imaging;
using HandsLiftedApp.XTransitioningContentControl;
using ReactiveUI;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Slides
{
    [XmlRoot(Namespace = Constants.Namespace)]
    [Serializable]
    public class ImageSlide<T> : MediaSlide, ISlideBitmapRender where T : IImageSlideState
    {
        private T _state;
        [XmlIgnore]
        public T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }

        public ImageSlide(string imagePath = @"C:\VisionScreens\TestImages\SWEC App Announcement.png") : this()
        {
            SourceMediaPath = imagePath;
        }

        public ImageSlide()
        {
            State = (T)Activator.CreateInstance(typeof(T), this);
        }

        public override string? SlideText => null;

        public override string? SlideLabel => Path.GetFileName(SourceMediaPath);

        public override void OnPreloadSlide()
        {
            // does not need to be async
            base.OnEnterSlide();
            State.OnSlideEnterEvent();
        }
        public override void OnEnterSlide()
        {
            base.OnEnterSlide();
            State.OnSlideEnterEvent();
        }

        public override void OnLeaveSlide()
        {
            base.OnLeaveSlide();
            State.OnSlideLeaveEvent();
        }

        public Bitmap GetBitmap() {
            return State.GetBitmap();
        }
    }
    public interface IImageSlideState : ISlideState {
        public Bitmap GetBitmap();
    }
}
