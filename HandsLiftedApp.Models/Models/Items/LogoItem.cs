using HandsLiftedApp.Data.Slides;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("Logo", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class LogoItem<I> : Item<I> where I : IItemState
    {
        public LogoItem() {
            Title = "(Logo)";
        }


        public override ObservableCollection<Slide> Slides => new ObservableCollection<Slide>() { new LogoSlide() };
    }
}
