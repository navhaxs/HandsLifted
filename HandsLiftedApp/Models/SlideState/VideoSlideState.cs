using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Utils.LibMpvVideo;
using LibMpv.Client;
using Microsoft.WindowsAPICodePack.Shell;
using ReactiveUI;
using Serilog;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace HandsLiftedApp.Models.SlideState
{
    // state - create VLC when slide activated. destroy VLC after some timeout after slide deactive
    public class VideoSlideStateImpl : SlideStateBase<VideoSlide<VideoSlideStateImpl>>, IVideoSlideState, IDisposable, ISlideState
    {

        public MpvContext? Context { set; get; }


        private CompositeDisposable _subscriptions;

        private VideoSlide<VideoSlideStateImpl> parentVideoSlide;

        // Route property changed events to MVVM context
        private void MpvContextPropertyChanged(object? sender, MpvPropertyEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Name))
            {
                // If there will be a lot of properties, it might be better to do a dictionary lookup
                var observableProperty = observableProperties.FirstOrDefault(it => it.LibMpvName == e.Name);
                if (observableProperty != null)
                {
                    this.RaisePropertyChanged(observableProperty.MvvmName);
                    // RaisePropertyChanged(observableProperty.MvvmName);
                }
            }
        }

        public VideoSlideStateImpl(ref VideoSlide<VideoSlideStateImpl> videoSlide) : base(ref videoSlide)
        {
            Context = Globals.GlobalMpvContextInstance;

            parentVideoSlide = videoSlide;

            videoSlide.WhenAnyValue(videoSlide => videoSlide.SourceMediaPath).Subscribe(SourceMediaPath =>
            {
                if (SourceMediaPath != null || SourceMediaPath != "")
                {
                    try
                    {
                        ShellFile shellFile = ShellFile.FromFilePath(SourceMediaPath);
                        var bm = shellFile.Thumbnail.Bitmap;
                        Thumbnail = bm.ConvertToAvaloniaBitmap();
                        Log.Debug($"Thumbnail loaded for {SourceMediaPath}");
                    }
                    catch
                    {
                        Log.Debug($"Thumbnail FAILED loading for {SourceMediaPath}");
                    }
                }
            });

            // Register propertyes for observation
            foreach (var observableProperty in observableProperties)
                Context.ObserveProperty(observableProperty.LibMpvName, observableProperty.LibMpvFormat, 0);

            // Register router LibMpv => MVVM
            Context.PropertyChanged += MpvContextPropertyChanged;


            // TODO init only when actually needed (on slide enter)
            try
            {
                //    MediaPlayer = new MediaPlayer(Globals.GlobalLibVLCInstance);
                //    MediaPlayer.EndReached += MediaPlayer_EndReached;
                //    string absolute = new Uri(VideoPath).AbsoluteUri;
                //    bool isfile = absolute.StartsWith("file://");
                //    _media = new Media(Globals.GlobalLibVLCInstance, VideoPath, isfile ? FromType.FromPath : FromType.FromLocation);
                //    MediaPlayer.Media = _media;

                //    bool operationActive = false;
                //    var refresh = new Subject<Unit>();

                //    //disable events while some operations active, as sometimes causing deadlocks
                //    IObservable<Unit> Wrap(IObservable<Unit> obs)
                //        => obs.Where(_ => !operationActive).Merge(refresh).ObserveOn(AvaloniaScheduler.Instance);

                //    IObservable<Unit> VLCEvent(string name)
                //        => Observable.FromEventPattern(MediaPlayer, name).Select(_ => Unit.Default);

                //    void Op(Action action)
                //    {
                //        operationActive = true;
                //        action();
                //        operationActive = false;
                //        refresh.OnNext(Unit.Default);
                //    };

                //    var positionChanged = VLCEvent(nameof(MediaPlayer.PositionChanged));
                //    var playingChanged = VLCEvent(nameof(MediaPlayer.Playing));
                //    var stoppedChanged = VLCEvent(nameof(MediaPlayer.Stopped));
                //    var timeChanged = VLCEvent(nameof(MediaPlayer.TimeChanged));
                //    var lengthChanged = VLCEvent(nameof(MediaPlayer.LengthChanged));
                //    var muteChanged = VLCEvent(nameof(MediaPlayer.Muted))
                //                        .Merge(VLCEvent(nameof(MediaPlayer.Unmuted)));
                //    var endReachedChanged = VLCEvent(nameof(MediaPlayer.EndReached));
                //    var pausedChanged = VLCEvent(nameof(MediaPlayer.Paused));
                //    var volumeChanged = VLCEvent(nameof(MediaPlayer.VolumeChanged));
                //    var stateChanged = Observable.Merge(playingChanged, stoppedChanged, endReachedChanged, pausedChanged);
                //    var hasMediaObservable = this.WhenAnyValue(v => v.MediaUrl, v => !string.IsNullOrEmpty(v));
                //    var fullState = Observable.Merge(
                //                        stateChanged,
                //                        VLCEvent(nameof(MediaPlayer.NothingSpecial)),
                //                        VLCEvent(nameof(MediaPlayer.Buffering)),
                //                        VLCEvent(nameof(MediaPlayer.EncounteredError))
                //                        );

                //    _subscriptions = new CompositeDisposable
                //{
                //    Wrap(positionChanged).DistinctUntilChanged(_ => Position).Subscribe(_ => this.RaisePropertyChanged(nameof(Position))),
                //    Wrap(timeChanged).DistinctUntilChanged(_ => CurrentTime).Subscribe(_ => this.RaisePropertyChanged(nameof(CurrentTime))),
                //    Wrap(timeChanged).DistinctUntilChanged(_ => PrettyCurrentTime).Subscribe(_ => this.RaisePropertyChanged(nameof(PrettyCurrentTime))),
                //    Wrap(timeChanged).DistinctUntilChanged(_ => PrettyRemainingTime).Subscribe(_ => this.RaisePropertyChanged(nameof(PrettyRemainingTime))),
                //    Wrap(lengthChanged).DistinctUntilChanged(_ => Duration).Subscribe(_ => this.RaisePropertyChanged(nameof(Duration))),
                //    Wrap(lengthChanged).DistinctUntilChanged(_ => PrettyDuration).Subscribe(_ => this.RaisePropertyChanged(nameof(PrettyDuration))),
                //    Wrap(muteChanged).DistinctUntilChanged(_ => IsMuted).Subscribe(_ => this.RaisePropertyChanged(nameof(IsMuted))),
                //    Wrap(fullState).DistinctUntilChanged(_ => State).Subscribe(_ => this.RaisePropertyChanged(nameof(State))),
                //    Wrap(volumeChanged).DistinctUntilChanged(_ => Volume).Subscribe(_ => this.RaisePropertyChanged(nameof(Volume))),
                //    Wrap(fullState).DistinctUntilChanged(_ => Information).Subscribe(_ => this.RaisePropertyChanged(nameof(Information))),
                //    Wrap(fullState).DistinctUntilChanged(_ => ShouldDisplayPauseButton).Subscribe(_ => this.RaisePropertyChanged(nameof(ShouldDisplayPauseButton)))
                //};

                //    bool active() => _subscriptions == null ? false : MediaPlayer.IsPlaying || MediaPlayer.CanPause;

                //    stateChanged = Wrap(stateChanged);
                // Globals.GlobalMpvContextInstance.Seek += (sender, args) =>
                // {
                //     Debug.Print(args.ToString());
                // };
                //
            }
            catch
            {

            }

            // do I also need to check for file is on disk (e.g. Google Drive File Stream - will this hang here....)loading?
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
                    // this.MediaPlayer.Stop();
                    // this.MediaPlayer.Play();
                }
            });
        }

        public long? Duration
        {
            get
            {
                try
                {
                    return Globals.GlobalMpvContextInstance?.GetPropertyLong("duration");
                }
                catch (MpvException ex)
                {
                    return null;
                }
            }
            set
            {
                if (value == null) return;
                try
                {
                    Globals.GlobalMpvContextInstance?.SetPropertyLong("duration", value.Value);
                }
                catch (MpvException ex)
                {
                }
            }
        }

        public long? TimePos
        {
            get
            {
                try
                {
                    return Globals.GlobalMpvContextInstance?.GetPropertyLong("time-pos");
                }
                catch (MpvException ex)
                {
                    return null;
                }
            }
            set
            {
                if (value == null) return;
                try
                {
                    Globals.GlobalMpvContextInstance?.SetPropertyLong("time-pos", value.Value);
                }
                catch (MpvException ex)
                {
                }
            }
        }

        public delegate void SlideLeaveHandler(object sender, SlideLeaveEventArgs e);
        public event SlideLeaveHandler OnSlideLeave;


        void ISlideState.OnSlideLeaveEvent()
        {
            // MediaPlayer?.Stop();
            Log.Debug("VideoSlideStateImpl OnSlideLeaveEvent");

            Globals.GlobalMpvContextInstance.SetPropertyFlag("pause", true);

            // Make sure someone is listening to event
            if (OnSlideLeave == null) return;

            SlideLeaveEventArgs args = new SlideLeaveEventArgs();
            OnSlideLeave(this, args);
        }

        public class SlideLeaveEventArgs : EventArgs
        {
        }

        // --- stuff from vlc sample ---

        private string _MediaUrl;

        public string MediaUrl
        {
            get => _MediaUrl;
            set => this.RaiseAndSetIfChanged(ref _MediaUrl, value);
        }

        public bool? IsMuted
        {
            get => false;//MediaPlayer?.Mute;
            //set => MediaPlayer.Mute = value;
        }

        //public TimeSpan CurrentTime => TimeSpan.FromMilliseconds(MediaPlayer?.Time > -1 ? MediaPlayer.Time : 0);
        //public String PrettyCurrentTime => CurrentTime.ToString(@"hh\:mm\:ss");
        //public String PrettyRemainingTime => $"-{Duration.Subtract(CurrentTime).ToString(@"hh\:mm\:ss")}";
        //public TimeSpan Duration => TimeSpan.FromMilliseconds(MediaPlayer?.Length > -1 ? MediaPlayer.Length : 0);
        //public String PrettyDuration => Duration.ToString(@"hh\:mm\:ss");

        //public VLCState State => MediaPlayer.State;

        //public bool ShouldDisplayPauseButton => MediaPlayer.State == VLCState.Playing;

        // public float Position
        // {
        //     get => (MediaPlayer != null) ? MediaPlayer.Position * 100.0f : 0f;
        //     set
        //     {
        //         if (MediaPlayer != null && MediaPlayer.Position != value / 100.0f)
        //         {
        //             MediaPlayer.Position = value / 100.0f;
        //         }
        //     }
        // }
        //
        // public int Volume
        // {
        //     get => MediaPlayer != null ? MediaPlayer.Volume : 0;
        //     set => MediaPlayer.Volume = value;
        // }

        //public ICommand PlayCommand { get; }
        //public ICommand StopCommand { get; }
        //public ICommand PauseCommand { get; }
        //public ICommand ForwardCommand { get; }
        //public ICommand BackwardCommand { get; }
        //public ICommand NextFrameCommand { get; }
        //public ICommand OpenCommand { get; }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _subscriptions = null;
            // MediaPlayer.Dispose();
        }

        void ISlideState.OnSlideEnterEvent()
        {
            throw new NotImplementedException();
        }

        static PropertyToObserve[] observableProperties = new PropertyToObserve[]
        {
            new() { MvvmName=nameof(Duration), LibMpvName="duration", LibMpvFormat = mpv_format.MPV_FORMAT_INT64 },
            new() { MvvmName=nameof(TimePos), LibMpvName="time-pos", LibMpvFormat = mpv_format.MPV_FORMAT_INT64 },
            // new() { MvvmName=nameof(Paused), LibMpvName="pause", LibMpvFormat = mpv_format.MPV_FORMAT_FLAG },
            // new() { MvvmName=nameof(PlaybackSpeed), LibMpvName="speed", LibMpvFormat = mpv_format.MPV_FORMAT_DOUBLE }
        };


    }
}
