using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HandsLiftedApp.Converters
{

    public class MultiplicationConverter : IMultiValueConverter
    {
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        object? IMultiValueConverter.Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            return (int)values.ToArray<object>().Aggregate(1.0, (a, b) => TryParseValue(a) * TryParseValue(b)) * 0.01;
        }

        double TryParseValue(object value)
        {
            if (value is int)
            {
                return (int)value;
            }
            else if (value is double)
            {
                return (double)value;
            }
            else if (value is string && double.TryParse((string)value, out double n))
            {
                return n;
            }
            return 1.0; // ignore
        }
    }
}
