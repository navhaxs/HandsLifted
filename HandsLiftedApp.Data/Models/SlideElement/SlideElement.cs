using System;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Avalonia.Layout;
using Avalonia.Media;
using HandsLiftedApp.Data.Data.Models.Types;
using Newtonsoft.Json;
using ReactiveUI;

namespace HandsLiftedApp.Data.Models.SlideElement
{
    [XmlRoot(Namespace = Constants.Namespace)]
    [XmlInclude(typeof(TextElement))]
    public abstract class SlideElement : ReactiveObject
    {
        // might need id

        private int _x;
        public int X { get => _x; set => this.RaiseAndSetIfChanged(ref _x, value); }
        
        private int _y;
        public int Y { get => _y; set => this.RaiseAndSetIfChanged(ref _y, value); }

        private int _width = 100;
        public int Width { get => _width; set => this.RaiseAndSetIfChanged(ref _width, value); }
        private int _height = 100;
        public int Height { get => _height; set => this.RaiseAndSetIfChanged(ref _height, value); }
    }

    [XmlRoot(Namespace = Constants.Namespace)]
    public class TextElement : SlideElement
    {
        private string _text = "";

        public TextElement()
        {
            _calculatedLineHeight = this.WhenAnyValue(x => x.FontSize, x => x.LineHeightEm,
                    (fontSize, lineHeightEm) => { return (int)(fontSize * lineHeightEm); })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.LineHeight);
            
            _calculatedTextAlignmentLeft = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Left)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentLeft);
            
            _calculatedTextAlignmentCenter = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Center)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentCenter);
            
            _calculatedTextAlignmentRight = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Right)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentRight);
            
            _calculatedTextAlignmentJustify = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Justify)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentJustify); 
            
            _calculatedVerticalAlignmentTop = this.WhenAnyValue(x => x.VerticalAlignment,
                    (a) => a == VerticalAlignment.Top)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentLeft);
            
            _calculatedVerticalAlignmentCenter = this.WhenAnyValue(x => x.VerticalAlignment,
                    (a) => a == VerticalAlignment.Center)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentCenter);
            
            _calculatedVerticalAlignmentBottom = this.WhenAnyValue(x => x.VerticalAlignment,
                    (textAlignment) => textAlignment == VerticalAlignment.Bottom)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentRight);

            _calculatedVerticalAlignmentStretch = this.WhenAnyValue(x => x.VerticalAlignment,
                    (textAlignment) => textAlignment == VerticalAlignment.Stretch)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentJustify);
        }

        public string Text { get => _text; set => this.RaiseAndSetIfChanged(ref _text, value); }
        
        [DataMember]
        public XmlFontFamily FontFamily = new();
        
        [XmlIgnore]
        public FontFamily FontFamilyAsAvalonia { get => FontFamily; set => this.RaiseAndSetIfChanged(ref FontFamily, value); }

        [XmlIgnore]
        public string FontFamilyAsText
        {
            get => (string)FontFamily;
            set { FontFamily = value; }
        }

        private FontWeight _fontWeight = FontWeight.Normal;

        [DataMember]
        public FontWeight FontWeight { get => _fontWeight; set => this.RaiseAndSetIfChanged(ref _fontWeight, value); }

        private int _fontSize = 90;
        public int FontSize { get => _fontSize; set => this.RaiseAndSetIfChanged(ref _fontSize, value); }

        private TextAlignment _textAlignment = TextAlignment.Center;
        public TextAlignment TextAlignment { get => _textAlignment; set => this.RaiseAndSetIfChanged(ref _textAlignment, value); }
        
        private VerticalAlignment _verticalAlignment = VerticalAlignment.Center;
        public VerticalAlignment VerticalAlignment { get => _verticalAlignment; set => this.RaiseAndSetIfChanged(ref _verticalAlignment, value); }
        
        [DataMember]
        public XmlColor ForegroundColour = Color.Parse("White");

        [XmlIgnore]
        public Color ForegroundAvaloniaColour
        {
            get => ForegroundColour;
            set => this.RaiseAndSetIfChanged(ref ForegroundColour, value);
        }

        [DataMember]
        public XmlColor BackgroundColour = Color.Parse("#4d347f");

        [XmlIgnore]
        public Color BackgroundAvaloniaColour
        {
            get => BackgroundColour;
            set => this.RaiseAndSetIfChanged(ref BackgroundColour, value);
        }
        
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextAlignmentLeft;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextAlignmentCenter;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextAlignmentRight;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextAlignmentJustify;

        private readonly ObservableAsPropertyHelper<bool> _calculatedVerticalAlignmentTop;
        private readonly ObservableAsPropertyHelper<bool> _calculatedVerticalAlignmentCenter;
        private readonly ObservableAsPropertyHelper<bool> _calculatedVerticalAlignmentBottom;
        private readonly ObservableAsPropertyHelper<bool> _calculatedVerticalAlignmentStretch;
        
        [XmlIgnore]
        public bool CalculatedTextAlignmentLeft
        {
            get => _calculatedTextAlignmentLeft.Value;
            set { if (value) TextAlignment = TextAlignment.Left; }
        }

        [XmlIgnore]
        public bool CalculatedTextAlignmentCenter
        {
            get => _calculatedTextAlignmentCenter.Value;
            set { if (value) TextAlignment = TextAlignment.Center; }
        }
        [XmlIgnore]
        public bool CalculatedTextAlignmentRight
        {
            get => _calculatedTextAlignmentRight.Value;
            set { if (value) TextAlignment = TextAlignment.Right; }
        }
        [XmlIgnore]
        public bool CalculatedTextAlignmentJustify
        {
            get => _calculatedTextAlignmentJustify.Value;
            set { if (value) TextAlignment = TextAlignment.Justify; }
        }
        
        [XmlIgnore]
        public bool CalculatedVerticalAlignmentTop
        {
            get => _calculatedVerticalAlignmentTop.Value;
            set { if (value) VerticalAlignment = VerticalAlignment.Top; }
        }

        [XmlIgnore]
        public bool CalculatedVerticalAlignmentCenter
        {
            get => _calculatedVerticalAlignmentCenter.Value;
            set { if (value) VerticalAlignment = VerticalAlignment.Center; }
        }
        [XmlIgnore]
        public bool CalculatedVerticalAlignmentBottom
        {
            get => _calculatedVerticalAlignmentBottom.Value;
            set { if (value) VerticalAlignment = VerticalAlignment.Bottom; }
        }
        [XmlIgnore]
        public bool CalculatedVerticalAlignmentStretch
        {
            get => _calculatedVerticalAlignmentStretch.Value;
            set { if (value) VerticalAlignment = VerticalAlignment.Stretch; }
        }
        
        private readonly ObservableAsPropertyHelper<int> _calculatedLineHeight;

        public int LineHeight
        {
            get => _calculatedLineHeight.Value;
        }

        private decimal _lineHeightEm = 1.0M;

        [DataMember]
        public decimal LineHeightEm
        {
            get => Math.Round(_lineHeightEm, 2, MidpointRounding.ToEven);
            set => this.RaiseAndSetIfChanged(ref _lineHeightEm, Math.Round(value, 2, MidpointRounding.ToEven));
        }
    }

    [XmlRoot(Namespace = Constants.Namespace)]
    public class ImageElement : SlideElement
    {
        public string FilePath { get; set; } = "";
    }
}