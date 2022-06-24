using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.SlideState
{
    public class ImageSlideState : SlideStateBase
    {
        public ImageSlideState(ImageSlide data, int index) : base(data, index)
        {
            _ = LoadImage();
        }
        public async Task LoadImage()
        {
            Image = await Task.Run(() => {
                var path = ((ImageSlide)this.Data).ImagePath;
                using (Stream imageStream = File.OpenRead(path))
                {
                    return Bitmap.DecodeToWidth(imageStream, 1920);
                }
            });
        }

        public string ImageName
        {
            get
            {
                return Path.GetFileName(((ImageSlide) this.Data).ImagePath);
            }
        }

        private Bitmap? _image;
        public Bitmap? Image
        {
            get => _image;
            private set => this.RaiseAndSetIfChanged(ref _image, value);
        }
    }
}
