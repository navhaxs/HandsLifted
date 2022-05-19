using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Data.Slides
{
    public class ImageSlide : Slide
    {
        public string ImagePath { get; set; }

        public ImageSlide(String imagePath = @"C:\VisionScreens\TestImages\SWEC App Announcement.png")
        {
            ImagePath = imagePath;
        }

        //

        public override string SlideLabel => throw new NotImplementedException();

        public override string SlideText => throw new NotImplementedException();

        public override string SlideNumber => throw new NotImplementedException();

    }
}
