// HandsLiftedApp.Core/Converters/IsMotionBackgroundThemeConverter.cs
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    // Converts a (possibly null) BaseSlideTheme - e.g. SongItemInstance.ResolvedDesignTheme - to
    // whether it is a motion-background theme. Used to hide the per-item video override button
    // entirely when no motion-background theme is assigned, rather than showing it disabled.
    public class IsMotionBackgroundThemeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is BaseSlideTheme theme && theme.BackgroundMode == ThemeBackgroundMode.MotionBackground;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
