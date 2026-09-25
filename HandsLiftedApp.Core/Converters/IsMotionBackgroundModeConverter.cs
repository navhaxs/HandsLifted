// HandsLiftedApp.Core/Converters/IsMotionBackgroundModeConverter.cs
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class IsMotionBackgroundModeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is ThemeBackgroundMode mode && mode == ThemeBackgroundMode.MotionBackground;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
