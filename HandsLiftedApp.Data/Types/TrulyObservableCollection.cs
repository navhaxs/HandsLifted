using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HandsLiftedApp.Data
{
    public sealed class TrulyObservableCollection<T> : ObservableCollection<T>
        where T : INotifyPropertyChanged
    {

        public TrulyObservableCollection() : base()
        {
        }

        public TrulyObservableCollection(List<T> x) : base(x)
        {
            // the base constructor copies the items without raising CollectionChanged, so they would
            // otherwise never be hooked and edits to them would go unreported
            foreach (INotifyPropertyChanged inpc in x)
                inpc.PropertyChanged += ItemPropertyChanged;
        }

        public event EventHandler<PropertyChangedEventArgs> CollectionItemChanged;

        protected override void ClearItems()
        {
            // Clear() raises a Reset with no OldItems, so detach explicitly or removed items keep reporting
            foreach (INotifyPropertyChanged inpc in this)
                inpc.PropertyChanged -= ItemPropertyChanged;
            base.ClearItems();
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);
            if (e.NewItems != null)
            {
                foreach (INotifyPropertyChanged inpc in e.NewItems)
                    inpc.PropertyChanged += ItemPropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (INotifyPropertyChanged inpc in e.OldItems)
                    inpc.PropertyChanged -= ItemPropertyChanged;
            }
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
