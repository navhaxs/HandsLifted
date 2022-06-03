using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    public abstract class Item : ReactiveObject {
        [XmlIgnore]
        public abstract IEnumerable<Slide> Slides { get; } //=> _slides;
    }
}
