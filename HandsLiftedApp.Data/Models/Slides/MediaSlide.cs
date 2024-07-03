using ReactiveUI;
using System;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Slides
{
    [XmlRoot(Namespace = Constants.Namespace)]
    [Serializable]
    public abstract class MediaSlide : Slide
    {
        public string SourceMediaFilePath { get => _sourceMediaFilePath; set => this.RaiseAndSetIfChanged(ref _sourceMediaFilePath, value); }
        private string _sourceMediaFilePath;

        public MediaSlide()
        {

        }
    }
}