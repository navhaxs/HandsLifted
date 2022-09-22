using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using System.Collections.Specialized;
using System.Diagnostics;

namespace HandsLiftedApp.Controls
{
    public class ListBoxWithoutKey : ListBox
    {

        /// <summary>
        /// Constructor a new LoggingListBox.
        /// </summary>
        public ListBoxWithoutKey()
        {

            //this.ItemsCollectionChanged;

            //SubscribeToAutoScroll_ItemsCollectionChanged(
            //    this);
        }

        protected override void ItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            base.ItemsCollectionChanged(sender, e);

            //if (AlwaysSelected && SelectedIndex == -1 && ItemCount > 0)
            //{
            //    SelectedIndex = 0;
            //}
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                Debug.Print("something moved");
                foreach (var c in this.ItemContainerGenerator.Containers) {
                    bool isSelected = c.Index == this.SelectedIndex;
                    if (c.ContainerControl is ListBoxItem lbi)
                    {
                        lbi.IsSelected = isSelected;
                    }
                }
            }
        }

        ///// <summary>
        ///// Subscribes to the list items' collection changed event if AutoScroll is enabled.
        ///// Otherwise, it unsubscribes from that event.
        ///// For this to work, the underlying list must implement INotifyCollectionChanged.
        /////
        ///// (This function was only creative for brevity)
        ///// </summary>
        ///// <param name="listBox">The list box containing the items collection.</param>
        ///// <param name="subscribe">Subscribe to the collection changed event?</param>
        //private static void SubscribeToAutoScroll_ItemsCollectionChanged(
        //    ListBoxWithoutKey listBox)
        //{
        //    AvaloniaList<object> notifyCollection = listBox.Items as AvaloniaList<object>;
        //    if (notifyCollection != null)
        //    {
        //        notifyCollection.CollectionChanged += NotifyCollection_CollectionChanged;
        //    }
        //}

        //private static void NotifyCollection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        //{
        //    Debug.Print("hello");
        //}

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // do nothing instead of the default behaviour!
        }

    }
}
