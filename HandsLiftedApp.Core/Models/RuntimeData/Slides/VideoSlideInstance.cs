using System;
using System.Linq;
using HandsLiftedApp.Data.Slides;
using LibMpv.Client;
using LibMpv.Context.MVVM;
using ReactiveUI;

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
    public class VideoSlideInstance : VideoSlide
    {
        public VideoSlideInstance(string videoPath = "C:\\VisionScreens\\TestImages\\WA22 Speaker Interview.mp4") :
            base(videoPath)
        {
            var Context = Globals.MpvContextInstance;

            // Register properties for observation
            foreach (var observableProperty in observableProperties)
                Context.ObserveProperty(observableProperty.LibMpvName, observableProperty.LibMpvFormat, 0);

            // Register router LibMpv => MVVM
            Context.PropertyChanged += MpvContextPropertyChanged;
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
                    return Globals.MpvContextInstance?.GetPropertyLong("duration");
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
                    Globals.MpvContextInstance?.SetPropertyLong("duration", value.Value);
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
                    TimeSpan time = TimeSpan.FromSeconds((double)Duration);

//here backslash is must to tell that colon is
//not the part of format, it just a character that we want in output
                    string str = time.ToString(@"hh\:mm\:ss");
                    return str;
                }

                return "";
            }
        }

        public string? PrettyRemainingTime
        {
            get
            {
                if (Duration != null && TimePos != null)
                {
                    TimeSpan time = TimeSpan.FromSeconds((double)Duration - (double)TimePos);

//here backslash is must to tell that colon is
//not the part of format, it just a character that we want in output
                    string str = time.ToString(@"hh\:mm\:ss");
                    return str;
                }

                return "";
            }
        }


        public long? TimePos
        {
            get
            {
                try
                {
                    return Globals.MpvContextInstance?.GetPropertyLong("time-pos");
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
                    Globals.MpvContextInstance?.SetPropertyLong("time-pos", value.Value);
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
                if (TimePos != null)
                {
                    TimeSpan time = TimeSpan.FromSeconds((double)TimePos);

//here backslash is must to tell that colon is
//not the part of format, it just a character that we want in output
                    string str = time.ToString(@"hh\:mm\:ss");
                    return str;
                }

                return "";
            }
        }

        public bool? Paused
        {
            get => Globals.MpvContextInstance?.GetPropertyFlag("pause");
            set
            {
                if (value == null) return;
                Globals.MpvContextInstance?.SetPropertyFlag("pause", value.Value);
            }
        }


        static PropertyToObserve[] observableProperties =
        [
            new() { MvvmName = nameof(Duration), LibMpvName = "duration", LibMpvFormat = mpv_format.MPV_FORMAT_INT64 },
            new() { MvvmName = nameof(TimePos), LibMpvName = "time-pos", LibMpvFormat = mpv_format.MPV_FORMAT_INT64 },
            new() { MvvmName = nameof(Paused), LibMpvName = "pause", LibMpvFormat = mpv_format.MPV_FORMAT_FLAG }
            // new() { MvvmName=nameof(PlaybackSpeed), LibMpvName="speed", LibMpvFormat = mpv_format.MPV_FORMAT_DOUBLE }
        ];
    }
}