using ReactiveUI;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    public class GoogleSlidesGroupItem<I, J, K> : SlidesGroupItem<I, J> where I : IItemState where J : IItemAutoAdvanceTimerState where K : IGoogleSlidesGroupItemState
    {
        private K _syncstate;
        [XmlIgnore]
        public K SyncState { get => _syncstate; set => this.RaiseAndSetIfChanged(ref _syncstate, value); }

        public GoogleSlidesGroupItem()
        {
            SyncState = (K)Activator.CreateInstance(typeof(K), this);
        }

        private string _sourceGooglePresentationId;

        public string SourceGooglePresentationId { get => _sourceGooglePresentationId; set => this.RaiseAndSetIfChanged(ref _sourceGooglePresentationId, value); }



        private string _sourceSlidesExportDirectory;
        public string SourceSlidesExportDirectory { get => _sourceSlidesExportDirectory; set => this.RaiseAndSetIfChanged(ref _sourceSlidesExportDirectory, value); }

    }
    public interface IGoogleSlidesGroupItemState
    {
    }
}
