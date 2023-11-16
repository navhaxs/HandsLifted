using Avalonia.Media;
using ReactiveUI;
using System;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.SlideTheme
{

    [Serializable]
    public class BaseSlideTheme : ReactiveObject
    {
        // Slide Design Meta
        private string _Name;
        public string Name { get => _Name; set => this.RaiseAndSetIfChanged(ref _Name, value); }

        // Slide Design Properties
        private FontFamily _FontFamily;
        [XmlIgnore]
        public FontFamily FontFamily { get => _FontFamily; set => this.RaiseAndSetIfChanged(ref _FontFamily, value); }
        private FontWeight _FontWeight;
        [XmlIgnore]
        public FontWeight FontWeight { get => _FontWeight; set => this.RaiseAndSetIfChanged(ref _FontWeight, value); }
        private Color _TextColour;
        public Color TextColour { get => _TextColour; set => this.RaiseAndSetIfChanged(ref _TextColour, value); }
        private Color _BackgroundColour;
        public Color BackgroundColour { get => _BackgroundColour; set => this.RaiseAndSetIfChanged(ref _BackgroundColour, value); }
        private int _FontSize;
        public int FontSize { get => _FontSize; set => this.RaiseAndSetIfChanged(ref _FontSize, value); }
        private int _LineHeight;
        public int LineHeight { get => _LineHeight; set => this.RaiseAndSetIfChanged(ref _LineHeight, value); }
        private string _BackgroundGraphicFilePath;
        public string BackgroundGraphicFilePath { get => _BackgroundGraphicFilePath; set => this.RaiseAndSetIfChanged(ref _BackgroundGraphicFilePath, value); }

        // TODO - KV map for additional properties
    }
}
