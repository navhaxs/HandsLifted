using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models
{
    [XmlRoot("Playlist", Namespace = Constants.Namespace, IsNullable = false)]
    // todo: see https://stackoverflow.com/questions/11886290/use-the-xmlinclude-or-soapinclude-attribute-to-specify-types-that-are-not-known
    [XmlInclude(typeof(SongItem))]
    [XmlInclude(typeof(SlidesGroup))]
    [Serializable]
    public class Playlist : ReactiveObject
    {
        public SerializableDictionary<String, String> Meta { get; set; } = new SerializableDictionary<String, String>();
        private ObservableCollection<Item> _items = new ObservableCollection<Item>();
        public ObservableCollection<Item> Items { get => _items; set => this.RaiseAndSetIfChanged(ref _items, value); }
    }
}
