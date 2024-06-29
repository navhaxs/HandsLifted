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
