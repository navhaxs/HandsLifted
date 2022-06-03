using HandsLiftedApp.Data.Slides;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Data.Models.Items
{
    class TestItem : Item
    {

        public List<Slide> _slides { get; set; } = new List<Slide>();
        public override IEnumerable<Slide> Slides => _slides;
    }
}
