using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using System.Collections.Specialized;

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
                foreach (var c in this.ItemContainerGenerator.Containers)
                {
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


        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.Source is IVisual source)
            {
                var point = e.GetCurrentPoint(source);

                if (point.Properties.IsLeftButtonPressed) //  || point.Properties.IsRightButtonPressed)
                {
                    base.OnPointerPressed(e);
                    e.Handled = UpdateSelectionFromEventSource(
                        e.Source,
                        true,
                        e.KeyModifiers.HasAllFlags(KeyModifiers.Shift),
                        e.KeyModifiers.HasAllFlags(AvaloniaLocator.Current.GetRequiredService<PlatformHotkeyConfiguration>().CommandModifiers),
                        point.Properties.IsRightButtonPressed);
                }
            }
        }

    }
}
