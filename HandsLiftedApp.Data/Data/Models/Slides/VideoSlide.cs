using ReactiveUI;
using System;
using System.IO;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Slides
{
    [XmlRoot(Namespace = Constants.Namespace)]
    [Serializable]
    public class VideoSlide : MediaSlide
    {
        /// <summary>
        /// Loop the playback of this video item
        /// </summary>
        public bool IsLoop { get => _isLoop; set => this.RaiseAndSetIfChanged(ref _isLoop, value); }
        private bool _isLoop = false;

        /// <summary>
        /// Mute the playback of this video item
        /// </summary>
        public bool IsMute { get => _isMute; set => this.RaiseAndSetIfChanged(ref _isMute, value); }
        private bool _isMute = false;

        public VideoSlide(String videoPath = @"C:\VisionScreens\TestImages\WA22 Speaker Interview.mp4") : this()
        {
            SourceMediaFilePath = videoPath;
        }

        public VideoSlide()
        {
        }

        public override string? SlideText => null;

        public override string? SlideLabel => Path.GetFileName(SourceMediaFilePath);

        public override void OnEnterSlide()
        {
            base.OnEnterSlide();
        }

        public override void OnLeaveSlide()
        {
            base.OnLeaveSlide();
        }
    }

    public interface IVideoSlideState : ISlideState
    {
    }
}
