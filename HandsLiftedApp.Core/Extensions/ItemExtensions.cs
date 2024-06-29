using System.Linq;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Extensions
{
    public static class ItemExtensions
    {
        public static ItemInstanceProxy GenerateBaseItemInstance(this Item item)
        {
            return new ItemInstanceProxy(item);
        }

        public static int IndexOf(this PlaylistItemInstanceCollection<ItemInstanceProxy> items, Item itemToMatch)
        {
            var match = items.Select((item, index) => new { Item = item, Index = index })
                .FirstOrDefault(i => i.Item.Item == itemToMatch);
            if (match != null)
            {
                return match.Index;
            }

            return -1;
        }
    }
}