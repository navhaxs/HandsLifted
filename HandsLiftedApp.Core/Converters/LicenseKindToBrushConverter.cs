using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HandsLiftedApp.Core.Converters
{
    public class LicenseKindToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var license = value as string ?? string.Empty;

            if (license.StartsWith("GPL", StringComparison.OrdinalIgnoreCase)
                || license.StartsWith("LGPL", StringComparison.OrdinalIgnoreCase)
                || license.StartsWith("CC BY-SA", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(Color.Parse("#d97706")); // amber — copyleft

            if (license.Equals("Proprietary", StringComparison.OrdinalIgnoreCase)
                || license.Equals("Commercial", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(Color.Parse("#dc2626")); // red — proprietary

            return new SolidColorBrush(Color.Parse("#6b7280")); // muted gray — permissive
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
