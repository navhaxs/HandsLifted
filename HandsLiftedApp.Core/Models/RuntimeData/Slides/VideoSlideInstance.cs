using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Models.RuntimeData.Slides
{
    public class VideoSlideInstance : VideoSlide
    {
        public VideoSlideInstance(string videoPath = "C:\\VisionScreens\\TestImages\\WA22 Speaker Interview.mp4") : base(videoPath)
        {
        }
    }
}