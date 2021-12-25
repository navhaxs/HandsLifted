using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models
{
    public class ImageSlide : Slide
    {
        public string ImagePath { get; set; }

        public ImageSlide()
        {
            Image = new Bitmap(@"C:\Users\Jeremy\source\repos\navhaxs\HandsLifted\HandsLiftedApp\HandsLiftedApp\Assets\SWEC-ProPresenter-Logo-Slide.png");
        }

        private Bitmap? _image;

        public Bitmap? Image
        {
            get => _image;
            private set => this.RaiseAndSetIfChanged(ref _image, value);
        }
    }
}
