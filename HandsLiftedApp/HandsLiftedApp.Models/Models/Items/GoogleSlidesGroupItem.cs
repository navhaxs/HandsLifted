using ReactiveUI;
using System.Xml.Serialization;


namespace HandsLiftedApp.Data.Models.Items
{
    public class GoogleSlidesGroupItem<I, J> : SlidesGroupItem<I> where I : IItemState where J : IGoogleSlidesGroupItemState
    {
        private J _syncstate;
        [XmlIgnore]
        public J SyncState { get => _syncstate; set => this.RaiseAndSetIfChanged(ref _syncstate, value); }

        public GoogleSlidesGroupItem()
        {
            SyncState = (J)Activator.CreateInstance(typeof(J), this);
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
