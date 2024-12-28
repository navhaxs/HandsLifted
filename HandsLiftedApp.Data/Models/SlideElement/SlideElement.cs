using System.Runtime.Serialization;
using System.Xml.Serialization;
using Avalonia.Media;
using HandsLiftedApp.Data.Data.Models.Types;
using ReactiveUI;

namespace HandsLiftedApp.Data.Models.SlideElement
{
    public abstract class SlideElement : ReactiveObject
    {
        // might need id
        
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class TextElement : SlideElement
    {
        public string Text { get; set; } = "";
        
        [DataMember]
        public XmlFontFamily FontFamily = new();
        
        [XmlIgnore]
        public FontFamily FontFamilyAsAvalonia { get => FontFamily; set => this.RaiseAndSetIfChanged(ref FontFamily, value); }
        private FontWeight _fontWeight = FontWeight.Normal;

        [DataMember]
        public FontWeight FontWeight { get => _fontWeight; set => this.RaiseAndSetIfChanged(ref _fontWeight, value); }

        public int FontSize { get; set; } = 90;
        public TextAlignment TextAlignment { get; set; }
        
        [DataMember]
        public XmlColor ForegroundColour = Color.Parse("#4d347f");

        [XmlIgnore]
        public Color ForegroundAvaloniaColour
        {
            get => ForegroundColour;
            set => this.RaiseAndSetIfChanged(ref ForegroundColour, value);
        }
    }

    public class ImageElement : SlideElement
    {
        public string FilePath { get; set; } = "";
    }
}