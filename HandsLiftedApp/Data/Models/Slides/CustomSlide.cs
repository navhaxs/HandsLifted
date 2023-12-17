using HandsLiftedApp.Data.Slides;
using System.Xml.Serialization;
using System;

namespace HandsLiftedApp.Data.Models.Slides
{
    [XmlRoot(Namespace = Constants.Namespace)]
    [Serializable]
    public class CustomSlide : Slide
    {
        public override string? SlideLabel => "abc";

        public override string? SlideText => "123";
    }
}
