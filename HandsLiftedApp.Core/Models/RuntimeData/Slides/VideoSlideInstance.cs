using System;
using System.Linq;
using System.Threading.Tasks;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.XTransitioningContentControl;
using LibMpv.Client;
using LibMpv.Context;
using LibMpv.Context.MVVM;
using ReactiveUI;
using System.Threading;
using Serilog;

namespace HandsLiftedApp.Core.Models.RuntimeData.Slides
{
    /*
 *
MPV has a lot of different properties, command and options.
This example provides an idea on how to use some of them.

Read MPV documentation:

- https://mpv.io/manual/master/#properties
- https://mpv.io/manual/master/#options
- https://mpv.io/manual/master/#list-of-input-commands

 */
    public class VideoSlideInstance : VideoSlide, ISlideRender, IDisposable
    {
        // Globals.Instance.MpvContextInstance is an app-lifetime singleton (see CLAUDE.md) -
        // subscribing to it without ever unsubscribing pins this instance (and everything it
        // references) alive for the rest of the process. Capture the exact context subscribed
        // to here so Dispose() can unhook from that same instance later.
        private readonly MpvContext? _subscribedContext;

        public VideoSlideInstance(string videoPath = "C:\\VisionScreens\\TestImages\\WA22 Speaker Interview.mp4") :
            base(videoPath)
        {
            var Context = Globals.Instance.MpvContextInstance;

            if (Context != null)
            {
                // Register properties for observation
                foreach (var observableProperty in observableProperties)
                    Context.ObserveProperty(observableProperty.LibMpvName, observableProperty.LibMpvFormat, 0);

                // Register router LibMpv => MVVM
                Context.PropertyChanged += MpvContextPropertyChanged;

                // Log decode/open failures (e.g. stalled cloud-synced file) instead of failing silently
                Context.EndFile += OnEndFile;

                _subscribedContext = Context;
            }
        }

        public void Dispose()
        {
            _enterSlideCts?.Cancel();
            _enterSlideCts?.Dispose();
            _enterSlideCts = null;

            if (_subscribedContext != null)
            {
                _subscribedContext.PropertyChanged -= MpvContextPropertyChanged;
                _subscribedContext.EndFile -= OnEndFile;
            }
        }

        private void OnEndFile(object? sender, MpvEndFileEventArgs e)
        {
            if (e.Reason == mpv_end_file_reason.MPV_END_FILE_REASON_ERROR)
            {
                var errorMessage = libmpv.mpv_error_string(e.Error);
                Log.Error("[VideoSlide] Decode failure for {FilePath}: {Reason}", SourceMediaFilePath, errorMessage);
            }
        }

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
                    this.RaisePropertyChanged("Pretty" + observableProperty.MvvmName);

                    if (observableProperty.MvvmName == "TimePos")
                    {
                        this.RaisePropertyChanged("PrettyRemainingTime");
                    }
                }
            }
        }

        public long? Duration
        {
            get
            {
                try
                {
                    return Globals.Instance.MpvContextInstance?.GetPropertyLong("duration");
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
                    Globals.Instance.MpvContextInstance?.SetPropertyLong("duration", value.Value);
                }
                catch (MpvException ex)
                {
                }
            }
        }


        public string? PrettyDuration
        {
            get
            {
                if (Duration != null)
                {
                    return TimeSpan.FromSeconds((double)Duration).ToString(@"hh\:mm\:ss");
                }

                return null;
            }
        }

        public string? PrettyRemainingTime
        {
            get
            {
                try
                {
                    if (Duration != null && TimePos != null)
                    {
                        return TimeSpan.FromSeconds((double)Duration - (double)TimePos).ToString(@"hh\:mm\:ss");
                    }
                }
                catch (Exception)
                {
                }

                return null;
            }
        }


        public long? TimePos
        {
            get
            {
                try
                {
                    return Globals.Instance.MpvContextInstance?.GetPropertyLong("time-pos");
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
                    Globals.Instance.MpvContextInstance?.SetPropertyLong("time-pos", value.Value);
                }
                catch (MpvException ex)
                {
                }
            }
        }

        public string? PrettyTimePos
        {
            get
            {
                try
                {
                    if (TimePos != null)
                    {
                        return TimeSpan.FromSeconds((double)TimePos).ToString(@"hh\:mm\:ss");
                    }
                }
                catch (Exception)
                {
                }

                return null;
            }
        }

        public bool? Paused
        {
            get => Globals.Instance.MpvContextInstance?.GetPropertyFlag("pause");
            set
            {
                if (value == null) return;
                Globals.Instance.MpvContextInstance?.SetPropertyFlag("pause", value.Value);
            }
        }

        public bool? Mute
        {
            get => Globals.Instance.MpvContextInstance?.GetPropertyFlag("mute");
            set
            {
                if (value == null) return;
                Globals.Instance.MpvContextInstance?.SetPropertyFlag("mute", value.Value);
            }
        }

        public double? Volume
        {
            get => Globals.Instance.MpvContextInstance?.GetPropertyDouble("volume");
            set
            {
                if (value == null) return;
                Globals.Instance.MpvContextInstance?.SetPropertyDouble("volume", value.Value);
            }
        }

        static PropertyToObserve[] observableProperties =
        [
            new() { MvvmName = nameof(Duration), LibMpvName = "duration", LibMpvFormat = mpv_format.MPV_FORMAT_INT64 },
            new() { MvvmName = nameof(TimePos), LibMpvName = "time-pos", LibMpvFormat = mpv_format.MPV_FORMAT_INT64 },
            new() { MvvmName = nameof(Paused), LibMpvName = "pause", LibMpvFormat = mpv_format.MPV_FORMAT_FLAG },
            // new() { MvvmName=nameof(PlaybackSpeed), LibMpvName="speed", LibMpvFormat = mpv_format.MPV_FORMAT_DOUBLE }
            new() { MvvmName = nameof(Mute), LibMpvName = "mute", LibMpvFormat = mpv_format.MPV_FORMAT_FLAG },
            new() { MvvmName = nameof(Volume), LibMpvName = "volume", LibMpvFormat = mpv_format.MPV_FORMAT_DOUBLE }
        ];

        private CancellationTokenSource? _enterSlideCts;

        public override void OnEnterSlide()
        {
            base.OnEnterSlide();

            _enterSlideCts?.Cancel(); // Cancel any previous token
            _enterSlideCts = new CancellationTokenSource();
            var token = _enterSlideCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2400, token);
                    if (!token.IsCancellationRequested)
                    {
                        PlayFromStart();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Task was cancelled, do nothing
                }
            }, token);
        }
        
        public void PlayFromStart()
        {
            Log.Information("MPV loadfile");
            
            // Log current relevant MPV properties
            try 
            {
                var videoDisplay = Globals.Instance.MpvContextInstance.GetPropertyString("video-display");
                var wid = Globals.Instance.MpvContextInstance.GetPropertyString("wid");
                Log.Information($"MPV properties - video-display: {videoDisplay}, wid: {wid}");
            }
            catch (MpvException ex)
            {
                Log.Error(ex, "Failed to get MPV properties");
            }

            Globals.Instance.MpvContextInstance.Command("loadfile", SourceMediaFilePath, "replace");
            Globals.Instance.MpvContextInstance.SetPropertyFlag("pause", false);
        }

        public override void OnLeaveSlide()
        {
            _enterSlideCts?.Cancel();
            _enterSlideCts?.Dispose();
            _enterSlideCts = null;

            Globals.Instance.MpvContextInstance?.Command("stop");
            base.OnLeaveSlide();
        }
    }
}