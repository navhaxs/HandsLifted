using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HandsLiftedApp.Converters
{

    public class IntMinusOneConverter : IValueConverter
    {
        // int minus 1
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int)
            {
                return (int)value - 1;
            }
            else if (value is string && int.TryParse((string)value, out int n))
            {
                return n - 1;
            }

            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
