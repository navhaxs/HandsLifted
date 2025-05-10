using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Material.Icons;

namespace HandsLiftedApp.Core.Converters
{
    public class FullscreenIconKindConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WindowState state)
            {
                return state == WindowState.FullScreen ? MaterialIconKind.FullscreenExit : MaterialIconKind.Fullscreen;
            }
            return MaterialIconKind.Fullscreen;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}