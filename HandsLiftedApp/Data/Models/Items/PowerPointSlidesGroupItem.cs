using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("PowerPoint", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class PowerPointSlidesGroupItem<I, J, K> : SlidesGroupItem<I, J> where I : IItemState where J : IItemAutoAdvanceTimerState where K : IPowerPointSlidesGroupItemState
    {
        private K _syncstate;
        [XmlIgnore]
        public K SyncState { get => _syncstate; set => this.RaiseAndSetIfChanged(ref _syncstate, value); }

        public PowerPointSlidesGroupItem()
        {
            SyncState = (K)Activator.CreateInstance(typeof(K), this);
        }

        private string _sourcePresentationFile;

        public string SourcePresentationFile { get => _sourcePresentationFile; set => this.RaiseAndSetIfChanged(ref _sourcePresentationFile, value); }

        private string _sourceSlidesExportDirectory;

        public string SourceSlidesExportDirectory { get => _sourceSlidesExportDirectory; set => this.RaiseAndSetIfChanged(ref _sourceSlidesExportDirectory, value); }

        // <"PowerPoint Slide ID", exported slide image filename> in order of slide index 
        private Dictionary<string, string> _slideIdMap = new Dictionary<string, string>();
        // TODO make this serializable
        [XmlIgnore]
        public Dictionary<string, string> SlideIdMap { get => _slideIdMap; set => this.RaiseAndSetIfChanged(ref _slideIdMap, value); }

    }
    public interface IPowerPointSlidesGroupItemState
    {
    }
}
