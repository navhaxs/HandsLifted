using Avalonia.Media.Imaging;
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
    public class ImageSlideState : SlideState<ImageSlide>
    {
        public ImageSlideState(ImageSlide data) : base(data)
        {
            using (Stream imageStream = File.OpenRead(data.ImagePath))
            {
                Image = Bitmap.DecodeToWidth(imageStream, 400);
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
