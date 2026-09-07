using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Data;

namespace HandsLiftedApp.Core.Models
{
    public sealed class PlaylistItemInstanceCollection<T> : ObservableCollection<T>
        where T : INotifyPropertyChanged
    {

        public PlaylistItemInstanceCollection() : base()
        {
        }

        public PlaylistItemInstanceCollection(List<T> x) : base(x)
        {
            foreach (var item in x)
            {
                RegisterItem(item);
            }
        }

        private void RegisterItem(T item)
        {
            item.PropertyChanged += HandleDataFieldPropertyChanges;
            
            if (item is IItemDirtyBit i)
            {
                i.ItemDataModified += OnIOnItemDataModified;
            }
        }

        private void UnregisterItem(T item)
        {
            item.PropertyChanged -= HandleDataFieldPropertyChanges;
            if (item is IItemDirtyBit i)
            {
                i.ItemDataModified -= OnIOnItemDataModified;
            }

            (item as IDisposable)?.Dispose();
        }

        private void OnIOnItemDataModified(object? sender, EventArgs args)
        {
            ItemDataModified?.Invoke(this, EventArgs.Empty);
        }

        private void HandleDataFieldPropertyChanges(object? sender, PropertyChangedEventArgs args)
        {
            var properties = sender?.GetType()
                .GetProperties()
                .Where(prop => prop.IsDefined(typeof(DataField), false));

            if (properties != null && properties.Any(property => property.Name == args.PropertyName))
            {
                ItemDataModified?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler ItemDataModified;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);

            // A Move (reorder, e.g. "Move Up"/"Move Down" or drag-reorder) raises NewItems and
            // OldItems containing the SAME item instance - it never left the collection, it just
            // changed position. Processing it through RegisterItem/UnregisterItem below would be
            // harmless on its own (one subscription added, one equivalent one removed - net
            // no-op), except UnregisterItem now also calls Dispose() on IDisposable items (e.g.
            // SongItemInstance), which would permanently tear down that item's subscriptions while
            // it's still live in the playlist. Skip register/unregister entirely for a pure Move.
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                return;
            }

            if (e.NewItems != null)
            {
                foreach (T inpc in e.NewItems)
                {
                    RegisterItem(inpc);
                }
            }
            if (e.OldItems != null)
            {
                foreach (T item in e.OldItems)
                {
                    UnregisterItem(item);
                }
            }
        }
    }
}
