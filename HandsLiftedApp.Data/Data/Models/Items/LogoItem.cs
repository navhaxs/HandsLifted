using HandsLiftedApp.Data.Slides;
using System;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("Logo", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class LogoItem : Item
    {
        public LogoItem()
        {
            Title = "Logo";
        }


        [XmlIgnore]
        public override ObservableCollection<Slide> Slides => new ObservableCollection<Slide>() { new LogoSlide() };
    }
}
