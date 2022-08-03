using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Models
{
    internal class ActiveSlideChangedMessage
    {
        public Item<ItemStateImpl> SourceItem { get; set; }
    }
}
