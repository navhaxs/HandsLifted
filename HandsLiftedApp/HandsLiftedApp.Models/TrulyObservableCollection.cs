using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Data
{
    public sealed class TrulyObservableCollection<T> : ObservableCollection<T>
        where T : INotifyPropertyChanged
    {
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
            var index = IndexOf((T)sender);
            var args = new NotifyCollectionChangedEventArgs(
                action: NotifyCollectionChangedAction.Replace,
                newItem: sender,
                oldItem: sender,
                index: index);
            base.OnCollectionChanged(args);
        }
    }
}
