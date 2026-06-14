using Avalonia.Media;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using HandsLiftedApp.Data.Data.Models.Types;
using Newtonsoft.Json;

namespace HandsLiftedApp.Data.SlideTheme
{
    [Serializable]
    public class BaseSlideTheme : ReactiveObject
    {
        private Guid _id = Guid.NewGuid();

        public BaseSlideTheme()
        {
            _calculatedLineHeight = this.WhenAnyValue(x => x.FontSize, x => x.LineHeightEm,
                    (fontSize, lineHeightEm) => (int)(fontSize * lineHeightEm))
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
            
            _calculatedTextFontBold = this.WhenAnyValue(x => x.FontWeight,
                (fontWeight) => fontWeight == Avalonia.Media.FontWeight.Bold)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextFontBold);
            
            _calculatedTextFontItalic = this.WhenAnyValue(x => x.FontStyle,
                (fontStyle) => fontStyle == FontStyle.Italic)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextFontItalic);
            
            // _calculatedTextFontUnderline = this.WhenAnyValue(x => x.TextDecorations,
            //     (textDecorations) => textDecorations.Any(decoration => decoration.Location == TextDecorationLocation.Underline))
            //     .ObserveOn(RxApp.MainThreadScheduler)
            //     .ToProperty(this, x => x.CalculatedTextFontUnderline);
        }

        [DataMember]
        public Guid Id
        {
            get => _id;
            set => this.RaiseAndSetIfChanged(ref _id, value);
        }

        // Slide Design Meta
        private string _name = "My new theme";

        [DataMember]
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        // Slide Design Properties
        private XmlFontFamily _fontFamily = new();

        [DataMember]
        public XmlFontFamily FontFamily
        {
            get => _fontFamily;
            set
            {
                this.RaiseAndSetIfChanged(ref _fontFamily, value);
                this.RaisePropertyChanged(nameof(FontFamilyAsAvalonia));
                this.RaisePropertyChanged(nameof(FontFamilyAsText));
            }
        }

        [XmlIgnore]
        public FontFamily FontFamilyAsAvalonia
        {
            get => FontFamily;
            set { FontFamily = value; }
        }

        [XmlIgnore]
        public string FontFamilyAsText
        {
            get => (string)FontFamily;
            set { FontFamily = value; }
        }

        private XmlFontWeight _fontWeight = (XmlFontWeight)Avalonia.Media.FontWeight.Normal;

        [DataMember]
        public XmlFontWeight FontWeight
        {
            get => _fontWeight;
            set => this.RaiseAndSetIfChanged(ref _fontWeight, value);
        }

        private FontStyle _fontStyle = FontStyle.Normal;

        [DataMember]
        public FontStyle FontStyle
        {
            get => _fontStyle;
            set => this.RaiseAndSetIfChanged(ref _fontStyle, value);
        }

        // private TextDecorationCollection _textDecorations = new();
        //
        // [DataMember]
        // public TextDecorationCollection TextDecorations
        // {
        //     get => _textDecorations;
        //     set => this.RaiseAndSetIfChanged(ref _textDecorations, value);
        // }

        [DataMember] public XmlColor TextColour = Colors.White;

        [XmlIgnore]
        public Color TextAvaloniaColour
        {
            get => TextColour;
            set => this.RaiseAndSetIfChanged(ref TextColour, value);
        }

        private TextAlignment _textAlignment = TextAlignment.Center;

        [DataMember]
        public TextAlignment TextAlignment
        {
            get => _textAlignment;
            set => this.RaiseAndSetIfChanged(ref _textAlignment, value);
        }

        private readonly ObservableAsPropertyHelper<bool> _calculatedTextAlignmentLeft;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextAlignmentCenter;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextAlignmentRight;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextAlignmentJustify;
        
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextFontBold;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextFontItalic;
        // private readonly ObservableAsPropertyHelper<bool> _calculatedTextFontUnderline;
        
        public bool CalculatedTextAlignmentLeft
        {
            get => _calculatedTextAlignmentLeft.Value;
            set { if (value) TextAlignment = TextAlignment.Left; }
        }

        public bool CalculatedTextAlignmentCenter
        {
            get => _calculatedTextAlignmentCenter.Value;
            set { if (value) TextAlignment = TextAlignment.Center; }
        }
        public bool CalculatedTextAlignmentRight
        {
            get => _calculatedTextAlignmentRight.Value;
            set { if (value) TextAlignment = TextAlignment.Right; }
        }
        
        public bool CalculatedTextAlignmentJustify
        {
            get => _calculatedTextAlignmentJustify.Value;
            set { if (value) TextAlignment = TextAlignment.Justify; }
        }
        
        public bool CalculatedTextFontBold
        {
            get => _calculatedTextFontBold.Value;
            set { if (value) FontWeight = (XmlFontWeight)Avalonia.Media.FontWeight.Bold; }
        }
        
        public bool CalculatedTextFontItalic
        {
            get => _calculatedTextFontItalic.Value;
            set { FontStyle = value ? FontStyle.Italic : FontStyle.Normal; }
        }
        
        // public bool CalculatedTextFontUnderline
        // {
        //     get => _calculatedTextFontUnderline.Value;
        //     set { TextDecorations = value ? Avalonia.Media.TextDecorations.Underline : new TextDecorationCollection(); }
        // }

        [DataMember] public XmlColor BackgroundColour = Color.Parse("Black");

        [XmlIgnore]
        public Color BackgroundAvaloniaColour
        {
            get => BackgroundColour;
            set => this.RaiseAndSetIfChanged(ref BackgroundColour, value);
        }

        private int _fontSize = 100;

        [DataMember]
        public int FontSize
        {
            get => _fontSize;
            set => this.RaiseAndSetIfChanged(ref _fontSize, value);
        }

        private readonly ObservableAsPropertyHelper<int> _calculatedLineHeight;

        public int LineHeight
        {
            get => _calculatedLineHeight.Value;
        }

        private decimal _lineHeightEm = 1.2M;

        [DataMember]
        public decimal LineHeightEm
        {
            get => Math.Round(_lineHeightEm, 2, MidpointRounding.ToEven);
            set => this.RaiseAndSetIfChanged(ref _lineHeightEm, Math.Round(value, 2, MidpointRounding.ToEven));
        }

        private string? _backgroundGraphicFilePath;

        [DataMember]
        public string? BackgroundGraphicFilePath
        {
            get => _backgroundGraphicFilePath;
            set => this.RaiseAndSetIfChanged(ref _backgroundGraphicFilePath, value);
        }

        private bool _dropShadowEnabled = true;

        [DataMember]
        public bool DropShadowEnabled
        {
            get => _dropShadowEnabled;
            set => this.RaiseAndSetIfChanged(ref _dropShadowEnabled, value);
        }

        private decimal _dropShadowOffsetX = 0M;

        [DataMember]
        public decimal DropShadowOffsetX
        {
            get => _dropShadowOffsetX;
            set => this.RaiseAndSetIfChanged(ref _dropShadowOffsetX, value);
        }

        private decimal _dropShadowOffsetY = 8M;

        [DataMember]
        public decimal DropShadowOffsetY
        {
            get => _dropShadowOffsetY;
            set => this.RaiseAndSetIfChanged(ref _dropShadowOffsetY, value);
        }

        private decimal _dropShadowBlurRadius = 20M;

        [DataMember]
        public decimal DropShadowBlurRadius
        {
            get => _dropShadowBlurRadius;
            set => this.RaiseAndSetIfChanged(ref _dropShadowBlurRadius, value);
        }

        [DataMember] public XmlColor DropShadowColour = Colors.Black;

        [XmlIgnore]
        public Color DropShadowAvaloniaColour
        {
            get => DropShadowColour;
            set => this.RaiseAndSetIfChanged(ref DropShadowColour, value);
        }

        // TODO - KV map for additional properties
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public void CopyFrom(BaseSlideTheme other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            foreach (var prop in GetType().GetProperties().Where(p => p.CanWrite && p.CanRead))
                prop.SetValue(this, prop.GetValue(other));

            foreach (var field in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                field.SetValue(this, field.GetValue(other));
        }
    }
}