using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.ItemState;

namespace HandsLiftedApp.Models
{
    internal class ActiveSlideChangedMessage
    {
        public Item<ItemStateImpl> SourceItem { get; set; }
    }
}
