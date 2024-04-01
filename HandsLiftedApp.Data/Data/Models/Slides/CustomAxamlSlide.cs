using HandsLiftedApp.Data.Slides;
using System.Xml.Serialization;
using System;
using ReactiveUI;

namespace HandsLiftedApp.Data.Models.Slides
{
    [XmlRoot(Namespace = Constants.Namespace)]
    [Serializable]
    public class CustomAxamlSlide : MediaSlide
    {
        public override string? SlideLabel => "abc";

        public override string? SlideText => "123";

        private string _text = "";
        public string Text
        {
            get => _text;
            set => this.RaiseAndSetIfChanged(ref _text, value);
        }
    }
}
