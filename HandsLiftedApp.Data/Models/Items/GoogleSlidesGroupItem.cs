using ReactiveUI;
using System;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("GoogleSlides", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class GoogleSlidesGroupItem : SlidesGroupItem
    {
        public GoogleSlidesGroupItem()
        {
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
