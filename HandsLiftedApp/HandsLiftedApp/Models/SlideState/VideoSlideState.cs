using Avalonia.Controls.Shapes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Utils;
using LibVLCSharp.Shared;
using Microsoft.WindowsAPICodePack.Shell;
using ReactiveUI;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HandsLiftedApp.Models.SlideState
{
    // state - create VLC when slide activated. destroy VLC after some timeout after slide deactive
    public class VideoSlideStateImpl : SlideStateBase<VideoSlide<VideoSlideStateImpl>>, IVideoSlideState, IDisposable
    {
        private LibVLC _libVLC;
        public Media _media { get; set; }

        private CompositeDisposable _subscriptions;

        private VideoSlide<VideoSlideStateImpl> parentVideoSlide;

        public VideoSlideStateImpl(ref VideoSlide<VideoSlideStateImpl> videoSlide) : base(ref videoSlide)
        {
            parentVideoSlide = videoSlide;
            VideoPath = videoSlide.VideoPath;

            // TODO init only when actually needed (on slide enter)
            try
            {
                Log.Debug("init VLC");
                _libVLC = new LibVLC();
                MediaPlayer = new MediaPlayer(_libVLC);
                Log.Debug("init VLC - DONE");

                MediaPlayer.EndReached += MediaPlayer_EndReached;


                string absolute = new Uri(VideoPath).AbsoluteUri;
                bool isfile = absolute.StartsWith("file://");
                _media = new Media(_libVLC, VideoPath, isfile ? FromType.FromPath : FromType.FromLocation);
                MediaPlayer.Media = _media;


                bool operationActive = false;
                var refresh = new Subject<Unit>();

                //disable events while some operations active, as sometimes causing deadlocks
                IObservable<Unit> Wrap(IObservable<Unit> obs)
                    => obs.Where(_ => !operationActive).Merge(refresh).ObserveOn(AvaloniaScheduler.Instance);

                IObservable<Unit> VLCEvent(string name)
                    => Observable.FromEventPattern(MediaPlayer, name).Select(_ => Unit.Default);

                void Op(Action action)
                {
                    operationActive = true;
                    action();
                    operationActive = false;
                    refresh.OnNext(Unit.Default);
                };

                var positionChanged = VLCEvent(nameof(MediaPlayer.PositionChanged));
                var playingChanged = VLCEvent(nameof(MediaPlayer.Playing));
                var stoppedChanged = VLCEvent(nameof(MediaPlayer.Stopped));
                var timeChanged = VLCEvent(nameof(MediaPlayer.TimeChanged));
                var lengthChanged = VLCEvent(nameof(MediaPlayer.LengthChanged));
                var muteChanged = VLCEvent(nameof(MediaPlayer.Muted))
                                    .Merge(VLCEvent(nameof(MediaPlayer.Unmuted)));
                var endReachedChanged = VLCEvent(nameof(MediaPlayer.EndReached));
                var pausedChanged = VLCEvent(nameof(MediaPlayer.Paused));
                var volumeChanged = VLCEvent(nameof(MediaPlayer.VolumeChanged));
                var stateChanged = Observable.Merge(playingChanged, stoppedChanged, endReachedChanged, pausedChanged);
                var hasMediaObservable = this.WhenAnyValue(v => v.MediaUrl, v => !string.IsNullOrEmpty(v));
                var fullState = Observable.Merge(
                                    stateChanged,
                                    VLCEvent(nameof(MediaPlayer.NothingSpecial)),
                                    VLCEvent(nameof(MediaPlayer.Buffering)),
                                    VLCEvent(nameof(MediaPlayer.EncounteredError))
                                    );

                _subscriptions = new CompositeDisposable
            {
                Wrap(positionChanged).DistinctUntilChanged(_ => Position).Subscribe(_ => this.RaisePropertyChanged(nameof(Position))),
                Wrap(timeChanged).DistinctUntilChanged(_ => CurrentTime).Subscribe(_ => this.RaisePropertyChanged(nameof(CurrentTime))),
                Wrap(timeChanged).DistinctUntilChanged(_ => PrettyCurrentTime).Subscribe(_ => this.RaisePropertyChanged(nameof(PrettyCurrentTime))),
                Wrap(timeChanged).DistinctUntilChanged(_ => PrettyRemainingTime).Subscribe(_ => this.RaisePropertyChanged(nameof(PrettyRemainingTime))),
                Wrap(lengthChanged).DistinctUntilChanged(_ => Duration).Subscribe(_ => this.RaisePropertyChanged(nameof(Duration))),
                Wrap(lengthChanged).DistinctUntilChanged(_ => PrettyDuration).Subscribe(_ => this.RaisePropertyChanged(nameof(PrettyDuration))),
                Wrap(muteChanged).DistinctUntilChanged(_ => IsMuted).Subscribe(_ => this.RaisePropertyChanged(nameof(IsMuted))),
                Wrap(fullState).DistinctUntilChanged(_ => State).Subscribe(_ => this.RaisePropertyChanged(nameof(State))),
                Wrap(volumeChanged).DistinctUntilChanged(_ => Volume).Subscribe(_ => this.RaisePropertyChanged(nameof(Volume))),
                Wrap(fullState).DistinctUntilChanged(_ => Information).Subscribe(_ => this.RaisePropertyChanged(nameof(Information)))
            };

                bool active() => _subscriptions == null ? false : MediaPlayer.IsPlaying || MediaPlayer.CanPause;

                stateChanged = Wrap(stateChanged);

            }
            catch
            {

            }

            // do I also need to check for file is on disk (e.g. GOogle Drive File Stream - will this hang here....)loading?

            if (!File.Exists(VideoPath))
            {
                // ??
            }

            try
            {

                ShellFile shellFile = ShellFile.FromFilePath(VideoPath);
                var bm = shellFile.Thumbnail.Bitmap;
                Thumbnail = bm.ConvertToAvaloniaBitmap();
            }
            catch { }
        }

        private Bitmap? _thumbnail;
        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            private set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }

        private void MediaPlayer_EndReached(object? sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem((object state) =>
            {
                if (parentVideoSlide.IsLoop)
                {
                    // loop this media
                    this.MediaPlayer.Stop();
                    this.MediaPlayer.Play();
                }
            });
        }

        // TODO: thumbnail
        //private Bitmap? _image;

        //public Bitmap? Image
        //{
        //    get => _image;
        //    private set => this.RaiseAndSetIfChanged(ref _image, value);
        //}
        public string VideoPath { get; set; }
        public MediaPlayer MediaPlayer { get; }

        public async Task OnSlideEnterEvent()
        {

        }
        public async Task OnSlideLeaveEvent()
        {
            MediaPlayer?.Stop();
        }

        // --- stuff from vlc sample ---

        private string _MediaUrl;

        public string MediaUrl
        {
            get => _MediaUrl;
            set => this.RaiseAndSetIfChanged(ref _MediaUrl, value);
        }

        public bool IsMuted
        {
            get => MediaPlayer.Mute;
            set => MediaPlayer.Mute = value;
        }

        public TimeSpan CurrentTime => TimeSpan.FromMilliseconds(MediaPlayer?.Time > -1 ? MediaPlayer.Time : 0);
        public String PrettyCurrentTime => CurrentTime.ToString(@"hh\:mm\:ss");
        public String PrettyRemainingTime => $"-{Duration.Subtract(CurrentTime).ToString(@"hh\:mm\:ss")}";
        public TimeSpan Duration => TimeSpan.FromMilliseconds(MediaPlayer?.Length > -1 ? MediaPlayer.Length : 0);
        public String PrettyDuration => Duration.ToString(@"hh\:mm\:ss");

        public VLCState State => MediaPlayer.State;

        public string MediaInfo
        {
            get
            {
                var m = MediaPlayer.Media;

                if (m == null)
                    return "";

                var vt = m.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Video);
                var at = m.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Audio);
                var videoCodec = m.CodecDescription(TrackType.Video, vt.Codec);
                var audioCodec = m.CodecDescription(TrackType.Audio, at.Codec);

                return $"{vt.Data.Video.Width}x{vt.Data.Video.Height} {vt.Description}video: {videoCodec} audio: {audioCodec}";
            }
        }

        public string Information => $"FPS:{MediaPlayer.Fps} {MediaInfo}";

        public float Position
        {
            get => (MediaPlayer != null) ? MediaPlayer.Position * 100.0f : 0f;
            set
            {
                if (MediaPlayer != null && MediaPlayer.Position != value / 100.0f)
                {
                    MediaPlayer.Position = value / 100.0f;
                }
            }
        }

        public int Volume
        {
            get => MediaPlayer != null ? MediaPlayer.Volume : 0;
            set => MediaPlayer.Volume = value;
        }

        public ICommand PlayCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ForwardCommand { get; }
        public ICommand BackwardCommand { get; }
        public ICommand NextFrameCommand { get; }
        public ICommand OpenCommand { get; }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _subscriptions = null;
            MediaPlayer.Dispose();
        }
    }
}
