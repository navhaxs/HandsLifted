using Avalonia.Animation;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("Item", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public abstract class Item : ReactiveObject
    {
        [XmlIgnore]
        public Guid UUID { get; set; }

        protected Item()
        {
            UUID = Guid.NewGuid();
        }

        private string _title = "New Item";
        public string Title
        {
            get => _title; set
            {
                this.RaiseAndSetIfChanged(ref _title, value);
                this.RaisePropertyChanged(nameof(Slides));
            }
        }
        //
        // [XmlIgnore]
        // public abstract ObservableCollection<Slide?> Slides { get; }

    }

}
