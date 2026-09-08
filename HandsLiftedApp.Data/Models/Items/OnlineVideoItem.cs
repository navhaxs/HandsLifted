using ReactiveUI;
using System;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("OnlineVideo", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class OnlineVideoItem : MediaGroupItem
    {
        public OnlineVideoItem()
        {
        }

        private string _sourceVideoUrl;

        public string SourceVideoUrl { get => _sourceVideoUrl; set => this.RaiseAndSetIfChanged(ref _sourceVideoUrl, value); }
    }
}
