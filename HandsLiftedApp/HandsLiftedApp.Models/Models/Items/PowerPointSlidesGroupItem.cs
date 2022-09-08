using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    public class PowerPointSlidesGroupItem<I, J> : SlidesGroupItem<I> where I : IItemState where J : IPowerPointSlidesGroupItemState
    {
        private J _syncstate;
        [XmlIgnore]
        public J SyncState { get => _syncstate; set => this.RaiseAndSetIfChanged(ref _syncstate, value); }

        public PowerPointSlidesGroupItem()
        {
            SyncState = (J)Activator.CreateInstance(typeof(J), this);
        }

        private string _sourcePresentationFile;

        public string SourcePresentationFile { get => _sourcePresentationFile; set => this.RaiseAndSetIfChanged(ref _sourcePresentationFile, value); }

        private string _sourceSlidesExportDirectory;

        public string SourceSlidesExportDirectory { get => _sourceSlidesExportDirectory; set => this.RaiseAndSetIfChanged(ref _sourceSlidesExportDirectory, value); }

        // <"PowerPoint Slide ID", exported slide image filename> in order of slide index 
        private Dictionary<string, string> _slideIdMap = new Dictionary<string, string>();
        public Dictionary<string, string> SlideIdMap { get => _slideIdMap; set => this.RaiseAndSetIfChanged(ref _slideIdMap, value); }

    }
    public interface IPowerPointSlidesGroupItemState
    {
    }
}
