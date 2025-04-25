using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HandsLiftedApp.Views.ControlModules
{
    public class BooleanToPlayPauseToggleIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPaused)
            {
                return isPaused ? "Play" : "Pause";
            }
            return "Play";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}