using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    // TODO: need to define list of media, rather than Slide ??? for serialization
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

    }
    public interface IPowerPointSlidesGroupItemState
    {
    }
}
