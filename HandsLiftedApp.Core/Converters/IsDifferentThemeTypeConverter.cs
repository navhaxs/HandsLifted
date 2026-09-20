using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    // Used to disable a "move to group" menu item when the bound Type already matches the
    // item's target group (ConverterParameter) - moving a design to its own group is a no-op.
    public class IsDifferentThemeTypeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SlideThemeType type && parameter is string targetName &&
                Enum.TryParse<SlideThemeType>(targetName, out var target))
            {
                return type != target;
            }

            return true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
