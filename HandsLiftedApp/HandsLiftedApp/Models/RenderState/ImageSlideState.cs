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

namespace HandsLiftedApp.Models.Render
{
    public class ImageSlideState : SlideState
    {
        public ImageSlideState(ImageSlide data) : base(data)
        {
            using (Stream imageStream = File.OpenRead(data.ImagePath))
            {
                Image = Bitmap.DecodeToWidth(imageStream, 1920);
            }
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
