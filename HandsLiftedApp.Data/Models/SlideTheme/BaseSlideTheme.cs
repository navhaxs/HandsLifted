using Avalonia.Media;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
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
                    (fontSize, lineHeightEm) => { return (int)(fontSize * lineHeightEm); })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.LineHeight);
            
            _calculatedTextAlignmentLeft = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => { return textAlignment == TextAlignment.Left; })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentLeft);
            
            _calculatedTextAlignmentCentre = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => { return textAlignment == TextAlignment.Center; })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentCentre);
            
            _calculatedTextAlignmentRight = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => { return textAlignment == TextAlignment.Right; })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentRight);
            
            _calculatedTextAlignmentJustify = this.WhenAnyValue(x => x.TextAlignment,
                    (textAlignment) => { return textAlignment == TextAlignment.Justify; })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CalculatedTextAlignmentJustify);
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
        [DataMember] public XmlFontFamily FontFamily = new();

        [XmlIgnore]
        public FontFamily FontFamilyAsAvalonia
        {
            get => FontFamily;
            set => this.RaiseAndSetIfChanged(ref FontFamily, value);
        }

        private XmlFontWeight _fontWeight = (XmlFontWeight)Avalonia.Media.FontWeight.Normal;

        [DataMember]
        public XmlFontWeight FontWeight
        {
            get => _fontWeight;
            set => this.RaiseAndSetIfChanged(ref _fontWeight, value);
        }

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
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextAlignmentCentre;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextAlignmentRight;
        private readonly ObservableAsPropertyHelper<bool> _calculatedTextAlignmentJustify;
        
        public bool CalculatedTextAlignmentLeft
        {
            get => _calculatedTextAlignmentLeft.Value;
            set { if (value) TextAlignment = TextAlignment.Left; }
        }

        public bool CalculatedTextAlignmentCentre
        {
            get => _calculatedTextAlignmentCentre.Value;
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

        [DataMember] public XmlColor BackgroundColour = Color.Parse("#4d347f");

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

        private decimal _lineHeightEm = 1.0M;

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

        // StrokeColour
        // StrokeThickness

        // DropShadowColour
        // DropShadowRadius

        // [XmlAttribute]

        // TODO - KV map for additional properties
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public void CopyFrom(BaseSlideTheme other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            var properties = GetType().GetProperties()
                .Where(p => p.CanWrite && p.CanRead);

            foreach (var prop in properties)
            {
                var value = prop.GetValue(other);
                prop.SetValue(this, value);
            }
        }
    }
}