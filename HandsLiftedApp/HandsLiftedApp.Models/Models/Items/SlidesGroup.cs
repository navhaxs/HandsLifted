using HandsLiftedApp.Data.Slides;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    // TODO: need to define list of media, rather than Slide ??? for serialization
    public class SlidesGroup : Item
    {
        [XmlIgnore]
        public ObservableCollection<Slide> _Slides { get; set; } = new ObservableCollection<Slide>();
        [XmlIgnore]
        public override ObservableCollection<Slide> Slides => _Slides;
    }
}
