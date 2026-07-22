using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HandsLiftedApp.Converters
{
    // value = percentage (e.g. from SlideThumbnailSizeMultiplier), parameter = base size to scale
    public class PercentageScaleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
            {
                return value;
            }

            double percentage = System.Convert.ToDouble(value, culture);
            double baseSize = System.Convert.ToDouble(parameter, culture);
            return (int)(percentage * baseSize * 0.01);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
