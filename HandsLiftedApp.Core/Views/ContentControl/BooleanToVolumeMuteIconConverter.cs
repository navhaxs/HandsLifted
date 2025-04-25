using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HandsLiftedApp.Views.ControlModules
{
    public class BooleanToVolumeMuteIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMuted)
            {
                return isMuted ? "VolumeMute" : "VolumeHigh";
            }
            return "VolumeHigh";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}