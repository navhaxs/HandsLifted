using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Avalonia.Media;
using HandsLiftedApp.Data.Data.Models.Types;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Data.Data.Models.Slides
{
    public class CustomSlide : Slide
    {
        // TODO not really needed
        public override string? SlideLabel { get; } = "";
        public override string? SlideText { get; } = "";
        
        // Elements
        public List<SlideElement> SlideElements = new();
        
        // TODO - gradient
        [DataMember]
        public XmlColor BackgroundColour = Color.Parse("#4d347f");

        [XmlIgnore]
        public Color BackgroundAvaloniaColour
        {
            get => BackgroundColour;
            set => this.RaiseAndSetIfChanged(ref BackgroundColour, value);
        }

    }
}