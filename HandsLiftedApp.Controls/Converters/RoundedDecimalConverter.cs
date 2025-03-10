using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HandsLiftedApp.Controls.Converters
{

    public class RoundedDecimalConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string)
            {
                Decimal.TryParse((string)value, out decimal parsed);
                return Math.Round(parsed, 2);
            }
            if (value is decimal)
            {
                return Math.Round((decimal)value, 2);
            }
            if (value is double)
            {
                return Math.Round((double)value, 2);
            }

            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string)
            {
                Decimal.TryParse((string)value, out decimal parsed);
                return Math.Round(parsed, 2);
            }
            if (value is decimal)
            {
                return Math.Round((decimal)value, 2);
            }
            if (value is double)
            {
                return Math.Round((double)value, 2);
            }
            
            throw new NotSupportedException();
        }
    }
}
