using ReactiveUI;
using System;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Slides
{
    [XmlRoot(Namespace = Constants.Namespace)]
    [Serializable]
    public abstract class MediaSlide : Slide
    {
        public string SourceMediaPath { get => _sourceMediaPath; set => this.RaiseAndSetIfChanged(ref _sourceMediaPath, value); }
        private string _sourceMediaPath;

        public MediaSlide()
        {

        }
    }


}
