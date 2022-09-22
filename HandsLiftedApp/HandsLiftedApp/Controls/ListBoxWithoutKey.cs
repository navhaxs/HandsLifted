using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using System.Collections.Specialized;
using System.Diagnostics;

namespace HandsLiftedApp.Controls
{
    public class ListBoxWithoutKey : ListBox
    {

        protected override void ItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            base.ItemsCollectionChanged(sender, e);

            // required to ensure no duplicate selected items after a 'move'
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                foreach (var c in this.ItemContainerGenerator.Containers) {
                    bool isSelected = c.Index == this.SelectedIndex;
                    if (c.ContainerControl is ListBoxItem lbi)
                    {
                        lbi.IsSelected = isSelected;
                    }
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // do nothing instead of the default behaviour!
        }

    }
}
