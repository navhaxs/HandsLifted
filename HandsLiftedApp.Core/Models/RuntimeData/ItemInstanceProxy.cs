using System;
using System.Linq;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Core.Models.RuntimeData
{
    // TODO could actually be moved into
    public class ItemInstanceProxy : ReactiveObject
    {
        public event EventHandler? ItemDataModified; // pass through from inner Item

        public ItemInstanceProxy(Item item)
        {
            Item = item;

            Item.PropertyChanged += (sender, args) =>
            {
                var properties = sender?.GetType().GetProperties()
                    .Where(prop => prop.IsDefined(typeof(DataField), false));
                
                if (properties != null && properties.Any(property => property.Name == args.PropertyName))
                {
                    ItemDataModified?.Invoke(this, EventArgs.Empty);
                }
            };
            
            if (Item is IItemDirtyBit i)
            {
                i.ItemDataModified += (sender, args) =>
                {
                    ItemDataModified?.Invoke(this, EventArgs.Empty);
                };
            }
        }

        public Item Item { get; init; }
    }
}