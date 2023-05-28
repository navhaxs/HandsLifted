using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.Models.ItemState;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace HandsLiftedApp.Converters
{

    // Converts a "ListBoxItem" to its corresponding index within the parent "ListBox"
    // based on https://stackoverflow.com/a/6337483/
    public class ItemToIndexConverter : IValueConverter, IMultiValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ListBoxItem)
            {
                var item = ((ListBoxItem)value);
                var itemsControl = ControlExtension.FindAncestor<ItemsControl>(item);
                int index = itemsControl.IndexFromContainer(item);
                return (index + 1).ToString(); // add 1 for human friendly item position
            }

            // converter used for the wrong type
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values != null && values[0] is Control)
            {
                Control leafControl = (Control)values[0];
                ItemsControl parentItemsControl = ControlExtension.FindAncestor<ItemsControl>(leafControl);
                string ret = (parentItemsControl.Items.IndexOf(values[1]) + 1).ToString();
                Item<ItemStateImpl> z = (Item<ItemStateImpl>)values[1];
                Debug.Print($"{ret} - {z.Title} {parentItemsControl.ItemsView}");
                return ret;
                //Control? containerFromItem = parentItemsControl.ContainerFromItem(values[1]);
                //if (containerFromItem != null)
                //{
                //    int itemIndex = parentItemsControl.IndexFromContainer(containerFromItem);
                //    return (itemIndex + 1).ToString(); // add 1 for human friendly item position
                //}
            }

            // converter used for the wrong type
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
