using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Documents
{
    public abstract class SlidesDocument : ReactiveObject {
        [XmlIgnore]
        public abstract IEnumerable<Slide> Slides { get; } //=> _slides;
    }
}
