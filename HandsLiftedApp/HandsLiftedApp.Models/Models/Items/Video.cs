using HandsLiftedApp.Data.Slides;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Data.Models.Items
{
    public class SlidesGroup : Item
    {
        public ObservableCollection<Slide> _Slides { get; set; } = new ObservableCollection<Slide>();
        public override ObservableCollection<Slide> Slides => _Slides;
    }
}
