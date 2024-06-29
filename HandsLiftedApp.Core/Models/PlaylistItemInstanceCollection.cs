using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using HandsLiftedApp.Core.Models.RuntimeData;

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
                item.PropertyChanged += ItemPropertyChanged;
                if (item is ItemInstanceProxy itemInstanceProxy)
                {
                    itemInstanceProxy.ItemDataModified += (itemInstanceProxyOnItemDataModified);
                }
            }
        }

        public event EventHandler<PropertyChangedEventArgs> CollectionItemChanged;
        
        public event EventHandler ItemDataModified;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);
            if (e.NewItems != null)
            {
                foreach (INotifyPropertyChanged inpc in e.NewItems)
                {
                    inpc.PropertyChanged += ItemPropertyChanged;
                    if (inpc is ItemInstanceProxy itemInstanceProxy)
                    {
                        itemInstanceProxy.ItemDataModified += (itemInstanceProxyOnItemDataModified);
                    }
                }
            }
            if (e.OldItems != null)
            {
                foreach (INotifyPropertyChanged inpc in e.OldItems)
                {
                    inpc.PropertyChanged -= ItemPropertyChanged;
                    if (inpc is ItemInstanceProxy itemInstanceProxy)
                    {
                        itemInstanceProxy.ItemDataModified -= (itemInstanceProxyOnItemDataModified);
                    }
                }
            }
        }

        private void itemInstanceProxyOnItemDataModified(object? sender, EventArgs args)
        {
            ItemDataModified?.Invoke(this, EventArgs.Empty);
        }

        private void ItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            CollectionItemChanged?.Invoke(this, e);
            //rather than marking entire collection as 'changed' which AvaloniaUI ListBox default behaviour does undesired things...
            //var index = IndexOf((T)sender);
            //var args = new NotifyCollectionChangedEventArgs(
            //    action: NotifyCollectionChangedAction.Replace,
            //    newItem: sender,
            //    oldItem: sender,
            //    index: index);
            //base.OnCollectionChanged(args);
        }
    }
}
