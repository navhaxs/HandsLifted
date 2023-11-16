using Avalonia;
using ReactiveUI;
using System.Runtime.Serialization;

namespace HandsLiftedApp.ViewModels
{
    [KnownType(typeof(DisplayModel))]
    [DataContract]
    public class PreferencesViewModel : ReactiveObject
    {
        public PreferencesViewModel()
        {
            //var canSearch = this
            //    .WhenAnyValue(x => x.SearchQuery)
            //    .Select(query => !string.IsNullOrWhiteSpace(query));

            //Search = ReactiveCommand.CreateFromTask(
            //    () => Task.Delay(1000), // a long-running operation
            //    canSearch);
        }

        //[IgnoreDataMember]
        //public ReactiveCommand<Unit, Unit> Search { get; }


        //private string _logoFilePath;
        //[DataMember]
        //public string LogoFilePath
        //{
        //    get => _logoFilePath;
        //    set => this.RaiseAndSetIfChanged(ref _logoFilePath, value);
        //}

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

        //private string _myCombo;
        //[DataMember]
        //public string ComboSelection
        //{
        //    get => _myCombo;
        //    set => this.RaiseAndSetIfChanged(ref _myCombo, value);
        //}

        private string _libraryPath = @"C:\VisionScreens\TestSongs\";
        [DataMember]
        public string LibraryPath
        {
            get => _libraryPath;
            set => this.RaiseAndSetIfChanged(ref _libraryPath, value);
        }

        // testing
        private int _SlideThumbnailSizeMultiplier = 100;
        [DataMember]
        public int SlideThumbnailSizeMultiplier
        {
            get => _SlideThumbnailSizeMultiplier;
            set => this.RaiseAndSetIfChanged(ref _SlideThumbnailSizeMultiplier, value);
        }

        private int _ItemWidth = 480;
        [DataMember]
        public int ItemWidth
        {
            get => _ItemWidth;
            set => this.RaiseAndSetIfChanged(ref _ItemWidth, value);
        }

        private int _ItemHeight = 320;//70;
        [DataMember]
        public int ItemHeight
        {
            get => _ItemHeight;
            set => this.RaiseAndSetIfChanged(ref _ItemHeight, value);
        }



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


            //protected bool Equals(DisplayModel dm)
            //{
            //    return this.X == dm.X
            //                  && this.Y == dm.Y
            //                  && this.Width == dm.Width
            //                  && this.Height == dm.Height;
            //}
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
