using Avalonia.Media;
using AvaloniaVerticalAlignment = Avalonia.Layout.VerticalAlignment;
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
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.LineHeight);
            
            _calculatedTextAlignmentLeft = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Left)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentLeft);
            
            _calculatedTextAlignmentCenter = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Center)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentCenter);
            
            _calculatedTextAlignmentRight = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Right)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentRight);
            
            _calculatedTextAlignmentJustify = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Justify)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentJustify);

            _calculatedTitleTextAlignmentLeft = this.WhenAnyValue(x => x.TitleTextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Left)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTitleTextAlignmentLeft);

            _calculatedTitleTextAlignmentCenter = this.WhenAnyValue(x => x.TitleTextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Center)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTitleTextAlignmentCenter);

            _calculatedTitleTextAlignmentRight = this.WhenAnyValue(x => x.TitleTextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Right)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTitleTextAlignmentRight);

            _calculatedCopyrightTextAlignmentLeft = this.WhenAnyValue(x => x.CopyrightTextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Left)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedCopyrightTextAlignmentLeft);

            _calculatedCopyrightTextAlignmentCenter = this.WhenAnyValue(x => x.CopyrightTextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Center)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedCopyrightTextAlignmentCenter);

            _calculatedCopyrightTextAlignmentRight = this.WhenAnyValue(x => x.CopyrightTextAlignment,
                    (textAlignment) => textAlignment == TextAlignment.Right)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedCopyrightTextAlignmentRight);

            _calculatedTitleVerticalAlignmentTop = this.WhenAnyValue(x => x.TitleVerticalAlignment,
                    (verticalAlignment) => verticalAlignment == AvaloniaVerticalAlignment.Top)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTitleVerticalAlignmentTop);

            _calculatedTitleVerticalAlignmentCenter = this.WhenAnyValue(x => x.TitleVerticalAlignment,
                    (verticalAlignment) => verticalAlignment == AvaloniaVerticalAlignment.Center)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTitleVerticalAlignmentCenter);

            _calculatedTitleVerticalAlignmentBottom = this.WhenAnyValue(x => x.TitleVerticalAlignment,
                    (verticalAlignment) => verticalAlignment == AvaloniaVerticalAlignment.Bottom)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTitleVerticalAlignmentBottom);

            _calculatedTextFontBold = this.WhenAnyValue(x => x.FontWeight,
                (fontWeight) => fontWeight == Avalonia.Media.FontWeight.Bold)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextFontBold);
            
            _calculatedTextFontItalic = this.WhenAnyValue(x => x.FontStyle,
                (fontStyle) => fontStyle == FontStyle.Italic)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextFontItalic);
            
            // _calculatedTextFontUnderline = this.WhenAnyValue(x => x.TextDecorations,
            //     (textDecorations) => textDecorations.Any(decoration => decoration.Location == TextDecorationLocation.Underline))
            //     .ObserveOn(RxSchedulers.MainThreadScheduler)
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

        private TextAlignment _titleTextAlignment = TextAlignment.Center;

        [DataMember]
        public TextAlignment TitleTextAlignment
        {
            get => _titleTextAlignment;
            set => this.RaiseAndSetIfChanged(ref _titleTextAlignment, value);
        }

        private readonly ObservableAsPropertyHelper<bool> _calculatedTitleTextAlignmentLeft;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTitleTextAlignmentCenter;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTitleTextAlignmentRight;

        public bool CalculatedTitleTextAlignmentLeft
        {
            get => _calculatedTitleTextAlignmentLeft.Value;
            set { if (value) TitleTextAlignment = TextAlignment.Left; }
        }

        public bool CalculatedTitleTextAlignmentCenter
        {
            get => _calculatedTitleTextAlignmentCenter.Value;
            set { if (value) TitleTextAlignment = TextAlignment.Center; }
        }

        public bool CalculatedTitleTextAlignmentRight
        {
            get => _calculatedTitleTextAlignmentRight.Value;
            set { if (value) TitleTextAlignment = TextAlignment.Right; }
        }

        private TextAlignment _copyrightTextAlignment = TextAlignment.Center;

        [DataMember]
        public TextAlignment CopyrightTextAlignment
        {
            get => _copyrightTextAlignment;
            set => this.RaiseAndSetIfChanged(ref _copyrightTextAlignment, value);
        }

        private readonly ObservableAsPropertyHelper<bool> _calculatedCopyrightTextAlignmentLeft;
        private readonly ObservableAsPropertyHelper<bool> _calculatedCopyrightTextAlignmentCenter;
        private readonly ObservableAsPropertyHelper<bool> _calculatedCopyrightTextAlignmentRight;

        public bool CalculatedCopyrightTextAlignmentLeft
        {
            get => _calculatedCopyrightTextAlignmentLeft.Value;
            set { if (value) CopyrightTextAlignment = TextAlignment.Left; }
        }

        public bool CalculatedCopyrightTextAlignmentCenter
        {
            get => _calculatedCopyrightTextAlignmentCenter.Value;
            set { if (value) CopyrightTextAlignment = TextAlignment.Center; }
        }

        public bool CalculatedCopyrightTextAlignmentRight
        {
            get => _calculatedCopyrightTextAlignmentRight.Value;
            set { if (value) CopyrightTextAlignment = TextAlignment.Right; }
        }

        private AvaloniaVerticalAlignment _titleVerticalAlignment = AvaloniaVerticalAlignment.Center;

        [DataMember]
        public AvaloniaVerticalAlignment TitleVerticalAlignment
        {
            get => _titleVerticalAlignment;
            set => this.RaiseAndSetIfChanged(ref _titleVerticalAlignment, value);
        }

        private readonly ObservableAsPropertyHelper<bool> _calculatedTitleVerticalAlignmentTop;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTitleVerticalAlignmentCenter;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTitleVerticalAlignmentBottom;

        public bool CalculatedTitleVerticalAlignmentTop
        {
            get => _calculatedTitleVerticalAlignmentTop.Value;
            set { if (value) TitleVerticalAlignment = AvaloniaVerticalAlignment.Top; }
        }

        public bool CalculatedTitleVerticalAlignmentCenter
        {
            get => _calculatedTitleVerticalAlignmentCenter.Value;
            set { if (value) TitleVerticalAlignment = AvaloniaVerticalAlignment.Center; }
        }

        public bool CalculatedTitleVerticalAlignmentBottom
        {
            get => _calculatedTitleVerticalAlignmentBottom.Value;
            set { if (value) TitleVerticalAlignment = AvaloniaVerticalAlignment.Bottom; }
        }

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

        private int _titleFontSize = 100;

        [DataMember]
        public int TitleFontSize
        {
            get => _titleFontSize;
            set => this.RaiseAndSetIfChanged(ref _titleFontSize, value);
        }

        private int _copyrightFontSize = 45;

        [DataMember]
        public int CopyrightFontSize
        {
            get => _copyrightFontSize;
            set => this.RaiseAndSetIfChanged(ref _copyrightFontSize, value);
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

        private bool _autofitEnabled = true;

        [DataMember]
        public bool AutofitEnabled
        {
            get => _autofitEnabled;
            set => this.RaiseAndSetIfChanged(ref _autofitEnabled, value);
        }

        private decimal _autofitMinFontSizeRatio = 0.5M;

        [DataMember]
        public decimal AutofitMinFontSizeRatio
        {
            get => _autofitMinFontSizeRatio;
            set => this.RaiseAndSetIfChanged(ref _autofitMinFontSizeRatio, value);
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