using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.SlideState
{
    public class ImageSlideStateImpl : SlideStateBase<ImageSlide<ImageSlideStateImpl>>, IImageSlideState
    {
        public ImageSlideStateImpl(ref ImageSlide<ImageSlideStateImpl> imageSlide) : base(ref imageSlide)
        {
            // this runs on a separate bg thread (todo: with lock)?
            Task.Run(() =>
            {
                // TODO optimise this in a separate thread
                LoadThumbnail();
                EnsureImageLoaded(); // HACK until XTransitioningControl reimplemented
                //if (Monitor.TryEnter(loadImageLock)) {
                //    try {
                //    }
                //    finally {
                //        Monitor.Exit(loadImageLock);
                //    }
                //}
            });
        }

        private static readonly object thumbnailLock = new object();
        private static readonly object loadImageLock = new object();

        private const int IMAGE_WIDTH = 1920;
        private const int THUMBNAIL_WIDTH = 500;

        public void EnsureImageLoaded()
        {
            if (Image is not null)
            {
                return;
            }

            //// skip if already running
            var path = _slide.ImagePath;

            if (File.Exists(path))
            {
                using (Stream imageStream = File.OpenRead(path))
                {
                    Image = Bitmap.DecodeToWidth(imageStream, IMAGE_WIDTH);
                }
            }
        }

        public void LoadThumbnail()
        {
            //await mySemaphoreSlim.WaitAsync();
            //if (Monitor.TryEnter(thumbnailLock))
            //{
                if (Thumbnail != null)
                {
                    return;
                }

                try
                {
                    // todo skip if already running
                    var path = _slide.ImagePath;
                    Debug.Print($"{RuntimeHelpers.GetHashCode(this)} Loading {path}");

                    if (File.Exists(path))
                    {
                        // todo try catch below line:
                        using (Stream imageStream = File.OpenRead(path))
                        {
                            try
                            {
                                // TODO should be using the BitmapLoader util
                                // Also should be on separate thread
                                Thumbnail = Bitmap.DecodeToWidth(imageStream, THUMBNAIL_WIDTH);
                            }
                            catch (Exception e)
                            {
                                Log.Error($"Failed to decode image [{path}]");
                            }
                        }
                    }

                }
                finally
                {
                    //Monitor.Exit(thumbnailLock);
                }
            //}
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

        private Bitmap? _thumbnail = null;
        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            private set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }
        public void OnSlideEnterEvent()
        {
            EnsureImageLoaded();
        }
        public void OnSlideLeaveEvent()
        {
            // low memory mode
            //Image = null;
        }

        public Bitmap GetBitmap()
        {
            EnsureImageLoaded();
            return Image;
        }
    }
}
