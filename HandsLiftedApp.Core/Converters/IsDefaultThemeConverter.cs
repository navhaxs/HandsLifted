using Avalonia.Data.Converters;
using System;
using System.Globalization;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class IsDefaultThemeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is BaseSlideTheme theme)
                return theme.Id == Globals.Instance.AppPreferences?.DefaultTheme?.Id;
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
