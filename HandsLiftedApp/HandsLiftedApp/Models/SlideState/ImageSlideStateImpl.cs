using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.SlideState
{
    public class ImageSlideStateImpl : SlideStateBase<ImageSlide<ImageSlideStateImpl>>, IImageSlideState
    {
        public ImageSlideStateImpl(ref ImageSlide<ImageSlideStateImpl> imageSlide) : base(ref imageSlide)
        {
            _ = LoadThumbnail();
        }

        private const int IMAGE_WIDTH = 1920;
        private const int THUMBNAIL_WIDTH = 300;

        private static readonly object loadImageLock = new object();

        public async Task LoadImage()
        {
            if (Image is not null)
            {
                return;
            }

            // skip if already running
            if (Monitor.TryEnter(loadImageLock))
            {
                try
                {

                    Image = await Task.Run(() =>
                    {
                        var path = _slide.ImagePath;

                        if (File.Exists(path))
                        {
                            using (Stream imageStream = File.OpenRead(path))
                            {
                                return Bitmap.DecodeToWidth(imageStream, IMAGE_WIDTH);
                            }
                        }

                        return null;
                    });
                }
                finally
                {
                    Monitor.Exit(loadImageLock);
                }
            }


        }
        public async Task LoadThumbnail()
        {
            // todo skip if already running
            Thumbnail = await Task.Run(() =>
            {
                var path = _slide.ImagePath;

                if (File.Exists(path))
                {
                    using (Stream imageStream = File.OpenRead(path))
                    {
                        return Bitmap.DecodeToWidth(imageStream, THUMBNAIL_WIDTH);
                    }
                }

                return null;
            });
        }

        public string ImageName
        {
            get
            {
                return Path.GetFileName(_slide.ImagePath);
            }
        }

        // note: on low memory scenarios, this can be unloaded for the not currently-active slide
        private Bitmap? _image;
        public Bitmap? Image
        {
            get => _image;
            private set => this.RaiseAndSetIfChanged(ref _image, value);
        }

        private Bitmap? _thumbnail;
        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            private set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }
        public void OnSlideEnterEvent()
        {
            LoadImage();
        }
        public void OnSlideLeaveEvent()
        {
            // low memory mode
            //Image = null;
        }
    }
}
