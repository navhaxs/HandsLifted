using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("PDF", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class PDFSlidesGroupItem<I, J> : SlidesGroupItem<I, J> where I : IItemState where J : IItemAutoAdvanceTimerState
    {
        public PDFSlidesGroupItem()
        {
        }

        private string _sourcePresentationFile;

        public string SourcePresentationFile { get => _sourcePresentationFile; set => this.RaiseAndSetIfChanged(ref _sourcePresentationFile, value); }

        private string _sourceSlidesExportDirectory;

        public string SourceSlidesExportDirectory { get => _sourceSlidesExportDirectory; set => this.RaiseAndSetIfChanged(ref _sourceSlidesExportDirectory, value); }

        // does PDF have an equivalent?
        private Dictionary<string, string> _slideIdMap = new Dictionary<string, string>();
        // TODO make this serializable
        [XmlIgnore]
        public Dictionary<string, string> SlideIdMap { get => _slideIdMap; set => this.RaiseAndSetIfChanged(ref _slideIdMap, value); }

    }
}
