using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace HandsLiftedApp.Controls
{
    public class ListBoxWithoutKey : ListBox
    {

        //protected override void ItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        //{
        //    base.ItemsCollectionChanged(sender, e);

        //    // required to ensure no duplicate selected items after a 'move'
        //    if (e.Action == NotifyCollectionChangedAction.Move)
        //    {
        //        for (int i = 0; i < this.ItemCount; i++) {
        //            var listBoxItemContainer = this.ContainerFromIndex(i);
        //            bool isSelected = i == this.SelectedIndex;
        //            if (listBoxItemContainer is ListBoxItem lbi)
        //            {
        //                lbi.IsSelected = isSelected;
        //            }
        //        }
        //    }
        //}

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // do nothing instead of the default behaviour!
        }


        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.Source is Visual source)
            {
                var point = e.GetCurrentPoint(source);

                if (point.Properties.IsLeftButtonPressed) //  || point.Properties.IsRightButtonPressed)
                {
                    base.OnPointerPressed(e);
                    // TODO
                    //e.Handled = UpdateSelectionFromEventSource(
                    //    e.Source,
                    //    true,
                    //    e.KeyModifiers.HasAllFlags(KeyModifiers.Shift),
                    //    e.KeyModifiers.HasAllFlags(AvaloniaLocator.Current.GetRequiredService<PlatformHotkeyConfiguration>().CommandModifiers),
                    //    point.Properties.IsRightButtonPressed);
                }
            }
        }

    }
}
