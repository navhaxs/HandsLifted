using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace HandsLiftedApp.Converters
{

    public class ColorToConstrastingBrush : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                HslColor hslColor = color.ToHsl();
                if (hslColor.L > 0.5)
                {
                    return ColorToBrushConverter.Convert(Color.Parse("Black"), typeof(IBrush));
                }
                return ColorToBrushConverter.Convert(Color.Parse("White"), typeof(IBrush));
            }
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
