using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Data.Slides
{
    public class ImageSlide<T> : Slide where T : IImageSlideState
    {
        T _state;
        T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }

        public string ImagePath { get; set; }

        public ImageSlide(String imagePath = @"C:\VisionScreens\TestImages\SWEC App Announcement.png")
        {
            ImagePath = imagePath;
            State = (T)Activator.CreateInstance(typeof(T), this);
        }

        public override string? SlideText => null;

        public override string? SlideLabel => Path.GetFileName(ImagePath);

    }
    public interface IImageSlideState : ISlideState { }

}
