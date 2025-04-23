using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Mime;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;
using Avalonia.Media;
using HandsLiftedApp.Data.Data.Models.Types;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Data.Data.Models.Slides
{
    [XmlRoot(Namespace = Constants.Namespace)]
    [XmlInclude(typeof(TextElement))]

    public class CustomSlide : Slide
    {
        // TODO not really needed
        public override string? SlideLabel { get; } = "";
        public override string? SlideText { get; } = "";
        
        // Slide dimensions
        
        [DataMember]
        public int SlideHeight { get; set; } = 1080;
        public int SlideWidth { get; set; } = 1920;
        
        // Elements
        private ObservableCollection<SlideElement> _slideElements = new();
        public ObservableCollection<SlideElement> SlideElements
        {
            get => _slideElements;
            set => this.RaiseAndSetIfChanged(ref _slideElements, value);
        }
        
        // TODO - gradient
        [DataMember]
        public XmlColor BackgroundColour = Color.Parse("#4d347f");

        [XmlIgnore]
        public Color BackgroundAvaloniaColour
        {
            get => BackgroundColour;
            set => this.RaiseAndSetIfChanged(ref BackgroundColour, value);
        }
        
        private string? _backgroundGraphicFilePath;

        [DataMember]
        public string? BackgroundGraphicFilePath
        {
            get => _backgroundGraphicFilePath;
            set => this.RaiseAndSetIfChanged(ref _backgroundGraphicFilePath, value);
        }
        
        public override string? ToString()
        {
            var stringBuilder = new StringBuilder();
            
            stringBuilder.AppendLine($"CustomSlide SlideLabel={SlideLabel}");

            foreach (var VARIABLE in SlideElements)
            {
                if (VARIABLE is TextElement textElement)
                {
                    stringBuilder.AppendLine($"TextElement Text={textElement.Text}");
                }
            }

            return stringBuilder.ToString();
        }
    }
}