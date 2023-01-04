using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HandsLiftedApp.Converters
{

    // Converts a "ListBoxItem" to its corresponding index within the parent "ListBox"
    // based on https://stackoverflow.com/a/6337483/
    public class ItemToIndexConverter : IMultiValueConverter
    {
        public static readonly ItemToIndexConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ListBoxItem)
            {
                var item = ((ListBoxItem)value);
                var itemsControl = IControlExtension.FindAncestor<ItemsControl>(item);
                int index = itemsControl.ItemContainerGenerator.IndexFromContainer(item);
                return (index + 1).ToString(); // add 1 for human friendly item position
            }

            // converter used for the wrong type
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values[0] is ListBoxItem)
            {
                var item = ((ListBoxItem)values[0]);
                var itemsControl = IControlExtension.FindAncestor<ItemsControl>(item);
                if (itemsControl != null)
                {
                    int index;

                    //if (values.Count == 3 && values[2] != null)
                    //{
                    //    return values[2];
                    //}


                    if (values[1] is TrulyObservableCollection<Slide> && item.DataContext is Slide)
                    {
                        index = ((TrulyObservableCollection<Slide>)values[1]).IndexOf((Slide)item.DataContext);
                    }
                    else
                    {
                        index = itemsControl.ItemContainerGenerator.IndexFromContainer(item);
                    }

                    return (index + 1).ToString(); // add 1 for human friendly item position
                }
                else
                {
                    return "#";
                }
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
