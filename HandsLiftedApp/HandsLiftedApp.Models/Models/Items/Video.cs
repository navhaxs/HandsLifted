using HandsLiftedApp.Data.Slides;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Data.Models.Items
{
    public class SlidesGroup : Item
    {
        public List<Slide> _Slides { get; set; } = new List<Slide>();
        public override IEnumerable<Slide> Slides => _Slides;
    }
}
