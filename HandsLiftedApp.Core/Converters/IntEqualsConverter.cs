using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HandsLiftedApp.Core.Converters
{
    public class IntEqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter is string paramString && int.TryParse(paramString, out var paramValue))
                return intValue == paramValue;
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
