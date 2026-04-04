using System;
using System.IO;
using System.Xml.Serialization;
using ReactiveUI;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("Item", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public abstract class Item : ReactiveObject
    {
        [XmlIgnore]
        public Guid UUID { get; set; }

        private int _index;
        [XmlIgnore]
        public int Index { get => _index; set => this.RaiseAndSetIfChanged(ref _index, value); }
        
        protected Item()
        {
            UUID = Guid.NewGuid();
        }

        private string _title = "";
        [DataField]
        public string Title
        {
            get => _title; set
            {
                this.RaiseAndSetIfChanged(ref _title, value);
                this.RaisePropertyChanged(nameof(Slides));
            }
        }

        public virtual Item Clone()
        {
            XmlSerializer serializer = new XmlSerializer(this.GetType());
            using (MemoryStream ms = new MemoryStream())
            {
                serializer.Serialize(ms, this);
                ms.Position = 0;
                var item = (Item)serializer.Deserialize(ms);
                item.UUID = Guid.NewGuid();
                return item;
            }
        }
        //
        // [XmlIgnore]
        // public abstract ObservableCollection<Slide?> Slides { get; }

    }

}
