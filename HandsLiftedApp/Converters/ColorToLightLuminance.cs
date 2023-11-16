using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace HandsLiftedApp.Converters
{

    public class ColorToLightLuminance : IValueConverter
    {
        public double S { get; set; } = 0.4;
        public double L { get; set; } = 0.8;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                HslColor hslColor = color.ToHsl();
                HslColor hslColor1 = new HslColor(1, hslColor.H, S, L);
                Color rgb = hslColor1.ToRgb();
                return new Color(rgb.A, rgb.R, rgb.G, rgb.B);
            }
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
