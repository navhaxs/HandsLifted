using HandsLiftedApp.Data.Models.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models
{
    internal class ActiveSlideChangedMessage
    {
        public Item<ItemStateImpl> SourceItem { get; set; }
    }
}
