using HandsLiftedApp.XTransitioningContentControl;
using ReactiveUI;
using Serilog;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Slides
{
    [XmlRoot(Namespace = Constants.Namespace)]
    [Serializable]
    public class VideoSlide<T> : Slide, IDynamicSlideRender where T : IVideoSlideState
    {
        T _state;

        [XmlIgnore]
        public T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }

        public string VideoPath { get => _videoPath; set => this.RaiseAndSetIfChanged(ref _videoPath, value); }
        private string _videoPath;

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

        public VideoSlide(String videoPath = @"C:\VisionScreens\TestImages\WA22 Speaker Interview.mp4")
        {
            VideoPath = videoPath;

            try
            {
                State = (T)Activator.CreateInstance(typeof(T), this);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        public VideoSlide()
        {

            try
            {
                State = (T)Activator.CreateInstance(typeof(T), this);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        public override string? SlideText => null;

        public override string? SlideLabel => Path.GetFileName(VideoPath);

        public override void OnEnterSlide()
        {
            Log.Debug("VideoSlide OnEnterSlide");
            base.OnEnterSlide();
            State.OnSlideEnterEvent();
        }

        public override void OnLeaveSlide()
        {
            base.OnLeaveSlide();
            State.OnSlideLeaveEvent();
        }
    }

    public interface IVideoSlideState : ISlideState
    {
    }

}
