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
    /**
     * Represents a null item
     */
    [XmlRoot("Blank", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class BlankItem<I> : Item<I> where I : IItemState
    {
        public BlankItem() {
            Title = "(Blank)";
        }


        public override ObservableCollection<Slide> Slides => new ObservableCollection<Slide>() { new LogoSlide() };
    }
}
