using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace HandsLiftedApp.Core.Converters
{
    // Prepends a null entry to a theme list so a ComboBox can offer a blank
    // selection that resolves back to the fallback theme (Design = Guid.Empty).
    public class PrependBlankThemeOptionConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is IEnumerable items)
            {
                var result = new List<object?> { null };
                result.AddRange(items.Cast<object?>());
                return result;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
