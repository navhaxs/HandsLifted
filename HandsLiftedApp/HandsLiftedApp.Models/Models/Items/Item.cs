using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    public abstract class Item : ReactiveObject {
        [XmlIgnore]

        public ItemState? State { get; set; }

        public abstract ObservableCollection<Slide> Slides { get; }
    }
}
