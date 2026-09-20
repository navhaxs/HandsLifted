using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.SlideTheme;
using Material.Icons;

namespace HandsLiftedApp.Core.Converters
{
    // Used on a "move to group" menu item's icon: shows a filled radio bullet when the bound
    // Type already matches the item's target group (ConverterParameter), a hollow one otherwise.
    public class RadioMenuIconConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SlideThemeType type && parameter is string targetName &&
                Enum.TryParse<SlideThemeType>(targetName, out var target) && type == target)
            {
                return MaterialIconKind.RadioboxMarked;
            }

            return MaterialIconKind.RadioboxBlank;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
