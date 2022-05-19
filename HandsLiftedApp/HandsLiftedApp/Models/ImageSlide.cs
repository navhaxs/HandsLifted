using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models2
{
    public class ImageSlide : Slide
    {
        public string ImagePath { get; set; }

        public ImageSlide(String imagePath = @"C:\Users\Jeremy\source\repos\navhaxs\HandsLifted\HandsLiftedApp\HandsLiftedApp\Assets\SWEC-ProPresenter-Logo-Slide.png")
        {
            using (Stream imageStream = File.OpenRead(imagePath))
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
