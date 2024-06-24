using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Utils;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.ViewModels
{
    [KnownType(typeof(DisplayModel))]
    [DataContract]
    public class AppPreferencesViewModel : ReactiveObject
    {
        public AppPreferencesViewModel()
        {
            // this.WhenAnyValue(p => p.LogoGraphicFile)
            //     .Throttle(TimeSpan.FromMilliseconds(200), RxApp.TaskpoolScheduler)
            //     .Subscribe(_ =>
            //     {
            //         if (LogoGraphicFile == "")
            //         {
            //             return;
            //         }
            //
            //         try
            //         {
            //             if (AssetLoader.Exists(new Uri(LogoGraphicFile)) || File.Exists(LogoGraphicFile))
            //             {
            //                 this.RaisePropertyChanged(nameof(LogoBitmap));
            //             }
            //         }
            //         catch (Exception e)
            //         {
            //             Log.Error(e, "Error loading logo");
            //         }
            //     });
            
            _logoBitmap = this.WhenAnyValue(p => p.LogoGraphicFile)
                .Throttle(TimeSpan.FromMilliseconds(200), RxApp.TaskpoolScheduler)
                .Select(x =>
                {
                    if (string.IsNullOrEmpty(x))
                    {
                        return null;
                    }

                    try
                    {
                        if (File.Exists(x) || AssetLoader.Exists(new Uri(x)))
                        {
                            return BitmapLoader.LoadBitmap(x);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("LogoGraphicFile read failed", ex);
                    }

                    return null;
                })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.LogoBitmap);

        }

        private bool _enableOutputNDI;
        [DataMember]
        public bool EnableOutputNDI
        {
            get => _enableOutputNDI;
            set => this.RaiseAndSetIfChanged(ref _enableOutputNDI, value);
        }

        private bool _enableLyricsOutputNDI;
        [DataMember]
        public bool EnableLyricsOutputNDI
        {
            get => _enableLyricsOutputNDI;
            set => this.RaiseAndSetIfChanged(ref _enableLyricsOutputNDI, value);
        }

        private bool _enableStageDisplayNDI;
        [DataMember]
        public bool EnableStageDisplayNDI
        {
            get => _enableStageDisplayNDI;
            set => this.RaiseAndSetIfChanged(ref _enableStageDisplayNDI, value);
        }

        private bool _onStartupShowOutput;
        [DataMember]
        public bool OnStartupShowOutput
        {
            get => _onStartupShowOutput;
            set => this.RaiseAndSetIfChanged(ref _onStartupShowOutput, value);
        }

        private bool _onStartupShowStage;
        [DataMember]
        public bool OnStartupShowStage
        {
            get => _onStartupShowStage;
            set => this.RaiseAndSetIfChanged(ref _onStartupShowStage, value);
        }

        private bool _onStartupShowLogo;
        [DataMember]
        public bool OnStartupShowLogo
        {
            get => _onStartupShowLogo;
            set => this.RaiseAndSetIfChanged(ref _onStartupShowLogo, value);
        }

        private DisplayModel _stageDisplayBounds;
        [DataMember]
        public DisplayModel StageDisplayBounds
        {
            get => _stageDisplayBounds;
            set => this.RaiseAndSetIfChanged(ref _stageDisplayBounds, value);
        }

        private DisplayModel _outputDisplayBounds;
        [DataMember]
        public DisplayModel OutputDisplayBounds
        {
            get => _outputDisplayBounds;
            set => this.RaiseAndSetIfChanged(ref _outputDisplayBounds, value);
        }

        private string _libraryPath = @"C:\VisionScreens\TestSongs\";
        [DataMember]
        public string LibraryPath
        {
            get => _libraryPath;
            set => this.RaiseAndSetIfChanged(ref _libraryPath, value);
        }
        
        private String _logoGraphicFile = @"avares://HandsLiftedApp.Core/Assets/DefaultTheme/logo-default.png";
        [DataMember]
        public String LogoGraphicFile { get => _logoGraphicFile; set => this.RaiseAndSetIfChanged(ref _logoGraphicFile, value); }
        
        private ObservableAsPropertyHelper<Bitmap?> _logoBitmap;
        [IgnoreDataMember]
        public Bitmap? LogoBitmap => _logoBitmap.Value;

        private BaseSlideTheme _defaultTheme = new BaseSlideTheme();
        [DataMember]
        public BaseSlideTheme DefaultTheme
        {
            get => _defaultTheme; set => this.RaiseAndSetIfChanged(ref _defaultTheme, value);
        }
        
        // private List<BaseSlideTheme> _designs = new() { new BaseSlideTheme() };
        public List<BaseSlideTheme> Designs { get => new(){DefaultTheme}; }

        [DataContract]
        public class DisplayModel : ReactiveObject
        {
            public DisplayModel()
            {
            }

            public DisplayModel(PixelRect bounds)
            {
                X = bounds.X;
                Y = bounds.Y;
                Width = bounds.Width;
                Height = bounds.Height;
            }

            /// <summary>
            /// Gets the Label.
            /// </summary>
            [DataMember]
            public string Label { get; set; } = "Display";

            /// <summary>
            /// Gets the X position.
            /// </summary>
            [DataMember]
            public int X { get; set; }

            /// <summary>
            /// Gets the Y position.
            /// </summary>
            [DataMember]
            public int Y { get; set; }

            /// <summary>
            /// Gets the width.
            /// </summary>
            [DataMember]
            public int Width { get; set; }

            /// <summary>
            /// Gets the height.
            /// </summary>
            [DataMember]
            public int Height { get; set; }

            public override string? ToString()
            {
                return $"{Label} ({X}, {Y}) {Width} x {Height}";
            }

            public override bool Equals(object? obj)
            {
                if (obj is DisplayModel)
                {
                    DisplayModel dm = obj as DisplayModel;
                    return this.X == dm.X
                        && this.Y == dm.Y
                        && this.Width == dm.Width
                        && this.Height == dm.Height;
                }
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                unchecked // Allow arithmetic overflow, numbers will just "wrap around"
                {
                    int hashcode = 1430287;
                    hashcode = hashcode * 7302013 ^ X.GetHashCode();
                    hashcode = hashcode * 7302013 ^ Y.GetHashCode();
                    hashcode = hashcode * 7302013 ^ Width.GetHashCode();
                    hashcode = hashcode * 7302013 ^ Height.GetHashCode();
                    return hashcode;
                }
            }
        }

        [DataContract]
        public class UnsetDisplay : DisplayModel
        {
            public override string? ToString()
            {
                return "(Unset)";
            }
        }
    }
}