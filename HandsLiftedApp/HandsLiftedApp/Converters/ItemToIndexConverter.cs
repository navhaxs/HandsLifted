using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HandsLiftedApp.Converters
{

    // Converts a "ListBoxItem" to its corresponding index within the parent "ListBox"
    // based on https://stackoverflow.com/a/6337483/
    public class ItemToIndexConverter : IValueConverter
    {
        public static readonly ItemToIndexConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ListBoxItem)
            {
                var item = ((ListBoxItem)value);
                var itemsControl = ItemContainerToZIndexConverterExtensions.FindAncestor<ItemsControl>(item);
                int index = itemsControl.ItemContainerGenerator.IndexFromContainer(item);
                return (index + 1).ToString(); // add 1 for human friendly item position
            }

            // converter used for the wrong type
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public static class ItemContainerToZIndexConverterExtensions
    {
        public static T FindAncestor<T>(this IControl obj) where T : IControl
        {
            var tmp = obj.Parent;
            while (tmp != null && !(tmp is T))
            {
                tmp = tmp.Parent;
            }
            return (T)tmp;
        }
    }
}
