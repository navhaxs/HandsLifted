using Avalonia.Media;
using ReactiveUI;
using System;
using System.Xml.Serialization;
using HandsLiftedApp.Data.Data.Models.Types;

namespace HandsLiftedApp.Data.SlideTheme
{

    [Serializable]
    public class BaseSlideTheme : ReactiveObject
    {
        private Guid _id = Guid.NewGuid();
        public Guid Id { get => _id; set => this.RaiseAndSetIfChanged(ref _id, value); }

        // Slide Design Meta
        private string _name = "My new theme";
        public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value); }

        // Slide Design Properties
        public XmlFontFamily FontFamily = new();
        [XmlIgnore]
        public FontFamily FontFamilyAsAvalonia { get => FontFamily; set => this.RaiseAndSetIfChanged(ref FontFamily, value); }
        private FontWeight _fontWeight = FontWeight.Normal;
        // [XmlIgnore]
        public FontWeight FontWeight { get => _fontWeight; set => this.RaiseAndSetIfChanged(ref _fontWeight, value); }
        
        public XmlColor TextColour = Colors.White;
        [XmlIgnore]
        public Color TextAvaloniaColour { get => TextColour; set => this.RaiseAndSetIfChanged(ref TextColour, value); }
        
        private TextAlignment _textAlignment = TextAlignment.Center;
        public TextAlignment TextAlignment
        {
            get => _textAlignment;
            set => this.RaiseAndSetIfChanged(ref  _textAlignment, value);
        }
        
        public XmlColor BackgroundColour = Color.Parse("#4d347f");
        [XmlIgnore]
        public Color BackgroundAvaloniaColour { get => BackgroundColour; set => this.RaiseAndSetIfChanged(ref BackgroundColour, value); }
        
        private int _fontSize = 100;
        public int FontSize { get => _fontSize; set => this.RaiseAndSetIfChanged(ref _fontSize, value); }
        private int _lineHeight = 150;
        public int LineHeight { get => _lineHeight; set => this.RaiseAndSetIfChanged(ref _lineHeight, value); }
        
        private string? _backgroundGraphicFilePath;
        public string? BackgroundGraphicFilePath { get => _backgroundGraphicFilePath; set => this.RaiseAndSetIfChanged(ref _backgroundGraphicFilePath, value); }
        
        // [XmlAttribute]

        // TODO - KV map for additional properties
    }
}
