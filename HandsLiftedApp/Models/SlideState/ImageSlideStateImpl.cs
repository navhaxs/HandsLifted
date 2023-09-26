﻿using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Services.Bitmaps;
using HandsLiftedApp.Utils;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Linq;
using static HandsLiftedApp.Services.Bitmaps.BitmapLoadWorkerThread;

namespace HandsLiftedApp.Models.SlideState
{
    public class ImageSlideStateImpl : SlideStateBase<ImageSlide<ImageSlideStateImpl>>, IImageSlideState
    {
        public ImageSlideStateImpl(ref ImageSlide<ImageSlideStateImpl> imageSlide) : base(ref imageSlide)
        {
            //imageSlide.WhenAnyValue(p => p.ImagePath);
            imageSlide.WhenAnyValue(p => p.SourceMediaPath)
            .Subscribe(_ =>
            {
                if (_ == null)
                    return;
                LoadImage(_);
            });
        }

        private const int IMAGE_WIDTH = 1920;
        private const int THUMBNAIL_WIDTH = 500;

        public void LoadImage(string imagePath)
        {
            System.Action<Bitmap> value = (bitmap) =>
            {
                //if (Image != null)
                //{
                //    Debug.Print("Already set");
                //}
                Image = bitmap;
            };
            BitmapLoadWorkerThread.priorityQueue.Add(new BitmapLoadRequest() { BitmapFilePath = imagePath, Callback = value });
        }

        public void EnsureImageLoaded()
        {
            if (Image is not null)
            {
                // TODO: force image reload if (hash of filpath+file.io last modified) has changed
                return;
            }

            var path = _slide.SourceMediaPath;

            Image = BitmapUtils.LoadBitmap(path, IMAGE_WIDTH);
        }

        public string ImageName
        {
            get => Path.GetFileName(_slide.SourceMediaPath);
        }

        // note: on low memory scenarios, this can be unloaded for the not currently-active slide
        private Bitmap? _image;
        public Bitmap? Image
        {
            get => _image;
            private set
            {
                this.RaiseAndSetIfChanged(ref _image, value);
                this.RaisePropertyChanged(nameof(Thumbnail));
            }
        }

        public Bitmap? Thumbnail
        {
            get => _image;
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
