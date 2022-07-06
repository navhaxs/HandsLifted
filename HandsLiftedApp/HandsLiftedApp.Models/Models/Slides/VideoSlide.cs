using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Data.Slides
{
    public class VideoSlide<T> : Slide where T : IVideoSlideState
    {
        public string VideoPath { get; set; }

        public VideoSlide(String videoPath = @"C:\VisionScreens\TestImages\WA22 Speaker Interview.mp4")
        {
            VideoPath = videoPath;
        }

        //


        public override string SlideText => "video";


    }

    public interface IVideoSlideState : ISlideState { }

}
