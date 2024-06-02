using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HandsLiftedApp.Controls.Converters
{
    public class SafeFontWeightConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is FontWeight fw)
            {
                if ((int)fw == 0)
                {
                    return FontWeight.Normal;
                }
                return value;
            }
            if (value is int)
            {
                return Math.Max(1, (int)value);
            }
            return FontWeight.Normal;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}