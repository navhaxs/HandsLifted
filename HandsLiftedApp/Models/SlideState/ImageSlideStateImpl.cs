using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.SlideState {
    public class ImageSlideStateImpl : SlideStateBase<ImageSlide<ImageSlideStateImpl>>, IImageSlideState {
        public ImageSlideStateImpl(ref ImageSlide<ImageSlideStateImpl> imageSlide) : base(ref imageSlide) {
            // this runs on a separate bg thread (todo: with lock)?
            Task.Run(() => {
                //if (Monitor.TryEnter(loadImageLock)) {
                //    try {
                        LoadThumbnail();
                //    }
                //    finally {
                //        Monitor.Exit(loadImageLock);
                //    }
                //}
            });
        }

        private static readonly object loadImageLock = new object();

        private const int IMAGE_WIDTH = 1920;
        private const int THUMBNAIL_WIDTH = 500;

        public void EnsureImageLoaded() {
            if (Image is not null) {
                return;
            }

            //// skip if already running
            var path = _slide.ImagePath;

            if (File.Exists(path)) {
                using (Stream imageStream = File.OpenRead(path)) {
                    Image = Bitmap.DecodeToWidth(imageStream, IMAGE_WIDTH);
                }
            }
        }

        public void LoadThumbnail() {
            // todo skip if already running
            var path = _slide.ImagePath;

            if (File.Exists(path)) {
                using (Stream imageStream = File.OpenRead(path)) {
                    try {
                        Thumbnail = Bitmap.DecodeToWidth(imageStream, THUMBNAIL_WIDTH);
                    }
                    catch (Exception e) {
                        Log.Error($"Failed to decode image [{path}]");
                    }
                }
            }
        }

        public string ImageName {
            get {
                return Path.GetFileName(_slide.ImagePath);
            }
        }

        // note: on low memory scenarios, this can be unloaded for the not currently-active slide
        private Bitmap? _image;
        public Bitmap? Image {
            get => _image;
            private set => this.RaiseAndSetIfChanged(ref _image, value);
        }

        private Bitmap? _thumbnail;
        public Bitmap? Thumbnail {
            get => _thumbnail;
            private set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }
        public void OnSlideEnterEvent() {
            EnsureImageLoaded();
        }
        public void OnSlideLeaveEvent() {
            // low memory mode
            //Image = null;
        }

        public Bitmap GetBitmap() {
            EnsureImageLoaded();
            return Image;
        }
    }
}
